using DataVoyager.Abstracts;
using DataVoyager.Import.Internals;
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

        await connection.CloseAsync();
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
