namespace DataVoyager.Import.Internals;

internal sealed class InputInformation
{
    private InputInformation(string fullname, string path, string tempName)
    {
        Path = path;
        FullName = fullname;
        TempPath = tempName;
    }

    internal string Path { get; private set; }
    internal string FullName { get; private set; }
    internal string TempPath { get; private set; }
    internal string SchemaFile => System.IO.Path.Combine(TempPath, "schema.sql");
    internal static InputInformation Create(string fullname)
    {
        var path = System.IO.Path.GetDirectoryName(fullname)!;
        var tempName = System.IO.Path.Combine(path, "_temp");
        return new InputInformation(fullname, path, tempName);
    }
}
