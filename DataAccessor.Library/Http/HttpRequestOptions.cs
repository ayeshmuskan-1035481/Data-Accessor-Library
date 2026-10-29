using System;
using System.Collections.Generic;
using System.Net.Http;

namespace DataAccessor.Library.Http;

public class HttpRequestOptions
{
    public HttpMethod Method { get; set; } = HttpMethod.Get;

    public object? Body { get; set; }

    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IDictionary<string, object?> QueryParameters { get; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
