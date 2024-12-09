
namespace DataVoyager.Export
{
    public interface IDatabaseExporter
    {
        Task Export(
            string connectionString
            , string output
            , ExportOptions options
            , CancellationToken cancellationToken = default);
    }
}