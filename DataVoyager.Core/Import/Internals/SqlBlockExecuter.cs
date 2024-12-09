using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DataVoyager.Import.Internals;

internal sealed class SqlBlockExecuter
{
    private readonly SqlConnection _sqlConnection;

    internal SqlBlockExecuter(SqlConnection sqlConnection) => _sqlConnection = sqlConnection;

    internal async Task Execute(string sqlScript, CancellationToken cancellationToken)
    {
        try
        {
            // Split the script into individual commands based on 'GO'
            string[] sqlCommands = Regex.Split(sqlScript, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

            if (_sqlConnection.State != System.Data.ConnectionState.Open)
            {
                await _sqlConnection.OpenAsync(cancellationToken);
            }

            foreach (var command in sqlCommands)
            {
                // Skip empty commands
                if (string.IsNullOrWhiteSpace(command)) continue;

                using (SqlCommand sqlCommand = new (command, _sqlConnection))
                {
                    await sqlCommand.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            Console.WriteLine("SQL script executed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}
