using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DataAccessor.Library.Core;
using DataAccessor.Library.Factories;

namespace DataAccessor.Library;

public class CrossServerDataAccess : IDisposable
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public CrossServerDataAccess()
        : this(jsonSerializerOptions: null)
    {
    }

    public CrossServerDataAccess(DataAccessOptions options)
        : this(options?.JsonOptions)
    {
    }

    public CrossServerDataAccess(JsonSerializerOptions? jsonSerializerOptions)
    {
        _jsonSerializerOptions = jsonSerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    public async Task<JsonDocument> QueryMultipleServersAsync(
        IReadOnlyDictionary<string, (string query, DatabaseConnectionOptions connection, object? parameters)> queries,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queries);

        var results = await Task.WhenAll(queries.Select(async query =>
        {
            using var connection = await DatabaseConnectionFactory.CreateConnectionAsync(query.Value.connection);
            var rows = await connection.QueryAsync(
                new CommandDefinition(
                    query.Value.query,
                    query.Value.parameters,
                    commandTimeout: query.Value.connection.CommandTimeout,
                    commandType: commandType,
                    cancellationToken: cancellationToken));

            return new KeyValuePair<string, JsonElement>(
                query.Key,
                JsonSerializer.SerializeToElement(rows, _jsonSerializerOptions));
        }));

        return JsonSerializer.SerializeToDocument(
            results.ToDictionary(item => item.Key, item => item.Value),
            _jsonSerializerOptions);
    }

    public async Task<IEnumerable<T>> QueryAcrossServersAsync<T>(
        string sql,
        IReadOnlyDictionary<string, DatabaseConnectionOptions> connections,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connections);

        var results = await Task.WhenAll(connections.Values.Select(
            connectionOptions => QuerySingleServerAsync<T>(
                connectionOptions,
                sql,
                parameters,
                commandType,
                cancellationToken)));
        return results.SelectMany(result => result).ToList();
    }

    public async Task<DistributedTransactionResult> ExecuteDistributedTransactionAcrossServersAsync(
        Dictionary<string, (DatabaseConnectionOptions connection, IEnumerable<(string command, object? parameters)> commands)> commandsByServer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commandsByServer);

        var result = new DistributedTransactionResult();
        var connections = new Dictionary<string, DbConnection>(StringComparer.OrdinalIgnoreCase);
        var transactions = new Dictionary<string, DbTransaction>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var (serverName, config) in commandsByServer)
            {
                var connection = await DatabaseConnectionFactory.CreateConnectionAsync(config.connection);
                var transaction = await connection.BeginTransactionAsync();
                connections[serverName] = connection;
                transactions[serverName] = transaction;
            }

            foreach (var (serverName, config) in commandsByServer)
            {
                var connection = connections[serverName];
                var transaction = transactions[serverName];

                foreach (var (command, parameters) in config.commands)
                {
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            command,
                            parameters,
                            transaction,
                            commandTimeout: config.connection.CommandTimeout,
                            commandType: CommandType.Text,
                            cancellationToken: cancellationToken));
                }
            }

            foreach (var transaction in transactions.Values)
            {
                await transaction.CommitAsync();
            }

            result.Success = true;
            return result;
        }
        catch (Exception exception)
        {
            result.Errors.Add(exception);
            result.Success = false;

            foreach (var transaction in transactions.Values)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                }
            }

            return result;
        }
        finally
        {
            foreach (var transaction in transactions.Values)
            {
                await transaction.DisposeAsync();
            }

            foreach (var connection in connections.Values)
            {
                await connection.DisposeAsync();
            }
        }
    }

    public void Dispose()
    {
    }

    private static async Task<IEnumerable<T>> QuerySingleServerAsync<T>(
        DatabaseConnectionOptions connectionOptions,
        string sql,
        object? parameters,
        CommandType commandType,
        CancellationToken cancellationToken)
    {
        using var connection = await DatabaseConnectionFactory.CreateConnectionAsync(connectionOptions);
        return await connection.QueryAsync<T>(
            new CommandDefinition(
                sql,
                parameters,
                commandTimeout: connectionOptions.CommandTimeout,
                commandType: commandType,
                cancellationToken: cancellationToken));
    }
}