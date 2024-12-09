using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Management.Smo;
using System.Text;

namespace DataVoyager.Export.Internals;

internal sealed class DBScripter
{
    private readonly Database _db;
    private readonly ScriptingOptions _scriptOptions;
    private readonly ILogger _logger;

    internal DBScripter(ILogger logger, Database db, ScriptingOptions scriptOptions)
    {
        _db = db;
        _scriptOptions = scriptOptions;
        _logger = logger;
    }

    internal int ScriptDatabase(Parameters parameters)
    {
        // If the user selected any of the specific database objects to script then disable the 'All' flag.
        if (parameters.DbObjects)
        {
            parameters.All = false;
        }
        else
        {
            parameters.DbObjects = true;
        }

        // Delete the output file if it exists.
        File.Delete(_scriptOptions.FileName);

        // Prefetch some database objects in an effort to speed up the scripting process.
        _db.PrefetchObjects(typeof(Schema), _scriptOptions);
        _db.PrefetchObjects(typeof(Table), _scriptOptions);
        _db.PrefetchObjects(typeof(StoredProcedure), _scriptOptions);
        _db.PrefetchObjects(typeof(UserDefinedFunction), _scriptOptions);
        _db.PrefetchObjects(typeof(View), _scriptOptions);

        // Script the database objects.
        Script(ScriptSchemas, parameters.Schemas, "schema names");
        Script(ScriptTables, parameters.Tables, "tables");
        Script(ScriptIndexes, parameters.Indexes, "non-clustered indexes");
        Script(ScriptForeignKeys, parameters.ForeignKeys, "foreign keys");
        Script(ScriptStoredProcedures, parameters.Procedures, "stored procedures");
        Script(ScriptUserDefinedFunctions, parameters.Functions, "user-defined functions");
        Script(ScriptViews, parameters.Views, "views");
        Script(ScriptTriggers, parameters.Triggers, "triggers");
        Script(ScriptUsers, parameters.Users, "users");

        return 0;
    }

