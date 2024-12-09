using DataVoyager.Commands.Abstractions;
using DataVoyager.Import;
using System.CommandLine;

namespace DataVoyager.Commands;

public class ImportCommand : Command<ImportCommandOptions, ImportCommandOptionsHandler>
{
    public ImportCommand() : base("import", "Imports a DVO file to a database.")
    {
        AddOption(new Option<string>("--connection", "The connection string to the database."));
        AddOption(new Option<string>("--file", "The dvo file path, ex. package.dvo"));
    }
}

public class ImportCommandOptions : ICommandOptions
{
    public required string Connection { get; set; }
    public required string File { get; set; }
}
public class ImportCommandOptionsHandler(
    IDatabaseImporter databaseImporter) : ICommandOptionsHandler<ImportCommandOptions>
{
    private readonly IDatabaseImporter _databaseImporter = databaseImporter;

    public async Task<int> HandleAsync(ImportCommandOptions options, CancellationToken cancellationToken)
    {
        await _databaseImporter.Import(options.Connection, options.File, cancellationToken);
        return 0;
    }
}