using DataAccessor.Api.Helpers;
using DataAccessor.Library;
using DataAccessor.Library.Core;
using Microsoft.AspNetCore.Mvc;
 
namespace DataAccessor.Api.Controllers;
 
/// <summary>
/// Predefined scenarios for <see cref="IGenericDataAccess"/> (Default connection).
/// Each endpoint mirrors one test case from <c>GenericDataAccessTests</c> and
/// <c>StoredProcedureAndFactoryTests</c> — no request body required.
/// </summary>
[ApiController]
[Route("api/generic")]
[Produces("application/json")]
[SwaggerVisible]
public class GenericDataAccessController : ControllerBase
{
    private readonly IGenericDataAccess _dataAccess;
    private readonly DataAccessOptions _options;
 
    public GenericDataAccessController(IGenericDataAccess dataAccess, DataAccessOptions options)
    {
        _dataAccess = dataAccess;
        _options = options;
    }
 
    // ── GenericDataAccessTests scenarios ─────────────────────────────────────
 
    /// <summary>
    /// [QueryTyped] SELECT with parameters → returns typed rows {Id, Value}.
    /// </summary>
    [HttpGet("query-typed")]
    public async Task<IActionResult> QueryTyped(CancellationToken ct)
    {
        var results = await _dataAccess.QueryAsync<QueryResult>(
            "SELECT @Id AS Id, @Value AS Value",
            new { Id = 1, Value = "Test" },
            cancellationToken: ct);
        return Ok(results);
    }
 
    /// <summary>
    /// [QuerySingle] SELECT with parameters → returns single typed row {Id, Value}.
    /// </summary>
    [HttpGet("query-single")]
    public async Task<IActionResult> QuerySingle(CancellationToken ct)
    {
        var result = await _dataAccess.QuerySingleAsync<QueryResult>(
            "SELECT @Id AS Id, @Value AS Value",
            new { Id = 1, Value = "Test" },
            cancellationToken: ct);
        return result is null ? NoContent() : Ok(result);
    }
 
    /// <summary>
    /// [Execute] CREATE temp table + INSERT one row → returns {rowsAffected: 1}.
    /// </summary>
    [HttpGet("execute")]
    public async Task<IActionResult> Execute(CancellationToken ct)
    {
        var rowsAffected = await _dataAccess.ExecuteAsync(
            "CREATE TABLE #TempTable (Id INT); INSERT INTO #TempTable (Id) VALUES (@Id);",
            new { Id = 1 },
            cancellationToken: ct);
        return Ok(new { rowsAffected });
    }
 
    /// <summary>
    /// [QueryJson] SELECT order fields as a JSON document → returns raw JSON array.
    /// </summary>
    [HttpGet("query-json")]
    public async Task<IActionResult> QueryJson(CancellationToken ct)
    {
        using var doc = await _dataAccess.QueryJsonAsync(
            "SELECT @Id AS OrderId, @CustomerName AS CustomerName, @OrderDate AS OrderDate",
            new { Id = 100, CustomerName = "Test Customer", OrderDate = "2025-10-16" },
            cancellationToken: ct);
        return Content(doc.RootElement.GetRawText(), "application/json");
    }
 
    /// <summary>
    /// [QueryComplex] SELECT with CASE expression evaluating ItemCount → returns dynamic row.
    /// </summary>
    [HttpGet("query-complex")]
    public async Task<IActionResult> QueryComplex(CancellationToken ct)
    {
        var results = await _dataAccess.QueryAsync<dynamic>(
            @"SELECT @CustomerName as CustomerName,
                     @OrderDate    as OrderDate,
                     @ItemCount    as ItemCount,
                     CASE WHEN @ItemCount > 2 THEN 'Large Order' ELSE 'Small Order' END as OrderSize",
            new { CustomerName = "Test Customer ABC", OrderDate = "2025-10-16", ItemCount = 5 },
            cancellationToken: ct);
        return Ok(results);
    }
 
    /// <summary>
    /// [QuerySingleNull] WHERE 1=0 means no rows → returns 204 No Content.
    /// </summary>
    [HttpGet("query-single-null")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> QuerySingleNull(CancellationToken ct)
    {
        var result = await _dataAccess.QuerySingleAsync<QueryResult>(
            "SELECT @Id AS Id, @Value AS Value WHERE 1 = 0",
            new { Id = 999, Value = "NonExistent" },
            cancellationToken: ct);
        return result is null ? NoContent() : Ok(result);
    }
 
    /// <summary>
    /// [ExecuteScalar] SELECT scalar expression → returns {value: 42}.
    /// </summary>
    [HttpGet("execute-scalar")]
    public async Task<IActionResult> ExecuteScalar(CancellationToken ct)
    {
        var value = await _dataAccess.ExecuteAsync<int>(
            "SELECT CAST(42 AS INT)",
            cancellationToken: ct);
        return Ok(new { value });
    }
 
    // ── StoredProcedureAndFactoryTests scenarios ──────────────────────────────
 
    /// <summary>
    /// [StoredProcTyped] Execute sp_GetOrderSummary on Example1 → returns typed order summary rows.
    /// Requires test databases (run CreateTestDatabases.ps1 first).
    /// </summary>
    [HttpGet("stored-procedure/typed")]
    public async Task<IActionResult> StoredProcedureTyped(CancellationToken ct)
    {
        var da = CreateDataAccess("Example1");
        var results = await da.ExecuteStoredProcedureAsync<OrderSummaryDto>(
            "sp_GetOrderSummary", new { OrderId = 1 }, ct);
        return Ok(results);
    }
 
    /// <summary>
    /// [StoredProcJson] Execute sp_GetInventorySummary on Example2 → returns JSON array.
    /// Requires test databases (run CreateTestDatabases.ps1 first).
    /// </summary>
    [HttpGet("stored-procedure/json")]
    public async Task<IActionResult> StoredProcedureJson(CancellationToken ct)
    {
        var da = CreateDataAccess("Example2");
        using var doc = await da.ExecuteStoredProcedureJsonAsync(
            "sp_GetInventorySummary", new { ProductCategory = "Electronics" }, ct);
        return Content(doc.RootElement.GetRawText(), "application/json");
    }
 
    // ── Helpers & nested DTOs ─────────────────────────────────────────────────
 
    private GenericDataAccess CreateDataAccess(string dbName)
    {
        var conn = _options.Databases
            .FirstOrDefault(d => string.Equals(d.Name, dbName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Database '{dbName}' not found in configuration.");
        return new GenericDataAccess(new ConfigurableDbConnectionFactory(conn), _options);
    }
 
    public record QueryResult(int Id, string Value);
    public record OrderSummaryDto(int OrderId, string CustomerName, int TotalItems);
}
 
 