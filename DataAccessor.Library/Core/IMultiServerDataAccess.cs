using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccessor.Library.Core;

public interface IMultiServerDataAccess
{
    Task<JsonDocument> QueryJsonAsync(string connectionName, string sql, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> QueryAsync<T>(string connectionName, string sql, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default);
    Task<T?> QuerySingleAsync<T>(string connectionName, string sql, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default);
    Task<int> ExecuteAsync(string connectionName, string sql, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default);
    Task<JsonDocument> QueryMultipleAsync(IReadOnlyDictionary<string, string> queries, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default);
    Task<JsonDocument> ExecuteMultipleAsync(IReadOnlyDictionary<string, string> commands, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> QueryAcrossConnectionsAsync<T>(IEnumerable<string> connectionNames, string sql, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default);
    Task<DistributedTransactionResult> ExecuteDistributedTransactionAsync(Dictionary<string, IEnumerable<(string command, object? parameters)>> commandsByServer, CancellationToken cancellationToken = default);
}
