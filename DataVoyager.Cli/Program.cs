using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using DataVoyager.Builder;
using DataVoyager.Commands;
using DataVoyager.Export;
using DataVoyager.Import;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var rootCommand = new RootCommand("DataVoyager CLI tool for exporting and importing databases to DVO files.")
{
    new ExportCommand()
    , new ImportCommand()
};
var builder = new CommandLineBuilder(rootCommand);
builder.UseDefaults();
builder.UseDependencyInjection(services => { 
    services.AddSingleton<IDatabaseExporter, DatabaseExporter>();
    services.AddSingleton<IDatabaseImporter, DatabaseImporter>();

    services.AddLogging(o => {
        o.AddConsole();
        o.SetMinimumLevel(LogLevel.Debug);
    });
});

return await builder.Build().InvokeAsync(args);