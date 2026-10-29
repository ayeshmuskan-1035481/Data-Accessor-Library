using DataAccessor.Api.Helpers;
using DataAccessor.Library;
using DataAccessor.Library.Core;
using Microsoft.AspNetCore.Mvc;

namespace DataAccessor.Api.Controllers;

/// <summary>
/// Predefined scenarios for <see cref="CrossServerDataAccess"/> where connection options
/// are resolved from appsettings (Example1 = "OrdersDb", Example2 = "ProductsDb").
/// Each endpoint mirrors one test case from <c>CrossServerDataAccessTests</c> — no request body required.
/// </summary>
[ApiController]
[Route("api/cross-server")]
[Produces("application/json")]
[SwaggerVisible]
public class CrossServerDataAccessController : ControllerBase
{
	private readonly CrossServerDataAccess _dataAccess;
	private readonly DataAccessOptions _options;

	public CrossServerDataAccessController(CrossServerDataAccess dataAccess, DataAccessOptions options)
	{
		_dataAccess = dataAccess;
		_options = options;
	}

	/// <summary>
	/// [QueryMultipleServers] Run DB_NAME() on Example1 (alias "OrdersDb") and Example2
	/// (alias "ProductsDb") in parallel → JSON object keyed by alias.
	/// </summary>
	[HttpGet("query-multiple-servers")]
	public async Task<IActionResult> QueryMultipleServers(CancellationToken ct)
	{
		var queries = new Dictionary<string, (string query, DatabaseConnectionOptions connection, object? parameters)>
		{
			["OrdersDb"] = ("SELECT DB_NAME() AS DatabaseName", GetConnection("Example1"), null),
			["ProductsDb"] = ("SELECT DB_NAME() AS DatabaseName", GetConnection("Example2"), null)
		};

		using var result = await _dataAccess.QueryMultipleServersAsync(queries, cancellationToken: ct);
		return Content(result.RootElement.GetRawText(), "application/json");
	}

	/// <summary>
	/// [QueryAcrossServers] Run the same SQL across Example1 and Example2 → combined flat array.
	/// </summary>
	[HttpGet("query-across-servers")]
	public async Task<IActionResult> QueryAcrossServers(CancellationToken ct)
	{
		var connections = new Dictionary<string, DatabaseConnectionOptions>
		{
			["Orders"] = GetConnection("Example1"),
			["Products"] = GetConnection("Example2")
		};

		var results = await _dataAccess.QueryAcrossServersAsync<DatabaseNameRow>(
			"SELECT DB_NAME() AS DatabaseName", connections, cancellationToken: ct);
		return Ok(results);
	}

	/// <summary>
	/// [DistributedTransaction/Commit] INSERT one Order row into Example1 and one Product row
	/// into Example2 inside a distributed transaction → {success: true}.
	/// Requires test databases with Orders and Products tables (run CreateTestDatabases.ps1 first).
	/// </summary>
	[HttpPost("distributed-transaction/commit")]
	public async Task<IActionResult> DistributedTransactionCommit(CancellationToken ct)
	{
		var commandsByServer = new Dictionary<string, (DatabaseConnectionOptions, IEnumerable<(string, object?)>)>
		{
			["Orders"] = (
				GetConnection("Example1"),
				new[]
				{
					(
						"INSERT INTO Orders (CustomerName, Status, TotalAmount) VALUES (@CustomerName, @Status, @TotalAmount)",
						(object?)new { CustomerName = "CrossServer_API", Status = "Queued", TotalAmount = 10m }
					)
				}),
			["Products"] = (
				GetConnection("Example2"),
				new[]
				{
					(
						"INSERT INTO Products (ProductName, ProductCategory, UnitPrice, StockLevel) VALUES (@ProductName, @ProductCategory, @UnitPrice, @StockLevel)",
						(object?)new { ProductName = "CrossServer_API_Product", ProductCategory = "Test", UnitPrice = 10m, StockLevel = 1 }
					)
				})
		};

		var result = await _dataAccess.ExecuteDistributedTransactionAcrossServersAsync(commandsByServer, ct);

		return result.Success
			? Ok(new { success = true })
			: StatusCode(StatusCodes.Status500InternalServerError, new
			{
				success = false,
				errors = result.Errors.Select(e => e.Message)
			});
	}

	// Helpers and nested DTOs
	private DatabaseConnectionOptions GetConnection(string name) =>
		_options.Databases.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase))
		?? throw new InvalidOperationException($"Database '{name}' not found in configuration.");

	public record DatabaseNameRow(string DatabaseName);
}
