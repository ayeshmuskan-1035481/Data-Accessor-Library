using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataAccessor.Library.Core;
using DataAccessor.Library.Http;

namespace DataAccessor.Library
{
    /// <summary>
    /// CompositeDataAccess automatically routes queries to HTTP or SQL based on the query string.
    /// </summary>
    public class CompositeDataAccess : IGenericDataAccess
    {
        private readonly HttpDataAccess _httpDataAccess;
        private readonly GenericDataAccess _sqlDataAccess;
        private readonly CrossServerDataAccess _crossServerDataAccess;

        public CompositeDataAccess(HttpDataAccess httpDataAccess, GenericDataAccess sqlDataAccess)
            : this(httpDataAccess, sqlDataAccess, new CrossServerDataAccess())
        {
        }

        public CompositeDataAccess(HttpDataAccess httpDataAccess, GenericDataAccess sqlDataAccess, CrossServerDataAccess crossServerDataAccess)
        {
            _httpDataAccess = httpDataAccess;
            _sqlDataAccess = sqlDataAccess;
            _crossServerDataAccess = crossServerDataAccess;
        }

        private bool IsHttpQuery(string query)
        {
            // Simple heuristic: treat as HTTP if it starts with '/' or 'http'
            return query.StartsWith("/") || query.StartsWith("http", StringComparison.OrdinalIgnoreCase);
        }

        public Task<JsonDocument> QueryJsonAsync(string query, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default)
        {
            if (IsHttpQuery(query))
                return _httpDataAccess.QueryJsonAsync(query, parameters, commandType, cancellationToken);
            return _sqlDataAccess.QueryJsonAsync(query, parameters, commandType, cancellationToken);
        }

        public Task<IEnumerable<T>> QueryAsync<T>(string query, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default)
        {
            if (IsHttpQuery(query))
                return _httpDataAccess.QueryAsync<T>(query, parameters, commandType, cancellationToken);
            return _sqlDataAccess.QueryAsync<T>(query, parameters, commandType, cancellationToken);
        }

        public Task<T?> QuerySingleAsync<T>(string query, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default)
        {
            if (IsHttpQuery(query))
                return _httpDataAccess.QuerySingleAsync<T>(query, parameters, commandType, cancellationToken);
            return _sqlDataAccess.QuerySingleAsync<T>(query, parameters, commandType, cancellationToken);
        }

        public Task<int> ExecuteAsync(string query, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default)
        {
            if (IsHttpQuery(query))
                return _httpDataAccess.ExecuteAsync(query, parameters, commandType, cancellationToken);
            return _sqlDataAccess.ExecuteAsync(query, parameters, commandType, cancellationToken);
        }

        public Task<T?> ExecuteAsync<T>(string query, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default)
        {
            if (IsHttpQuery(query))
                return _httpDataAccess.ExecuteAsync<T>(query, parameters, commandType, cancellationToken);
            return _sqlDataAccess.ExecuteAsync<T>(query, parameters, commandType, cancellationToken);
        }

        public Task<JsonDocument> ExecuteStoredProcedureJsonAsync(string procedureName, object? parameters = null, CancellationToken cancellationToken = default)
        {
            return _sqlDataAccess.ExecuteStoredProcedureJsonAsync(procedureName, parameters, cancellationToken);
        }

        public Task<IEnumerable<T>> ExecuteStoredProcedureAsync<T>(string procedureName, object? parameters = null, CancellationToken cancellationToken = default)
        {
            return _sqlDataAccess.ExecuteStoredProcedureAsync<T>(procedureName, parameters, cancellationToken);
        }

        public Task<IEnumerable<T>> QueryAcrossServersAsync<T>(string query, IReadOnlyDictionary<string, DatabaseConnectionOptions> connections, object? parameters = null, CommandType commandType = CommandType.Text, CancellationToken cancellationToken = default)
        {
            return _crossServerDataAccess.QueryAcrossServersAsync<T>(query, connections, parameters, commandType, cancellationToken);
        }
    }
}