using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Management.Smo;

namespace DataVoyager.Export.Internals;
internal static class Helpers
{
    internal static bool WriteSQLInner<T>(
        string db
        , string schema
        , string objType
        , string objName
        , TextWriter writer
        , T o
        , ScriptingOptions so
        , ILogger? logger = null) where T : SqlSmoObject
    {
        if (schema == "")
            schema = "dbo";
        if (db == "*")
            logger?.LogInformation(objType + ": " + objName);
        else
            logger?.LogInformation(objType + ": " + db + "." + schema + "." + objName + " (" + so.ToString() + ")");

        System.Collections.Specialized.StringCollection cs = [];
        try
        {
            //var srpt = new Scripter();
            //srpt.Options = so;
            //srpt.Options.SetTargetDatabaseEngineType(Microsoft.SqlServer.Management.Common.DatabaseEngineType.Standalone);
            //(o as Scripter).Options.Script(so);
            cs = (o as dynamic).Script(so);
        }
        catch (Exception)
        {
            throw;
        }

        if (cs != null)
        {
            var ts = "";
            foreach (var s in cs)
                ts += s + Environment.NewLine;
            if (!String.IsNullOrWhiteSpace(ts.Trim()))
            {
                // if (!File.Exists(filePath))
                SqlSchema.Write(writer, SqlComments(db, schema, objType, objName), true);

                SqlSchema.Write(writer, ts + ";" + Environment.NewLine, true);

                writer.Flush();
            }
        }

        return true;
    }


    private static string SqlComments(string db, string schema, string type, string name)
    {
        var s = "--****************************************************" + Environment.NewLine;
        s += "--DataVoyager" + Environment.NewLine;
        s += "--Export database schema." + Environment.NewLine;
        s += "-------------------------------------------------------" + Environment.NewLine;
        s += "--DB: " + db + Environment.NewLine;
        s += "--SCHEMA: " + schema + Environment.NewLine;
        s += "--" + type + ": " + name + Environment.NewLine;
        s += "--" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + Environment.NewLine;
        s += "--****************************************************" + Environment.NewLine + Environment.NewLine;
        return s;
    }
}