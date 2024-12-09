using DataVoyager.Abstracts;
using DataVoyager.Export.Internals;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using System.IO.Compression;

namespace DataVoyager.Export;

public sealed class DatabaseExporter(
    ILogger<DatabaseExporter> logger) : AbstractHandler, IDatabaseExporter
{
    private readonly ExportOptions _options = new();
    private readonly ILogger<DatabaseExporter> _logger = logger;

    public async Task Export(
        string connectionString
        , string output
        , ExportOptions options
        , CancellationToken cancellationToken = default)
    {
        var connection = await ConnectAsync(connectionString, cancellationToken);

        var outputDetails = OutputInformation.Create(output);
        ReadyBuild(outputDetails);

        // generate schema and data
        GenerateSchema(connection, outputDetails);
        await ExportData(connection, outputDetails, options.IgnoreObjects, cancellationToken);

        await connection.CloseAsync();

        ZipAndCleanup(outputDetails, cancellationToken);

        _logger.LogInformation($"Export completed successfully.\n\n{outputDetails.FullName}");
    }

    private static void ReadyBuild(OutputInformation output)
    {
        if (Directory.Exists(output.BuildPath))
            Directory.Delete(output.BuildPath, true);
        Directory.CreateDirectory(output.BuildPath);
    }

    private void ZipAndCleanup(OutputInformation output, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Compiling file...");

            // Create a zip file from the directory
            ZipFile.CreateFromDirectory(
                output.BuildPath
                , output.ZipFile
                , CompressionLevel.Fastest
                , includeBaseDirectory: false);
            _logger.LogTrace($"Directory zipped successfully to: {output.ZipFile}");

            File.Move(output.ZipFile, output.FullName, false);
            Directory.Delete(output.BuildPath, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
    
    private async Task ExportData(
        SqlConnection sqlConnection
        , OutputInformation output
        , string[] ignoreObjects
        , CancellationToken cancellationToken = default)
    {
        var sc = new ServerConnection(sqlConnection);
        sc.Connect();
        sc.InfoMessage += (sender, e) => _logger.LogInformation(e.Message);
        sc.ServerMessage += (sender, e) => _logger.LogInformation(e.Error.Message);

        var server = new Server(sc);

        var db = server.Databases
            .Cast<Database>()
            .AsQueryable()
            .Where(o => o.IsSystemObject == false)
            .First();
      
        if (db.IsSystemObject)
            return;

        foreach (Table table in db.Tables)
        {
            if (table.IsSystemObject)
                continue;
            _logger.LogInformation($"Export data - {table.Name}");

            if (ignoreObjects.Any(_ => _.Equals(table.Name, StringComparison.InvariantCultureIgnoreCase)))
            {
                _logger.LogInformation($"Table {table.Name} is ignored.");
                continue;
            }

            string outputPathFile = Path.Combine(output.BuildPath, table.Name);
            if (Directory.Exists(outputPathFile) == false)
                Directory.CreateDirectory(outputPathFile);

            string outputFile = Path.Combine(outputPathFile, "data.sql");

            ScriptingOptions scriptingOptions = new() 
            {
                ScriptData = true
                , ScriptSchema = false
                , FileName = outputFile
                , AppendToFile = true
                , ToFileOnly = true
                , AnsiFile = true
                , IncludeHeaders = true
                , Indexes = false
                , DriAll = false
            };

            try
            {
                var scripter = new Scripter(server) { Options = scriptingOptions };
                var script = scripter.EnumScript([table]);
            }
            catch (Exception e)
            {
                e = e;
            }
        }
    }
    private void GenerateSchema(
        SqlConnection sqlConnection
        , OutputInformation output)
    {
        var sc = new ServerConnection(sqlConnection);
        sc.Connect();

        var server = new Server(sc);

        string outputFile = Path.Combine(output.BuildPath, "schema.sql");

        var db = server.Databases
            .Cast<Database>()
            .AsQueryable()
            .Where(o => o.IsSystemObject == false)
            .First();

        DBScripter scripter = new (
            _logger
            , db
            , new ScriptingOptions()
            {
                AnsiFile = true,
                AppendToFile = true,
                ClusteredIndexes = true,
                Default = true,
                DriPrimaryKey = true,
                EnforceScriptingOptions = true,
                ExtendedProperties = true,
                FileName = outputFile,
                IncludeDatabaseContext = true,
                IncludeDatabaseRoleMemberships = false,
                IncludeIfNotExists = true,
                NoCollation = true,
                SchemaQualify = true,
                SchemaQualifyForeignKeysReferences = true,
                ScriptSchema = true,
                ToFileOnly = true,
                Triggers = true,
            });

        // generate scripts
        scripter.ScriptDatabase(new Parameters { 
            Views = true
            , Triggers = true
            , Tables = true
            , Schemas = true
            , Procedures = true
            , DbObjects = true
            , ForeignKeys = true
            , Functions = true
            , Indexes = true
            , Users = false
            , OutputFilename = outputFile
        });
    }
}