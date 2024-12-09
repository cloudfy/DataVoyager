using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataVoyager.Abstracts;

public abstract class AbstractHandler
{
    protected internal async Task<SqlConnection> ConnectAsync(
        string connectionString
        , CancellationToken cancellationToken = default)
    {
        var cn = new SqlConnection(connectionString);

        try
        {
            await cn.OpenAsync(cancellationToken);
        }
        catch (Exception)
        {
            throw;
        }

        return cn;

    }
}
