namespace DataVoyager.Export.Internals;

internal sealed class OutputInformation
{
    private OutputInformation(string output, string path, string buildPath)
    {
        Path = path;
        FullName = output;
        BuildPath = buildPath;
    }
    /// <summary>
    /// Root path of the DVO file.
    /// </summary>
    internal string Path { get; private set; }
    internal string FullName { get; private set; }
    internal string BuildPath { get; private set; }
    internal string ZipFile => System.IO.Path.ChangeExtension(FullName, "zip");
    internal static OutputInformation Create(string output)
    {
        var path = System.IO.Path.GetDirectoryName(output)!;
        var buildPath = System.IO.Path.Combine(path, "_build");

        return new OutputInformation(output, path, buildPath);
    }
}
