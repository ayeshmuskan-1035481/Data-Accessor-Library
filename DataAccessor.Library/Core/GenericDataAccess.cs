using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace DataAccessor.Library.Core;

public class GenericDataAccess : IGenericDataAccess
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly int? _commandTimeout;

    public GenericDataAccess(IDbConnectionFactory connectionFactory)
        : this(connectionFactory, jsonSerializerOptions: null, commandTimeout: null)
    {
    }

    public GenericDataAccess(IDbConnectionFactory connectionFactory, DataAccessOptions options)
        : this(
            connectionFactory,
            options?.JsonOptions,
            options?.Databases.FirstOrDefault(
                database => string.Equals(database.Name, "Default", StringComparison.OrdinalIgnoreCase))?.CommandTimeout
                ?? options?.Databases.FirstOrDefault()?.CommandTimeout)
    {
    }

    private GenericDataAccess(
        IDbConnectionFactory connectionFactory,
        JsonSerializerOptions? jsonSerializerOptions,
        int? commandTimeout)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _jsonSerializerOptions = jsonSerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
        _commandTimeout = commandTimeout;
    }

    public async Task<JsonDocument> QueryJsonAsync(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var result = await connection.QueryAsync(
            new CommandDefinition(
                sql,
                parameters,
                commandTimeout: _commandTimeout,
                commandType: commandType,
                cancellationToken: cancellationToken));

        return JsonSerializer.SerializeToDocument(result, _jsonSerializerOptions);
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QueryAsync<T>(
            new CommandDefinition(
                sql,
                parameters,
                commandTimeout: _commandTimeout,
                commandType: commandType,
                cancellationToken: cancellationToken));
    }

    public async Task<T?> QuerySingleAsync<T>(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<T>(
            new CommandDefinition(
                sql,
                parameters,
                commandTimeout: _commandTimeout,
                commandType: commandType,
                cancellationToken: cancellationToken));
    }

    public async Task<int> ExecuteAsync(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                parameters,
                commandTimeout: _commandTimeout,
                commandType: commandType,
                cancellationToken: cancellationToken));
    }

    public async Task<T?> ExecuteAsync<T>(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.ExecuteScalarAsync<T>(
            new CommandDefinition(
                sql,
                parameters,
                commandTimeout: _commandTimeout,
                commandType: commandType,
                cancellationToken: cancellationToken));
    }

    public Task<JsonDocument> ExecuteStoredProcedureJsonAsync(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        return QueryJsonAsync(procedureName, parameters, CommandType.StoredProcedure, cancellationToken);
    }

    public Task<IEnumerable<T>> ExecuteStoredProcedureAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        return QueryAsync<T>(procedureName, parameters, CommandType.StoredProcedure, cancellationToken);
    }
}
