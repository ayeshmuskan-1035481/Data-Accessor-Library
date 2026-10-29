using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccessor.Library.Core;

public interface IUnitOfWork : IDisposable
{
    IDbTransaction? Transaction { get; }

    Task BeginTransactionAsync();

    Task CommitAsync();

    Task RollbackAsync();

    Task<JsonDocument> QueryJsonAsync(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<T>> QueryAsync<T>(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default);

    Task<T?> QuerySingleAsync<T>(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default);

    Task<int> ExecuteAsync(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken cancellationToken = default);
}
