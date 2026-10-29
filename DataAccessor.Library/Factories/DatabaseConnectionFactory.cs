using System;
using System.Data.Common;
using System.Threading.Tasks;
using DataAccessor.Library;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;

namespace DataAccessor.Library.Factories;

public static class DatabaseConnectionFactory
{
    public static DbConnection CreateConnection(DatabaseProvider provider, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string must be provided.", nameof(connectionString));
        }

        return provider switch
        {
            DatabaseProvider.SqlServer => new SqlConnection(connectionString),
            DatabaseProvider.PostgreSQL => new NpgsqlConnection(connectionString),
            DatabaseProvider.MySQL => new MySqlConnection(connectionString),
            DatabaseProvider.SQLite => new SqliteConnection(connectionString),
            _ => throw new NotSupportedException($"Database provider '{provider}' is not supported.")
        };
    }

    public static DbConnection CreateConnection(DatabaseConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var connection = CreateConnection(options.Provider, options.ConnectionString);
        if (options.AutoOpen)
        {
            connection.Open();
        }

        return connection;
    }

    public static async Task<DbConnection> CreateConnectionAsync(DatabaseProvider provider, string connectionString)
    {
        var connection = CreateConnection(provider, connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public static async Task<DbConnection> CreateConnectionAsync(DatabaseConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var connection = CreateConnection(options.Provider, options.ConnectionString);
        if (options.AutoOpen)
        {
            await connection.OpenAsync();
        }

        return connection;
    }
}
