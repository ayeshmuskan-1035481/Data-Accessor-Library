using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using DataAccessor.Library.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DataAccessor.Library.Http
{
    public class HttpDataAccess : IGenericDataAccess
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public HttpDataAccess(HttpClient httpClient)
            : this(httpClient, jsonSerializerOptions: null)
        {
        }

        [ActivatorUtilitiesConstructor]
        public HttpDataAccess(HttpClient httpClient, DataAccessOptions options)
            : this(httpClient, options?.JsonOptions)
        {
        }

        public HttpDataAccess(HttpClient httpClient, JsonSerializerOptions? jsonSerializerOptions)
        {
            _httpClient = httpClient;
            _jsonSerializerOptions = jsonSerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
        }

        public async Task<JsonDocument> QueryJsonAsync(string url, object? parameters = null, System.Data.CommandType commandType = System.Data.CommandType.Text, CancellationToken cancellationToken = default)
        {
            var requestOptions = CreateRequestOptions(parameters, HttpMethod.Get);
            using var request = BuildRequestMessage(url, requestOptions, treatBodyAsQueryString: true);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream);
        }

        public async Task<IEnumerable<T>> QueryAsync<T>(string url, object? parameters = null, System.Data.CommandType commandType = System.Data.CommandType.Text, CancellationToken cancellationToken = default)
        {
            var jsonDoc = await QueryJsonAsync(url, parameters, commandType, cancellationToken);
            if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var result = JsonSerializer.Deserialize<IEnumerable<T>>(jsonDoc.RootElement.GetRawText(), _jsonSerializerOptions);
                return result ?? new List<T>();
            }
            else if (jsonDoc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var single = JsonSerializer.Deserialize<T>(jsonDoc.RootElement.GetRawText(), _jsonSerializerOptions);
                return single != null ? new List<T> { single } : new List<T>();
            }
            else
            {
                return new List<T>();
            }
        }

        public async Task<T?> QuerySingleAsync<T>(string url, object? parameters = null, System.Data.CommandType commandType = System.Data.CommandType.Text, CancellationToken cancellationToken = default)
        {
            var jsonDoc = await QueryJsonAsync(url, parameters, commandType, cancellationToken);
            if (jsonDoc.RootElement.ValueKind == JsonValueKind.Object)
            {
                return JsonSerializer.Deserialize<T>(jsonDoc.RootElement.GetRawText(), _jsonSerializerOptions);
            }
            else if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array && jsonDoc.RootElement.GetArrayLength() > 0)
            {
                return JsonSerializer.Deserialize<T>(jsonDoc.RootElement[0].GetRawText(), _jsonSerializerOptions);
            }
            else
            {
                return default;
            }
        }

        public async Task<int> ExecuteAsync(string url, object? parameters = null, System.Data.CommandType commandType = System.Data.CommandType.Text, CancellationToken cancellationToken = default)
        {
            var requestOptions = CreateRequestOptions(parameters, HttpMethod.Post);
            using var request = BuildRequestMessage(url, requestOptions, treatBodyAsQueryString: false);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return (int)response.StatusCode;
        }

        public async Task<T?> ExecuteAsync<T>(string url, object? parameters = null, System.Data.CommandType commandType = System.Data.CommandType.Text, CancellationToken cancellationToken = default)
        {
            var requestOptions = CreateRequestOptions(parameters, HttpMethod.Post);
            using var request = BuildRequestMessage(url, requestOptions, treatBodyAsQueryString: false);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var jsonDoc = await JsonDocument.ParseAsync(stream);
            return JsonSerializer.Deserialize<T>(jsonDoc.RootElement.GetRawText(), _jsonSerializerOptions);
        }

        public Task<JsonDocument> ExecuteStoredProcedureJsonAsync(string procedureName, object? parameters = null, CancellationToken cancellationToken = default)
        {
            return QueryJsonAsync(procedureName, parameters, System.Data.CommandType.StoredProcedure, cancellationToken);
        }

        public Task<IEnumerable<T>> ExecuteStoredProcedureAsync<T>(string procedureName, object? parameters = null, CancellationToken cancellationToken = default)
        {
            return QueryAsync<T>(procedureName, parameters, System.Data.CommandType.StoredProcedure, cancellationToken);
        }

        private HttpRequestOptions CreateRequestOptions(object? parameters, HttpMethod defaultMethod)
        {
            if (parameters is HttpRequestOptions requestOptions)
            {
                if (requestOptions.Method == default)
                {
                    requestOptions.Method = defaultMethod;
                }

                return requestOptions;
            }

            var request = new HttpRequestOptions
            {
                Method = defaultMethod
            };

            if (defaultMethod == HttpMethod.Get)
            {
                foreach (var (key, value) in ToKeyValuePairs(parameters))
                {
                    request.QueryParameters[key] = value;
                }
            }
            else
            {
                request.Body = parameters;
            }

            return request;
        }

        private HttpRequestMessage BuildRequestMessage(string url, HttpRequestOptions requestOptions, bool treatBodyAsQueryString)
        {
            var requestUri = AppendQueryString(url, requestOptions.QueryParameters, treatBodyAsQueryString ? requestOptions.Body : null);
            var request = new HttpRequestMessage(requestOptions.Method, requestUri);

            foreach (var header in requestOptions.Headers)
            {
                request.Headers.Remove(header.Key);
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (!treatBodyAsQueryString && requestOptions.Body != null && MethodSupportsBody(requestOptions.Method))
            {
                request.Content = new StringContent(JsonSerializer.Serialize(requestOptions.Body, _jsonSerializerOptions), Encoding.UTF8, "application/json");
            }

            return request;
        }

        private static bool MethodSupportsBody(HttpMethod method)
        {
            return method == HttpMethod.Post || method == HttpMethod.Put || method.Method.Equals("PATCH", StringComparison.OrdinalIgnoreCase) || method.Method.Equals("DELETE", StringComparison.OrdinalIgnoreCase);
        }

        private static string AppendQueryString(string url, IDictionary<string, object?> queryParameters, object? fallbackQuerySource)
        {
            var values = queryParameters.Count > 0
                ? queryParameters.Select(pair => KeyValuePair.Create(pair.Key, pair.Value))
                : ToKeyValuePairs(fallbackQuerySource);

            var encoded = values
                .Where(pair => pair.Value != null)
                .SelectMany(pair => ExpandQueryValue(pair.Key, pair.Value))
                .ToList();

            if (encoded.Count == 0)
            {
                return url;
            }

            var separator = url.Contains('?') ? '&' : '?';
            return url + separator + string.Join("&", encoded);
        }

        private static IEnumerable<string> ExpandQueryValue(string key, object? value)
        {
            if (value is null)
            {
                return Array.Empty<string>();
            }

            if (value is string)
            {
                return new[] { $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value.ToString()!)}" };
            }

            if (value is System.Collections.IEnumerable sequence)
            {
                var entries = new List<string>();
                foreach (var item in sequence)
                {
                    if (item != null)
                    {
                        entries.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(ConvertToInvariantString(item))}");
                    }
                }

                return entries;
            }

            return new[] { $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(ConvertToInvariantString(value))}" };
        }

        private static IEnumerable<KeyValuePair<string, object?>> ToKeyValuePairs(object? source)
        {
            if (source == null)
            {
                return Array.Empty<KeyValuePair<string, object?>>();
            }

            if (source is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                return pairs;
            }

            return source
                .GetType()
                .GetProperties()
                .Where(property => property.CanRead)
                .Select(property => KeyValuePair.Create(property.Name, property.GetValue(source)));
        }

        private static string ConvertToInvariantString(object value)
        {
            return value switch
            {
                DateTime dateTime => dateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                bool boolean => boolean ? "true" : "false",
                _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
            };
        }
    }
}
