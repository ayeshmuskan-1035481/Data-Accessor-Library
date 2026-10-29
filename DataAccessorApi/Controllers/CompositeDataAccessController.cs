using DataAccessor.Api.Helpers;
using DataAccessor.Library;
using DataAccessor.Library.Core;
using DataAccessor.Library.Http;
using Microsoft.AspNetCore.Mvc;
 
namespace DataAccessor.Api.Controllers;
 
/// <summary>
/// Predefined scenarios for <see cref="CompositeDataAccess"/>, which automatically routes
/// a query to either SQL or HTTP depending on whether it looks like a URL path.
/// Each endpoint mirrors one test case from <c>CompositeDataAccessTests</c> and the
/// composite path in <c>CrossServerDataAccessTests</c> — no request body required.
/// </summary>
[ApiController]
[Route("api/composite")]
[Produces("application/json")]
[SwaggerVisible]
public class CompositeDataAccessController : ControllerBase
{
    private readonly CompositeDataAccess _composite;
    private readonly DataAccessOptions _options;
 
    public CompositeDataAccessController(CompositeDataAccess composite, DataAccessOptions options)
    {
        _composite = composite;
        _options = options;
    }
 
    /// <summary>
    /// [RoutesToSql] Query string starts with SELECT → routed to the Default SQL connection.
    /// Returns typed rows {Id, Value}.
    /// </summary>
    [HttpGet("query-via-sql")]
    public async Task<IActionResult> QueryViaSql(CancellationToken ct)
    {
        var results = await _composite.QueryAsync<SqlRow>(
            "SELECT @Id AS Id, @Value AS Value",
            new { Id = 1, Value = "Test" },
            cancellationToken: ct);
        return Ok(results);
    }
 
    /// <summary>
    /// [RoutesToHttp] Query string starts with "/" → routed to HttpDataAccess.
    /// Calls /mock/todos/1 on the local built-in mock server.
    /// </summary>
    [HttpGet("query-via-http")]
    public async Task<IActionResult> QueryViaHttp(CancellationToken ct)
    {
        var results = await _composite.QueryAsync<TodoItem>("/mock/todos/1", cancellationToken: ct);
        return Ok(results);
    }
 
    /// <summary>
    /// [QueryAcrossServers] Delegates to the inner CrossServerDataAccess to run
    /// DB_NAME() on Example1 and Example2 → combined flat array with DatabaseName per row.
    /// </summary>
    [HttpGet("query-across-servers")]
    public async Task<IActionResult> QueryAcrossServers(CancellationToken ct)
    {
        var connections = new Dictionary<string, DatabaseConnectionOptions>
        {
            ["Orders"]   = GetConnection("Example1"),
            ["Products"] = GetConnection("Example2")
        };
 
        var results = await _composite.QueryAcrossServersAsync<DatabaseNameRow>(
            "SELECT DB_NAME() AS DatabaseName", connections, cancellationToken: ct);
        return Ok(results);
    }
 
    // ── Helpers & nested DTOs ─────────────────────────────────────────────────
 
    private DatabaseConnectionOptions GetConnection(string name) =>
        _options.Databases.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Database '{name}' not found in configuration.");
 
    public record SqlRow(int Id, string Value);
    public record TodoItem(int UserId, int Id, string Title, bool Completed);
    public record DatabaseNameRow(string DatabaseName);
}
 
 