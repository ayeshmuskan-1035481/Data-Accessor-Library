using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using DataAccessor.Library.Factories;
using Microsoft.Extensions.Configuration;

namespace DataAccessor.Library.Core;

public class MultiServerConnectionFactory : IMultiServerConnectionFactory
{
    private readonly IConfiguration _configuration;

    public MultiServerConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<DbConnection> CreateConnectionAsync(string connectionName)
    {
        var connectionString = _configuration.GetConnectionString(connectionName)
            ?? throw new ArgumentException($"Connection string '{connectionName}' not found.", nameof(connectionName));

        var provider = ResolveProvider(connectionName);
        var connection = DatabaseConnectionFactory.CreateConnection(provider, connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public DbConnection CreateConnection(string connectionName)
    {
        var connectionString = _configuration.GetConnectionString(connectionName)
            ?? throw new ArgumentException($"Connection string '{connectionName}' not found.", nameof(connectionName));

        var provider = ResolveProvider(connectionName);
        var connection = DatabaseConnectionFactory.CreateConnection(provider, connectionString);
        connection.Open();
        return connection;
    }

    public async Task<IDbTransaction> BeginTransactionAsync(DbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        return await connection.BeginTransactionAsync();
    }

    private DatabaseProvider ResolveProvider(string connectionName)
    {
        var providerName = _configuration[$"DataAccessor:Providers:{connectionName}"] ?? "SqlServer";
        return Enum.TryParse<DatabaseProvider>(providerName, ignoreCase: true, out var parsed)
            ? parsed
            : DatabaseProvider.SqlServer;
    }
}
