using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace DataAccessor.Library.Core;

public class MultiServerDataAccess : IMultiServerDataAccess
{
    private readonly IMultiServerConnectionFactory _connectionFactory;
    private readonly DistributedTransactionManager _transactionManager;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly IReadOnlyDictionary<string, int> _commandTimeouts;

    public MultiServerDataAccess(IMultiServerConnectionFactory connectionFactory)
        : this(connectionFactory, options: null)
    {
    }

    public MultiServerDataAccess(IMultiServerConnectionFactory connectionFactory, DataAccessOptions? options)
    {
        _connectionFactory = connectionFactory;
        _transactionManager = options == null
            ? new DistributedTransactionManager(connectionFactory)
            : new DistributedTransactionManager(connectionFactory, options);
        _jsonSerializerOptions = options?.JsonOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
        _commandTimeouts = options?.Databases.ToDictionary(
            database => database.Name,
            database => database.CommandTimeout,
            StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<JsonDocument> QueryJsonAsync(
        string connectionName,
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(connectionName);
        var result = await connection.QueryAsync(
            new CommandDefinition(
                sql,
                parameters,
                commandTimeout: ResolveCommandTimeout(connectionName),
                commandType: commandType,
                cancellationToken: cancellationToken));

        return JsonSerializer.SerializeToDocument(result, _jsonSerializerOptions);
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(
        string connectionName,
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(connectionName);
        return await connection.QueryAsync<T>(
            new CommandDefinition(
                sql,
                parameters,
                commandTimeout: ResolveCommandTimeout(connectionName),
                commandType: commandType,
                cancellationToken: cancellationToken));
    }

    public async Task<T?> QuerySingleAsync<T>(
        string connectionName,
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(connectionName);
        return await connection.QuerySingleOrDefaultAsync<T>(
            new CommandDefinition(
                sql,
                parameters,
                commandTimeout: ResolveCommandTimeout(connectionName),
                commandType: commandType,
                cancellationToken: cancellationToken));
    }

    public async Task<int> ExecuteAsync(
        string connectionName,
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(connectionName);
        return await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                parameters,
                commandTimeout: ResolveCommandTimeout(connectionName),
                commandType: commandType,
                cancellationToken: cancellationToken));
    }

    public async Task<JsonDocument> QueryMultipleAsync(
        IReadOnlyDictionary<string, string> queries,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queries);

        var results = await Task.WhenAll(queries.Select(async query =>
        {
            using var document = await QueryJsonAsync(
                query.Key,
                query.Value,
                parameters,
                commandType,
                cancellationToken);
            return new KeyValuePair<string, JsonElement>(query.Key, document.RootElement.Clone());
        }));

        return JsonSerializer.SerializeToDocument(
            results.ToDictionary(item => item.Key, item => item.Value),
            _jsonSerializerOptions);
    }

    public async Task<JsonDocument> ExecuteMultipleAsync(
        IReadOnlyDictionary<string, string> commands,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commands);

        var results = await Task.WhenAll(commands.Select(async command =>
        {
            var affectedRows = await ExecuteAsync(
                command.Key,
                command.Value,
                parameters,
                commandType,
                cancellationToken);
            return new KeyValuePair<string, int>(command.Key, affectedRows);
        }));

        return JsonSerializer.SerializeToDocument(
            results.ToDictionary(item => item.Key, item => item.Value),
            _jsonSerializerOptions);
    }

    public async Task<IEnumerable<T>> QueryAcrossConnectionsAsync<T>(
        IEnumerable<string> connectionNames,
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionNames);

        var results = await Task.WhenAll(connectionNames.Select(
            connectionName => QueryAsync<T>(
                connectionName,
                sql,
                parameters,
                commandType,
                cancellationToken)));
        return results.SelectMany(result => result).ToList();
    }

    public Task<DistributedTransactionResult> ExecuteDistributedTransactionAsync(
        Dictionary<string, IEnumerable<(string command, object? parameters)>> commandsByServer,
        CancellationToken cancellationToken = default)
    {
        return _transactionManager.ExecuteDistributedTransactionAsync(commandsByServer, cancellationToken);
    }

    private int? ResolveCommandTimeout(string connectionName)
    {
        return _commandTimeouts.TryGetValue(connectionName, out var commandTimeout)
            ? commandTimeout
            : null;
    }
}
