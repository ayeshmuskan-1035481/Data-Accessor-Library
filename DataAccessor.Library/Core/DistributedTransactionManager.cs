using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccessor.Library.Core;

public class DistributedTransactionResult
{
    public bool Success { get; set; }

    public List<Exception> Errors { get; set; } = new();
}

public class DistributedTransactionManager
{
    private readonly IMultiServerConnectionFactory _connectionFactory;
    private readonly IReadOnlyDictionary<string, int> _commandTimeouts;

    public DistributedTransactionManager(IMultiServerConnectionFactory connectionFactory)
        : this(connectionFactory, options: null)
    {
    }

    public DistributedTransactionManager(
        IMultiServerConnectionFactory connectionFactory,
        DataAccessOptions? options)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _commandTimeouts = options?.Databases.ToDictionary(
            database => database.Name,
            database => database.CommandTimeout,
            StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<DistributedTransactionResult> ExecuteDistributedTransactionAsync(
        Dictionary<string, IEnumerable<(string command, object? parameters)>> commandsByServer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commandsByServer);

        var connections = new Dictionary<string, DbConnection>();
        var transactions = new Dictionary<string, DbTransaction>();
        var result = new DistributedTransactionResult();

        try
        {
            foreach (var server in commandsByServer.Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var connection = await _connectionFactory.CreateConnectionAsync(server);
                connections[server] = connection;
                transactions[server] = await connection.BeginTransactionAsync(cancellationToken);
            }

            foreach (var (server, commands) in commandsByServer)
            {
                var connection = connections[server];
                var transaction = transactions[server];

                foreach (var (commandText, parameters) in commands)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var command = connection.CreateCommand();
                    command.CommandText = commandText;
                    command.Transaction = transaction;

                    if (_commandTimeouts.TryGetValue(server, out var commandTimeout))
                    {
                        command.CommandTimeout = commandTimeout;
                    }

                    if (parameters is not null)
                    {
                        foreach (var property in parameters.GetType().GetProperties())
                        {
                            var parameter = command.CreateParameter();
                            parameter.ParameterName = "@" + property.Name;
                            parameter.Value = property.GetValue(parameters) ?? DBNull.Value;
                            command.Parameters.Add(parameter);
                        }
                    }

                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            foreach (var transaction in transactions.Values)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            result.Success = true;
        }
        catch (Exception exception)
        {
            result.Errors.Add(exception);

            foreach (var transaction in transactions.Values)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch (Exception rollbackException)
                {
                    result.Errors.Add(rollbackException);
                }
            }
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

        return result;
    }
}
