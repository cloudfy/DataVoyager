namespace DataVoyager.Export;

public class ExportOptions
{
    public bool ScriptDatabase { get; set; } = false;
    public bool ScriptSchema { get; set; } = true;
    public bool ExportFullText { get; set; } = false;
    public bool ExportStatistics { get; set; } = false;
    public bool ExportViews { get; set; } = false;
    public bool ScriptUserDefinedTypes { get; set; } = true;
    public bool ScriptIndexes { get; set; } = false;
    public string[] IgnoreObjects { get; set; } = [];

    public ExportOptions WithIgoreObjects(string[] ignoreObjects)
    {
        IgnoreObjects = ignoreObjects;
        return this;
    }
}
