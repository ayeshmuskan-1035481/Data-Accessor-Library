using System.Data;
using System.Data.Common;
using DataAccessor.Library.Factories;

namespace DataAccessor.Library.Core;

public class ConfigurableDbConnectionFactory : IDbConnectionFactory
{
    private readonly DatabaseConnectionOptions _options;

    public ConfigurableDbConnectionFactory(DatabaseConnectionOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public DbConnection CreateConnection()
    {
        var connection = DatabaseConnectionFactory.CreateConnection(_options);
        EnsureOpen(connection);
        return connection;
    }

    public async Task<DbConnection> CreateConnectionAsync()
    {
        var connection = await DatabaseConnectionFactory.CreateConnectionAsync(_options);
        await EnsureOpenAsync(connection);
        return connection;
    }

    public IDbTransaction BeginTransaction(IDbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        EnsureOpen((DbConnection)connection);
        return connection.BeginTransaction();
    }

    public async Task<IDbTransaction> BeginTransactionAsync(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await EnsureOpenAsync(connection);
        return await connection.BeginTransactionAsync();
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
