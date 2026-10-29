using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccessor.Library.Core;

public interface IGenericDataAccess
{
    Task<JsonDocument> QueryJsonAsync(
        string sql,
        object? parameters = null,
        System.Data.CommandType commandType = System.Data.CommandType.Text,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<T>> QueryAsync<T>(
        string sql,
        object? parameters = null,
        System.Data.CommandType commandType = System.Data.CommandType.Text,
        CancellationToken cancellationToken = default);

    Task<T?> QuerySingleAsync<T>(
        string sql,
        object? parameters = null,
        System.Data.CommandType commandType = System.Data.CommandType.Text,
        CancellationToken cancellationToken = default);

    Task<int> ExecuteAsync(
        string sql,
        object? parameters = null,
        System.Data.CommandType commandType = System.Data.CommandType.Text,
        CancellationToken cancellationToken = default);

    Task<T?> ExecuteAsync<T>(
        string sql,
        object? parameters = null,
        System.Data.CommandType commandType = System.Data.CommandType.Text,
        CancellationToken cancellationToken = default);

    Task<JsonDocument> ExecuteStoredProcedureJsonAsync(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<T>> ExecuteStoredProcedureAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default);
}