    private void Script(Action script, bool scriptFlag, string objectName)
    {
        _logger.LogInformation($"Scripting {objectName} for database {_db.Name}.");

        try
        {
            if (scriptFlag)
            {
                script();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Scripting {objectName} for database {_db.Name}: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private void ScriptForeignKeys()
    {
        _scriptOptions.DriForeignKeys = true;

        StringBuilder header = new StringBuilder()
            .AppendLine()
            .AppendLine("/*****************************************************************************")
            .AppendLine(" * FOREIGN KEYS                                                              *")
            .AppendLine(" *****************************************************************************/");
        File.AppendAllText(_scriptOptions.FileName, header.ToString());

        foreach (Table table in _db.Tables)
        {
            if (!table.IsSystemObject)
            {
                foreach (ForeignKey foreignKey in table.ForeignKeys)
                {
                    File.AppendAllText(_scriptOptions.FileName, Environment.NewLine + $"/******  Object:  Foreign Key [{foreignKey.Name}]  ******/" + Environment.NewLine);
                    foreignKey.Script(_scriptOptions);
                }
            }
        }

        _scriptOptions.DriForeignKeys = false;
    }

    private void ScriptIndexes()
    {
        _scriptOptions.DriNonClustered = true;

        StringBuilder header = new StringBuilder()
            .AppendLine()
            .AppendLine("/*****************************************************************************")
            .AppendLine(" * INDEXES                                                                   *")
            .AppendLine(" *****************************************************************************/");
        File.AppendAllText(_scriptOptions.FileName, header.ToString());

        foreach (Table table in _db.Tables)
        {
            if (!table.IsSystemObject)
            {
                foreach (Microsoft.SqlServer.Management.Smo.Index index in table.Indexes)
                {
                    if (!index.IsSystemObject && !index.IsClustered)
                    {
                        File.AppendAllText(_scriptOptions.FileName, Environment.NewLine + $"/******  Object:  Index [{index.Name}]  ******/" + Environment.NewLine);
                        index.Script(_scriptOptions);
                    }
                }
            }
        }

        _scriptOptions.DriNonClustered = false;
    }

    private void ScriptSchemas()
    {
        _scriptOptions.Permissions = true;

        StringBuilder header = new StringBuilder()
            .AppendLine()
            .AppendLine("/*****************************************************************************")
            .AppendLine(" * SCHEMAS                                                                   *")
            .AppendLine(" *****************************************************************************/");
        File.AppendAllText(_scriptOptions.FileName, header.ToString());

        foreach (Schema schema in _db.Schemas)
        {
            if (!schema.IsSystemObject)
            {
                File.AppendAllText(_scriptOptions.FileName, Environment.NewLine + $"/******  Object:  Schema [{schema.Name}]  ******/" + Environment.NewLine);
                schema.Script(_scriptOptions);
            }
        }

        _scriptOptions.Permissions = false;
    }

    private void ScriptStoredProcedures()
    {
        StringBuilder header = new StringBuilder()
            .AppendLine()
            .AppendLine("/*****************************************************************************")
            .AppendLine(" * STORED PROCEDURES                                                         *")
            .AppendLine(" *****************************************************************************/");
        File.AppendAllText(_scriptOptions.FileName, header.ToString());

        foreach (StoredProcedure storedProcedure in _db.StoredProcedures)
        {
            if (!storedProcedure.IsSystemObject && !storedProcedure.Name.StartsWith("sp_"))
            {
                File.AppendAllText(_scriptOptions.FileName, Environment.NewLine + $"/******  Object:  StoredProcedure [{storedProcedure.Schema}].[{storedProcedure.Name}]  ******/" + Environment.NewLine);
                storedProcedure.Script(_scriptOptions);
            }
        }
    }

    private void ScriptTables()
    {
        StringBuilder header = new StringBuilder()
            .AppendLine()
            .AppendLine("/*****************************************************************************")
            .AppendLine(" * TABLES                                                                    *")
            .AppendLine(" *****************************************************************************/");
        File.AppendAllText(_scriptOptions.FileName, header.ToString());

        foreach (Table table in _db.Tables)
        {
            if (!table.IsSystemObject)
            {
                File.AppendAllText(_scriptOptions.FileName, Environment.NewLine + $"/******  Object:  Table [{table.Schema}].[{table.Name}]  ******/" + Environment.NewLine);
                table.Script(_scriptOptions);
            }
        }
    }

    private void ScriptTriggers()
    {
        _scriptOptions.Triggers = true;

        StringBuilder header = new StringBuilder()
            .AppendLine()
            .AppendLine("/*****************************************************************************")
            .AppendLine(" * TRIGGERS                                                                  *")
            .AppendLine(" *****************************************************************************/");
        File.AppendAllText(_scriptOptions.FileName, header.ToString());

        // Script database level triggers first.
        foreach (DatabaseDdlTrigger trigger in _db.Triggers)
        {
            if (!trigger.IsSystemObject)
            {
                File.AppendAllText(_scriptOptions.FileName, Environment.NewLine + $"/******  Object:  Trigger [{trigger.Name}]  ******/" + Environment.NewLine);
                trigger.Script(_scriptOptions);
            }
        }

        // Now script the table level triggers.
        foreach (Table table in _db.Tables)
        {
            if (!table.IsSystemObject)
            {
                foreach (Trigger trigger in table.Triggers)
                {
                    if (!trigger.IsSystemObject)
                    {
                        File.AppendAllText(_scriptOptions.FileName, Environment.NewLine + $"/******  Object:  Trigger [{trigger.Name}]  ******/" + Environment.NewLine);
                    }
                }
            }
        }

        _scriptOptions.Triggers = false;
    }

    private void ScriptUserDefinedFunctions()
    {
        StringBuilder header = new StringBuilder()
            .AppendLine()
            .AppendLine("/*****************************************************************************")
            .AppendLine(" * USER-DEFINED FUNCTIONS                                                    *")
            .AppendLine(" *****************************************************************************/");
        File.AppendAllText(_scriptOptions.FileName, header.ToString());

        foreach (UserDefinedFunction userDefinedFunction in _db.UserDefinedFunctions)
        {
            if (!userDefinedFunction.IsSystemObject)
            {
                File.AppendAllText(_scriptOptions.FileName, Environment.NewLine + $"/******  Object:  Function [{userDefinedFunction.Schema}].[{userDefinedFunction.Name}]  ******/" + Environment.NewLine);
                userDefinedFunction.Script(_scriptOptions);
            }
        }
    }

    private void ScriptUsers()
    {
        StringBuilder header = new StringBuilder()
            .AppendLine()
            .AppendLine("/*****************************************************************************")
            .AppendLine(" * USERS                                                                     *")
            .AppendLine(" *****************************************************************************/");
        File.AppendAllText(_scriptOptions.FileName, header.ToString());

        foreach (User user in _db.Users)
        {
            if (!user.IsSystemObject)
            {
                File.AppendAllText(_scriptOptions.FileName, Environment.NewLine + $"/******  Object:  User [{user.Name}]  ******/" + Environment.NewLine);
                user.Script(_scriptOptions);
            }
        }
    }

    private void ScriptViews()
    {
        StringBuilder header = new StringBuilder()
            .AppendLine()
            .AppendLine("/*****************************************************************************")
            .AppendLine(" * VIEWS                                                                     *")
            .AppendLine(" *****************************************************************************/");
        File.AppendAllText(_scriptOptions.FileName, header.ToString());

        foreach (View view in _db.Views)
        {
            if (!view.IsSystemObject)
            {
                File.AppendAllText(_scriptOptions.FileName, Environment.NewLine + $"/******  Object:  View [{view.Schema}].[{view.Name}]  ******/" + Environment.NewLine);
                view.Script(_scriptOptions);
            }
        }
    }
}