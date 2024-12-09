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
        await GenerateSchema(connection, outputDetails, cancellationToken);
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
    private async Task GenerateSchema(
        SqlConnection sqlConnection
        , OutputInformation output
        , CancellationToken cancellationToken = default)
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

        //using var outputWriter = new StreamWriter(outputFile);

        //foreach (var db in server.Databases
        //    .Cast<Database>()
        //    .AsQueryable()
        //    .Where(o => o.IsSystemObject == false))
        //{
        //    if (db.IsSystemObject)
        //        continue;

        //    _logger.LogInformation($"Exporting database - {db.Name}");

        //    if (_options.ScriptDatabase)
        //    {
        //        /// *************************************************************
        //        /// Database
        //        Helpers.WriteSQLInner<Database>(
        //            db.Name, "", "DB", db.Name, outputWriter, db, ScriptOption.Default, _logger);
        //    }
        //    if (_options.ScriptSchema)
        //    {
        //        /// *************************************************************
        //        /// Schema
        //        foreach (var schema2 in db.Schemas.Cast<Schema>().AsQueryable())
        //        {
        //            Helpers.WriteSQLInner<Schema>(
        //                db.Name, "", "Schema", schema2.Name, outputWriter, schema2, ScriptOption.ScriptSchema, _logger);
        //        }
        //    }

        //    if (_options.ScriptUserDefinedTypes) 
        //    { 
        //        /// *************************************************************
        //        /// DB USER TYPES
        //        // currentPath = csFile.CreateFolder(dbPath, pathify("UTYPE"));
        //        foreach (UserDefinedType o in db.UserDefinedTypes)
        //        {
        //            Helpers.WriteSQLInner<UserDefinedType>(db.Name, o.Schema, "UTYPE", o.Name, outputWriter, o, ScriptOption.Default, _logger);
        //        }
        //    }

        //    /// *************************************************************
        //    /// DB TRIGGERS
        //    //currentPath = csFile.CreateFolder(dbPath, pathify("TRIGGER"));
        //    foreach (DatabaseDdlTrigger o in db.Triggers.Cast<DatabaseDdlTrigger>().AsQueryable().Where(o => o.IsSystemObject == false))
        //    {
        //        Helpers.WriteSQLInner<DatabaseDdlTrigger>(
        //            db.Name, "dbo", "TRIGGER", o.Name, outputWriter, o, ScriptOption.Triggers, _logger);
        //    }

        //    /// *************************************************************
        //    /// DB USER TABLE TYPES
        //    //currentPath = csFile.CreateFolder(dbPath, pathify("TTYPES"));
        //    foreach (UserDefinedTableType o in db.UserDefinedTableTypes)
        //    {
        //        Helpers.WriteSQLInner<UserDefinedTableType>(db.Name, o.Schema, "TTYPES", o.Name, outputWriter, o, ScriptOption.Default, _logger);
        //    }

        //    /// *************************************************************
        //    /// DB FULLTEXT CATALOGS
        //    if (_options.ExportFullText)
        //    {
        //        //currentPath = csFile.CreateFolder(dbPath, pathify("FTC"));
        //        foreach (FullTextCatalog o in db.FullTextCatalogs)
        //        {
        //            Helpers.WriteSQLInner<FullTextCatalog>(
        //                db.Name, "dbo", "FTC", o.Name, outputWriter, o, ScriptOption.FullTextCatalogs, _logger);
        //        }

        //        /// *************************************************************
        //        /// DB FULLTEXT STOPLISTS
        //        //currentPath = csFile.CreateFolder(dbPath, pathify("FTL"));
        //        foreach (FullTextStopList o in db.FullTextStopLists)
        //        {
        //            // filePath = PrepareSqlFile(db.Name, "dbo", "FTL", o.Name, currentPath, "");
        //            Helpers.WriteSQLInner<FullTextStopList>(db.Name, "dbo", "FTL", o.Name, outputWriter, o, ScriptOption.Default, _logger);
        //        }
        //    }

        //    /// *************************************************************
        //    /// STORED PROCEDURES
        //    //currentPath = csFile.CreateFolder(dbPath, pathify("PROCEDURE"));
        //    foreach (StoredProcedure o in db.StoredProcedures.Cast<StoredProcedure>().AsQueryable().Where(o => o.IsSystemObject == false))
        //    {
        //        // filePath = PrepareSqlFile(db.Name, o.Schema, "PROCEDURE", o.Name, currentPath, "");
        //        Helpers.WriteSQLInner<StoredProcedure>(db.Name, o.Schema, "PROCEDURE", o.Name, outputWriter, o, ScriptOption.Default, _logger);
        //    }

        //    /// *************************************************************
        //    /// FUNCTIONS
        //    // currentPath = csFile.CreateFolder(dbPath, pathify("FUNCTION"));
        //    foreach (UserDefinedFunction o in db.UserDefinedFunctions.Cast<UserDefinedFunction>().Where(oo => oo.IsSystemObject == false))
        //    {
        //        Helpers.WriteSQLInner<UserDefinedFunction>(
        //            db.Name, o.Schema, "FUNCTION", o.Name, outputWriter, o, ScriptOption.Default, _logger);
        //    }

        //    /// *************************************************************
        //    /// TABLE
        //    foreach (Table o in db.Tables.Cast<Table>().AsQueryable().Where(o => o.IsSystemObject == false))
        //    {
        //        _logger.LogInformation($"Export table - {o.Name}");

        //        //currentPath = csFile.CreateFolder(dbPath, pathify("TABLE"));
        //        //filePath = PrepareSqlFile(db.Name, o.Schema, "TABLE", o.Name, currentPath, "");
        //        Helpers.WriteSQLInner<Table>(db.Name, o.Schema, "TABLE", o.Name, outputWriter, o, ScriptOption.Default, _logger);
        //        if (_options.ScriptIndexes) 
        //        { 
        //            Helpers.WriteSQLInner<Table>(db.Name, o.Schema, "TABLE", o.Name, outputWriter, o, ScriptOption.Indexes, _logger);
        //        }
        //        Helpers.WriteSQLInner<Table>(db.Name, o.Schema, "TABLE", o.Name, outputWriter, o, ScriptOption.DriAll, _logger);

        //        //////////////////////////////////////////////////////////////////////////
        //        /// TABLE TRIGGERS
        //        //currentPath = csFile.CreateFolder(dbPath, pathify("TRIGGER"));
        //        foreach (Trigger ot in o.Triggers.Cast<Trigger>().AsQueryable().Where(oo => oo.IsSystemObject == false))
        //        {
        //            //filePath = PrepareSqlFile(db.Name, o.Schema, "TRIGGER", ot.Name, currentPath, "TABLE_" + o.Name);
        //            Helpers.WriteSQLInner<Trigger>(db.Name, o.Schema, "TRIGGER", ot.Name, outputWriter, ot, ScriptOption.Default, _logger);
        //        }

        //        //////////////////////////////////////////////////////////////////////////
        //        /// TABLE STATISTICS
        //        if (_options.ExportStatistics)
        //        {
        //            //currentPath = csFile.CreateFolder(dbPath, pathify("STATISTIC"));
        //            foreach (Statistic ot in o.Statistics.Cast<Statistic>().AsQueryable())
        //            {
        //                //filePath = PrepareSqlFile(db.Name, o.Schema, "STATISTIC", ot.Name, currentPath, "TABLE_" + o.Name);
        //                Helpers.WriteSQLInner<Statistic>(db.Name, o.Schema, "STATISTIC", ot.Name, outputWriter, ot, ScriptOption.OptimizerData, _logger);
        //            }
        //        }
        //    }

        //    //////////////////////////////////////////////////////////////////////////
        //    // VIEWS
        //    if (_options.ExportViews)
        //    {
        //        foreach (View o in db.Views
        //            .Cast<View>()
        //            .AsQueryable()
        //            .Where(o => o.IsSystemObject == false))
        //        {

        //            // currentPath = csFile.CreateFolder(dbPath, pathify("VIEW"));
        //            // filePath = PrepareSqlFile(db.Name, o.Schema, "VIEW", o.Name, currentPath, "");
        //            Helpers.WriteSQLInner<View>(db.Name, o.Schema, "VIEW", o.Name, outputWriter, o, ScriptOption.Default, _logger);
        //            Helpers.WriteSQLInner<View>(db.Name, o.Schema, "VIEW", o.Name, outputWriter, o, ScriptOption.Indexes, _logger);
        //            Helpers.WriteSQLInner<View>(db.Name, o.Schema, "VIEW", o.Name, outputWriter, o, ScriptOption.DriAllConstraints, _logger);

        //            //////////////////////////////////////////////////////////////////////////
        //            //VIEW TRIGGERS
        //            //currentPath = csFile.CreateFolder(dbPath, pathify("TRIGGER"));
        //            foreach (Trigger ot in o.Triggers.Cast<Trigger>().AsQueryable().Where(oo => oo.IsSystemObject == false))
        //            {
        //                //filePath = PrepareSqlFile(db.Name, o.Schema, "TRIGGER", ot.Name, currentPath, "VIEW_" + o.Name);
        //                Helpers.WriteSQLInner<Trigger>(db.Name, o.Schema, "TRIGGER", ot.Name, outputWriter, ot, ScriptOption.Default, _logger);
        //            }

        //            //////////////////////////////////////////////////////////////////////////
        //            //VIEW STATISTICS
        //            if (_options.ExportStatistics)
        //            {
        //                //currentPath = csFile.CreateFolder(dbPath, pathify("STATISTIC"));
        //                foreach (Statistic ot in o.Statistics.Cast<Statistic>().AsQueryable())
        //                {
        //                    //filePath = PrepareSqlFile(db.Name, o.Schema, "STATISTIC", ot.Name, currentPath, "VIEW_" + o.Name);
        //                    Helpers.WriteSQLInner<Statistic>(db.Name, o.Schema, "STATISTIC", ot.Name, outputWriter, ot, ScriptOption.OptimizerData, _logger);
        //                }
        //            }
        //        }
        //    }
        //}

        //outputWriter.Flush();
        //outputWriter.Close();
    }
}