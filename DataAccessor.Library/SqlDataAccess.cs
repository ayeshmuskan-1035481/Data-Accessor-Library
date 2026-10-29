using System.Data;
using System.Data.Common;
using System.Text.Json;
using Dapper;
using DataAccessor.Library.Core;
 
namespace DataAccessor.Library;
 
public class SqlDataAccess : IGenericDataAccess, IDisposable, IAsyncDisposable
{
    private readonly DbConnection _connection;
    private readonly bool _leaveOpen;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly int? _commandTimeout;
    private bool _disposed;
 
    public SqlDataAccess(DbConnection connection, bool leaveOpen = false, JsonSerializerOptions? jsonSerializerOptions = null, int? commandTimeout = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _leaveOpen = leaveOpen;
        _jsonSerializerOptions = jsonSerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
        _commandTimeout = commandTimeout;
    }
 
    public async Task<JsonDocument> QueryJsonAsync(string sql, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync();
        var result = await _connection.QueryAsync(new CommandDefinition(sql, parameters, commandTimeout: _commandTimeout, commandType: commandType, cancellationToken: cancellationToken));
        return JsonSerializer.SerializeToDocument(result, _jsonSerializerOptions);
    }
 
    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync();
        return await _connection.QueryAsync<T>(new CommandDefinition(sql, parameters, commandTimeout: _commandTimeout, commandType: commandType, cancellationToken: cancellationToken));
    }
 
    public async Task<T?> QuerySingleAsync<T>(string sql, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync();
        return await _connection.QuerySingleOrDefaultAsync<T>(new CommandDefinition(sql, parameters, commandTimeout: _commandTimeout, commandType: commandType, cancellationToken: cancellationToken));
    }
 
    public async Task<int> ExecuteAsync(string sql, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync();
        return await _connection.ExecuteAsync(new CommandDefinition(sql, parameters, commandTimeout: _commandTimeout, commandType: commandType, cancellationToken: cancellationToken));
    }
 
    public async Task<T?> ExecuteAsync<T>(string sql, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync();
        return await _connection.ExecuteScalarAsync<T>(new CommandDefinition(sql, parameters, commandTimeout: _commandTimeout, commandType: commandType, cancellationToken: cancellationToken));
    }
 
    public Task<JsonDocument> ExecuteStoredProcedureJsonAsync(string procedureName, object? parameters = null, CancellationToken cancellationToken = default)
    {
        return QueryJsonAsync(procedureName, parameters, CommandType.StoredProcedure, cancellationToken);
    }
 
    public Task<IEnumerable<T>> ExecuteStoredProcedureAsync<T>(string procedureName, object? parameters = null, CancellationToken cancellationToken = default)
    {
        return QueryAsync<T>(procedureName, parameters, CommandType.StoredProcedure, cancellationToken);
    }
 
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
 
        if (!_leaveOpen)
        {
            _connection.Dispose();
        }
 
        _disposed = true;
        GC.SuppressFinalize(this);
    }
 
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
 
        if (!_leaveOpen)
        {
            await _connection.DisposeAsync();
        }
 
        _disposed = true;
        GC.SuppressFinalize(this);
    }
 
    private async Task EnsureOpenAsync()
    {
        if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync();
        }
    }
}
 