using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace DataAccessor.Library.Core;

public class UnitOfWork : IUnitOfWork, IAsyncDisposable
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly int? _commandTimeout;
    private DbConnection? _connection;
    private IDbTransaction? _transaction;
    private bool _disposed;

    public IDbTransaction? Transaction => _transaction;

    public UnitOfWork(IDbConnectionFactory connectionFactory)
        : this(connectionFactory, options: null)
    {
    }

    public UnitOfWork(IDbConnectionFactory connectionFactory, DataAccessOptions? options)
    {
        _connectionFactory = connectionFactory;
        _jsonSerializerOptions = options?.JsonOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
        _commandTimeout = options?.Databases.FirstOrDefault(database => string.Equals(database.Name, "Default", StringComparison.OrdinalIgnoreCase))?.CommandTimeout
            ?? options?.Databases.FirstOrDefault()?.CommandTimeout;
    }

    public async Task BeginTransactionAsync()
    {
        _connection = await _connectionFactory.CreateConnectionAsync();
        _transaction = await _connectionFactory.BeginTransactionAsync(_connection);
    }

    public async Task CommitAsync()
    {
        try
        {
            if (_transaction != null)
            {
                _transaction.Commit();
                _transaction = null;
            }
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
        finally
        {
            if (_connection != null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
    }

    public async Task RollbackAsync()
    {
        try
        {
            if (_transaction != null)
            {
                _transaction.Rollback();
                _transaction = null;
            }
        }
        finally
        {
            if (_connection != null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
    }

    public async Task<JsonDocument> QueryJsonAsync(string sql, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default)
    {
        EnsureTransactionStarted();
        var result = await _connection!.QueryAsync(
            new CommandDefinition(
                sql,
                parameters,
                _transaction,
                commandTimeout: _commandTimeout,
                commandType: commandType,
                cancellationToken: cancellationToken
            ));
        return JsonSerializer.SerializeToDocument(result, _jsonSerializerOptions);
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default)
    {
        EnsureTransactionStarted();
        return await _connection!.QueryAsync<T>(
            new CommandDefinition(
                sql,
                parameters,
                _transaction,
                commandTimeout: _commandTimeout,
                commandType: commandType,
                cancellationToken: cancellationToken
            ));
    }

    public async Task<T?> QuerySingleAsync<T>(string sql, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default)
    {
        EnsureTransactionStarted();
        return await _connection!.QuerySingleOrDefaultAsync<T>(
            new CommandDefinition(
                sql,
                parameters,
                _transaction,
                commandTimeout: _commandTimeout,
                commandType: commandType,
                cancellationToken: cancellationToken
            ));
    }

    public async Task<int> ExecuteAsync(string sql, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default)
    {
        EnsureTransactionStarted();
        return await _connection!.ExecuteAsync(
            new CommandDefinition(
                sql,
                parameters,
                _transaction,
                commandTimeout: _commandTimeout,
                commandType: commandType,
                cancellationToken: cancellationToken
            ));
    }

    private void EnsureTransactionStarted()
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("Transaction has not been started. Call BeginTransactionAsync first.");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (_transaction != null)
                {
                    _transaction.Dispose();
                    _transaction = null;
                }
                if (_connection != null)
                {
                    _connection.Dispose();
                    _connection = null;
                }
            }
            _disposed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_transaction != null)
            {
                _transaction.Dispose();
                _transaction = null;
            }
            if (_connection != null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
