using DataVoyager.Commands.Abstractions;
using DataVoyager.Export;
using System.CommandLine;

namespace DataVoyager.Commands;

public class ExportCommand : Command<ExportCommandOptions, ExportCommandOptionsHandler>
{
    public ExportCommand() : base("export", "Exports a database to a DVO file, including schema and data.")
    {
        AddOption(new Option<string>("--connection", "The connection string to the database."));
        AddOption(new Option<string>("--output", "The output file path, ex. package.dvo"));
        AddOption(new Option<int>("--logging", "The logging level. Default 1 (information). 2 = warning, 3 = trace, 4 = debug."));
        AddOption(new Option<string[]>("--ignore", "The tables to ignore."));
    }
}
public class ExportCommandOptions : ICommandOptions
{
    public int? Logging { get; set; }
    public required string Connection { get; set; }
    public required string Output { get; set; }
    public string[]? Ignore { get; set; }
}
public class ExportCommandOptionsHandler : ICommandOptionsHandler<ExportCommandOptions>
{
    private readonly IConsole _console;
    private readonly IDatabaseExporter _databaseExporter;

    public ExportCommandOptionsHandler(IConsole console, IDatabaseExporter databaseExporter)
    {
        _console = console;
        _databaseExporter = databaseExporter;
    }
    public async Task<int> HandleAsync(ExportCommandOptions options, CancellationToken cancellationToken)
    {
        await _databaseExporter.Export(
            options.Connection
            , options.Output
            , new ExportOptions()
                .WithIgoreObjects(options.Ignore ?? [])
            , cancellationToken);

        return 0;
    }
}