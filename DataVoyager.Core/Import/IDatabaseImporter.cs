
namespace DataVoyager.Import
{
    public interface IDatabaseImporter
    {
        Task Import(string connectionString, string file, CancellationToken cancellationToken = default);
    }
}