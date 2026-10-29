using DataAccessor.Api.Helpers;
using DataAccessor.Library.Core;
using Microsoft.AspNetCore.Mvc;
 
namespace DataAccessor.Api.Controllers;
 
/// <summary>
/// Predefined scenarios for <see cref="UnitOfWork"/> transaction semantics.
/// Each endpoint mirrors one test case from <c>UnitOfWorkTests</c> — no request body required.
/// </summary>
[ApiController]
[Route("api/unit-of-work")]
[Produces("application/json")]
[SwaggerVisible]
public class UnitOfWorkController : ControllerBase
{
    private readonly IDbConnectionFactory _connectionFactory;
 
    public UnitOfWorkController(IDbConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;
 
    /// <summary>
    /// [TransactionCommit] Begin a transaction, run SELECT with parameters inside it,
    /// commit, and return the query result → {Id: 1, Value: "Test Value"}.
    /// </summary>
    [HttpGet("commit")]
    public async Task<IActionResult> Commit(CancellationToken ct)
    {
        await using var uow = new UnitOfWork(_connectionFactory);
        await uow.BeginTransactionAsync();
 
        using var result = await uow.QueryJsonAsync(
            "SELECT @Id AS Id, @Value AS Value",
            new { Id = 1, Value = "Test Value" },
            cancellationToken: ct);
 
        var data = result.RootElement.GetRawText();
        await uow.CommitAsync();
 
        return Content(data, "application/json");
    }
 
    /// <summary>
    /// [TransactionRollback] Begin a transaction and immediately roll it back
    /// — demonstrates the rollback path succeeds without error → {rolledBack: true}.
    /// </summary>
    [HttpGet("rollback")]
    public async Task<IActionResult> Rollback(CancellationToken ct)
    {
        await using var uow = new UnitOfWork(_connectionFactory);
        await uow.BeginTransactionAsync();
        await uow.RollbackAsync();
        return Ok(new { rolledBack = true });
    }
}
 
 
 