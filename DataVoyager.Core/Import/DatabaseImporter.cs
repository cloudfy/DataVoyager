using DataVoyager.Abstracts;
using DataVoyager.Import.Internals;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.IO.Compression;

namespace DataVoyager.Import;

public sealed class DatabaseImporter(
    ILogger<DatabaseImporter> logger) : AbstractHandler, IDatabaseImporter
{
    private readonly ILogger<DatabaseImporter> _logger = logger;

    public async Task Import(
        string connectionString, string file, CancellationToken cancellationToken = default)
    {
        var inputFile = InputInformation.Create(file);

        UnzipAndPrepare(inputFile);

        var connection = await ConnectAsync(connectionString, cancellationToken);

        await CreateSchema(inputFile, connection, cancellationToken);
        await ImportData(inputFile, connection, cancellationToken);

        await connection.CloseAsync();

        CleanUp(inputFile);
    }

    private async Task ImportData(InputInformation inputFile, SqlConnection connection, CancellationToken cancellationToken)
    {
        var tempDirectory = new DirectoryInfo(inputFile.TempPath);
        
        foreach (var folder in tempDirectory.GetDirectories("*").OrderBy(o => o.Name)) 
        {
            _logger.LogDebug($"Processing folder: {folder.Name}");

            var fileName = Path.Combine(folder.FullName, "data.sql");

            if (File.Exists(fileName))
            {
                var content = await File.ReadAllTextAsync(fileName, cancellationToken);
                await new SqlBlockExecuter(connection)
                    .Execute(content, cancellationToken);
            }
        }
        _logger.LogTrace("Data imported successfully.");
    }
    private async Task CreateSchema(
        InputInformation input
        , SqlConnection connection
        , CancellationToken cancellationToken)
    {
        try
        {
            var content = await File.ReadAllTextAsync(input.SchemaFile, cancellationToken);

            await new SqlBlockExecuter(connection)
                .Execute(content, cancellationToken);
            //var schema = new Schema(connection);
            //await schema.CreateAsync(input.TempPath);
        }
        catch
        {
            throw;
        }
    }

    private void CleanUp(InputInformation input)
    {
        try
        {
            if (Directory.Exists(input.TempPath))
            {
                Directory.Delete(input.TempPath, true);
            }
        }
        catch
        {
            throw;
        }
    }

    private void UnzipAndPrepare(InputInformation file)
    {
        try
        {
            ZipFile.ExtractToDirectory(file.FullName, file.TempPath);
            _logger.LogTrace($"Directory unzipped successfully to: {file.TempPath}");
        }
        catch
        {
            throw;
        }
    }
}
