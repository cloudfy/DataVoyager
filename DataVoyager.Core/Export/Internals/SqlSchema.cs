namespace DataVoyager.Export.Internals;

internal static class SqlSchema
{
    internal static void WriteFile(string filePath, string v1, bool v2)
    {
        throw new NotImplementedException();
    }

    internal static void Write(string v1, bool v2)
    {
        Console.WriteLine(v1);
    }
    internal static void Write(TextWriter writer, string value, bool v2 = true)
    {
        writer.Write(value);
    }
}
