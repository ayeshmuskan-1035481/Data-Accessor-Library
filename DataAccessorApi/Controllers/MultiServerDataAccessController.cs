using DataAccessor.Api.Helpers;
using DataAccessor.Library.Core;
using Microsoft.AspNetCore.Mvc;
 
namespace DataAccessor.Api.Controllers;
 
/// <summary>
/// Predefined scenarios for <see cref="IMultiServerDataAccess"/> using named connections
/// from appsettings (Example1, Example2).
/// Each endpoint mirrors one test case from <c>MultiServerDataAccessTests</c> and
/// <c>MultiDatabaseDataAccessTests</c> — no request body required.
/// </summary>
[ApiController]
[Route("api/multi-server")]
[Produces("application/json")]
[SwaggerVisible]
public class MultiServerDataAccessController : ControllerBase
{
    private readonly IMultiServerDataAccess _dataAccess;
 
    public MultiServerDataAccessController(IMultiServerDataAccess dataAccess) =>
        _dataAccess = dataAccess;
 
    // ── MultiServerDataAccessTests scenarios ──────────────────────────────────
 
    /// <summary>
    /// [QueryJson/Example1] SELECT 'Test' AS TestValue on Example1 → JSON array.
    /// </summary>
    [HttpGet("query-json/example1")]
    public async Task<IActionResult> QueryJsonExample1(CancellationToken ct)
    {
        using var result = await _dataAccess.QueryJsonAsync("Example1", "SELECT 'Test' AS TestValue", cancellationToken: ct);
        return Content(result.RootElement.GetRawText(), "application/json");
    }
 
    /// <summary>
    /// [QueryJson/Example2] SELECT 'Test' AS TestValue on Example2 → JSON array.
    /// </summary>
    [HttpGet("query-json/example2")]
    public async Task<IActionResult> QueryJsonExample2(CancellationToken ct)
    {
        using var result = await _dataAccess.QueryJsonAsync("Example2", "SELECT 'Test' AS TestValue", cancellationToken: ct);
        return Content(result.RootElement.GetRawText(), "application/json");
    }
 
    /// <summary>
    /// [QueryTyped/Example1] SELECT typed row {Id, Value} from Example1.
    /// </summary>
    [HttpGet("query-typed/example1")]
    public async Task<IActionResult> QueryTypedExample1(CancellationToken ct)
    {
        var results = await _dataAccess.QueryAsync<ServerRow>("Example1", "SELECT 1 AS Id, 'Test' AS Value", cancellationToken: ct);
        return Ok(results);
    }
 
    /// <summary>
    /// [QueryTyped/Example2] SELECT typed row {Id, Value} from Example2.
    /// </summary>
    [HttpGet("query-typed/example2")]
    public async Task<IActionResult> QueryTypedExample2(CancellationToken ct)
    {
        var results = await _dataAccess.QueryAsync<ServerRow>("Example2", "SELECT 1 AS Id, 'Test' AS Value", cancellationToken: ct);
        return Ok(results);
    }
 
    /// <summary>
    /// [QuerySingle/Example1] SELECT single typed row {Id, Value} from Example1.
    /// </summary>
    [HttpGet("query-single/example1")]
    public async Task<IActionResult> QuerySingleExample1(CancellationToken ct)
    {
        var result = await _dataAccess.QuerySingleAsync<ServerRow>("Example1", "SELECT 1 AS Id, 'Test' AS Value", cancellationToken: ct);
        return result is null ? NoContent() : Ok(result);
    }
 
    /// <summary>
    /// [QuerySingle/Example2] SELECT single typed row {Id, Value} from Example2.
    /// </summary>
    [HttpGet("query-single/example2")]
    public async Task<IActionResult> QuerySingleExample2(CancellationToken ct)
    {
        var result = await _dataAccess.QuerySingleAsync<ServerRow>("Example2", "SELECT 1 AS Id, 'Test' AS Value", cancellationToken: ct);
        return result is null ? NoContent() : Ok(result);
    }
 
    /// <summary>
    /// [InvalidServer] Query a non-existent connection name → 400 with ArgumentException message.
    /// </summary>
    [HttpGet("invalid-server-error")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InvalidServerError(CancellationToken ct)
    {
        try
        {
            await _dataAccess.QueryAsync<ServerRow>("NonExistentServer", "SELECT 1 AS Id", cancellationToken: ct);
            return Ok(new { message = "Unexpected success — server should not exist." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { expectedError = true, message = ex.Message });
        }
    }
 
    // ── MultiDatabaseDataAccessTests scenarios ────────────────────────────────
 
    /// <summary>
    /// [CombineResults] Query Example1 and Example2 separately then combine → 2 rows.
    /// </summary>
    [HttpGet("combine-results")]
    public async Task<IActionResult> CombineResults(CancellationToken ct)
    {
        var result1 = await _dataAccess.QueryAsync<ServerRow>("Example1", "SELECT 1 AS Id, 'FromExample1' AS Value", cancellationToken: ct);
        var result2 = await _dataAccess.QueryAsync<ServerRow>("Example2", "SELECT 2 AS Id, 'FromExample2' AS Value", cancellationToken: ct);
        var combined = result1.Concat(result2).ToList();
        return Ok(combined);
    }
 
    /// <summary>
    /// [QueryMultiple] Different query per connection in parallel → JSON object keyed by connection name.
    /// </summary>
    [HttpGet("query-multiple")]
    public async Task<IActionResult> QueryMultiple(CancellationToken ct)
    {
        var queries = new Dictionary<string, string>
        {
            ["Example1"] = "SELECT 1 AS Id, 'Orders' AS SourceName",
            ["Example2"] = "SELECT 2 AS Id, 'Products' AS SourceName"
        };
        using var result = await _dataAccess.QueryMultipleAsync(queries, cancellationToken: ct);
        return Content(result.RootElement.GetRawText(), "application/json");
    }
 
    /// <summary>
    /// [ExecuteMultiple] Create temp table and insert one row per connection → affected-rows per connection name.
    /// </summary>
    [HttpGet("execute-multiple")]
    public async Task<IActionResult> ExecuteMultiple(CancellationToken ct)
    {
        var commands = new Dictionary<string, string>
        {
            ["Example1"] = "CREATE TABLE #TempEx1 (Id INT); INSERT INTO #TempEx1 (Id) VALUES (1);",
            ["Example2"] = "CREATE TABLE #TempEx2 (Id INT); INSERT INTO #TempEx2 (Id) VALUES (1);"
        };
        using var result = await _dataAccess.ExecuteMultipleAsync(commands, cancellationToken: ct);
        return Content(result.RootElement.GetRawText(), "application/json");
    }
 
    /// <summary>
    /// [QueryAcross] Same SQL across Example1 and Example2 → combined flat array with DB names.
    /// </summary>
    [HttpGet("query-across")]
    public async Task<IActionResult> QueryAcross(CancellationToken ct)
    {
        var results = await _dataAccess.QueryAcrossConnectionsAsync<DatabaseNameRow>(
            new[] { "Example1", "Example2" },
            "SELECT DB_NAME() AS DatabaseName",
            cancellationToken: ct);
        return Ok(results);
    }
 
    // ── Nested DTOs ───────────────────────────────────────────────────────────
 
    public record ServerRow(int Id, string Value);
    public record DatabaseNameRow(string DatabaseName);
}
 
 
 