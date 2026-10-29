using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using DataAccessor.Library.Factories;

namespace DataAccessor.Library.Core;

public class ConfigurableMultiServerConnectionFactory : IMultiServerConnectionFactory
{
    private readonly IReadOnlyDictionary<string, DatabaseConnectionOptions> _connections;

    public ConfigurableMultiServerConnectionFactory(IEnumerable<DatabaseConnectionOptions> connections)
    {
        ArgumentNullException.ThrowIfNull(connections);

        _connections = new ReadOnlyDictionary<string, DatabaseConnectionOptions>(
            connections.ToDictionary(option => option.Name, StringComparer.OrdinalIgnoreCase));
    }

    public async Task<DbConnection> CreateConnectionAsync(string connectionName)
    {
        var options = GetConnectionOptions(connectionName);
        var connection = await DatabaseConnectionFactory.CreateConnectionAsync(options);
        await EnsureOpenAsync(connection);
        return connection;
    }

    public DbConnection CreateConnection(string connectionName)
    {
        var options = GetConnectionOptions(connectionName);
        var connection = DatabaseConnectionFactory.CreateConnection(options);
        EnsureOpen(connection);
        return connection;
    }

    public async Task<IDbTransaction> BeginTransactionAsync(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await EnsureOpenAsync(connection);
        return await connection.BeginTransactionAsync();
    }

    private DatabaseConnectionOptions GetConnectionOptions(string connectionName)
    {
        if (!_connections.TryGetValue(connectionName, out var options))
        {
            throw new ArgumentException($"Connection string '{connectionName}' not found.", nameof(connectionName));
        }

        return options;
    }

    private static void EnsureOpen(DbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }
    }

    private static async Task EnsureOpenAsync(DbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
    }
}
