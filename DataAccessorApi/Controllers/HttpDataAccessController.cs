using DataAccessor.Library.Http;
using Microsoft.AspNetCore.Mvc;
using DataAccessor.Api.Helpers;
 
namespace DataAccessor.Api.Controllers;
 
/// <summary>
/// Predefined scenarios for <see cref="HttpDataAccess"/> using the base URL configured in
/// appsettings (<c>DataAccess:Http:BaseUrl</c>, defaults to https://jsonplaceholder.typicode.com).
/// Each endpoint mirrors one test case from <c>HttpDataAccessTests</c> — no request body required.
/// </summary>
[ApiController]
[Route("api/http")]
[Produces("application/json")]
[SwaggerVisible]
public class HttpDataAccessController : ControllerBase
{
    private readonly HttpDataAccess _dataAccess;
 
    public HttpDataAccessController(HttpDataAccess dataAccess) =>
        _dataAccess = dataAccess;
 
    /// <summary>
    /// [QueryJson] GET /mock/todos/1 → raw JSON document {userId, id, title, completed}.
    /// </summary>
    [HttpGet("query-json")]
    public async Task<IActionResult> QueryJson(CancellationToken ct)
    {
        using var doc = await _dataAccess.QueryJsonAsync("/mock/todos/1", cancellationToken: ct);
        return Content(doc.RootElement.GetRawText(), "application/json");
    }
 
    /// <summary>
    /// [QueryCollection] GET /mock/todos?_limit=3 → deserialised array of TodoItem.
    /// </summary>
    [HttpGet("query")]
    public async Task<IActionResult> Query(CancellationToken ct)
    {
        var results = await _dataAccess.QueryAsync<TodoItem>(
            "/mock/todos", new { _limit = 3 }, cancellationToken: ct);
        return Ok(results);
    }
 
    /// <summary>
    /// [QuerySingle] GET /mock/todos/1 → single deserialised TodoItem.
    /// </summary>
    [HttpGet("query-single")]
    public async Task<IActionResult> QuerySingle(CancellationToken ct)
    {
        var result = await _dataAccess.QuerySingleAsync<TodoItem>("/mock/todos/1", cancellationToken: ct);
        return result is null ? NoContent() : Ok(result);
    }
 
    /// <summary>
    /// [Execute/Post] POST /mock/posts with a sample body → HTTP status code of the upstream response.
    /// </summary>
    [HttpPost("execute")]
    public async Task<IActionResult> Execute(CancellationToken ct)
    {
        var options = new DataAccessor.Library.Http.HttpRequestOptions
        {
            Method = HttpMethod.Post,
            Body   = new { title = "API Test Post", body = "Hello from DataAccessor.Api", userId = 1 }
        };
        var statusCode = await _dataAccess.ExecuteAsync("/mock/posts", options, cancellationToken: ct);
        return Ok(new { httpStatusCode = statusCode });
    }
 
    // ── Nested DTO ────────────────────────────────────────────────────────────
 
    public record TodoItem(int UserId, int Id, string Title, bool Completed);
}
 
 
 