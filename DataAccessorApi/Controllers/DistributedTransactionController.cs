using DataAccessor.Library.Core;
using DataAccessor.Api.Helpers;
using Microsoft.AspNetCore.Mvc;
 
namespace DataAccessor.Api.Controllers;
 
/// <summary>
/// Predefined scenarios for <see cref="DistributedTransactionManager"/> using
/// pre-configured named connections (Example1, Example2).
/// Each endpoint mirrors one test case from <c>DistributedTransactionManagerTests</c>
/// — no request body required.
/// </summary>
[ApiController]
[Route("api/distributed-transaction")]
[Produces("application/json")]
[SwaggerVisible]
public class DistributedTransactionController : ControllerBase
{
    private readonly DistributedTransactionManager _manager;
 
    public DistributedTransactionController(DistributedTransactionManager manager) =>
        _manager = manager;
 
    /// <summary>
    /// [CommitOnAllServers] INSERT one Order into Example1 and one Product into Example2
    /// inside a distributed transaction — both commit → {success: true}.
    /// Requires test databases with Orders and Products tables (run CreateTestDatabases.ps1 first).
    /// </summary>
    [HttpPost("commit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Commit(CancellationToken ct)
    {
        var commands = new Dictionary<string, IEnumerable<(string, object?)>>
        {
            ["Example1"] = new (string, object?)[]
            {
                (
                    "INSERT INTO Orders (CustomerName, Status, TotalAmount) VALUES (@Name, @Status, @TotalAmount)",
                    new { Name = "DistTx_API_Commit", Status = "Active", TotalAmount = 100.0 }
                )
            },
            ["Example2"] = new (string, object?)[]
            {
                (
                    "INSERT INTO Products (ProductName, ProductCategory, UnitPrice, StockLevel) VALUES (@ProductName, @ProductCategory, @UnitPrice, @StockLevel)",
                    new { ProductName = "DistTx_API_Product", ProductCategory = "TestCategory", UnitPrice = 1.0, StockLevel = 10 }
                )
            }
        };
 
        var result = await _manager.ExecuteDistributedTransactionAsync(commands, ct);
 
        return result.Success
            ? Ok(new { success = true })
            : StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                errors = result.Errors.Select(e => e.Message)
            });
    }
 
    /// <summary>
    /// [RollbackOnError] One valid command on Example1 + deliberately invalid SQL on Example2
    /// — error causes full rollback → always returns {success: false, errors: [...]}.
    /// Does NOT require any specific tables — the invalid SQL triggers rollback before any write.
    /// </summary>
    [HttpPost("rollback-on-error")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RollbackOnError(CancellationToken ct)
    {
        var commands = new Dictionary<string, IEnumerable<(string, object?)>>
        {
            ["Example1"] = new (string, object?)[]
            {
                ("SELECT 1", null)                         // valid — would commit
            },
            ["Example2"] = new (string, object?)[]
            {
                ("INVALID SQL STATEMENT", null)            // invalid — triggers rollback
            }
        };
 
        var result = await _manager.ExecuteDistributedTransactionAsync(commands, ct);
 
        // This scenario is expected to fail — return 200 with the failure details
        return Ok(new
        {
            success = result.Success,
            errors  = result.Errors.Select(e => e.Message)
        });
    }
}
 
 
 