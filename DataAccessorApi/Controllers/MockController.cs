using Microsoft.AspNetCore.Mvc;
using DataAccessor.Api.Helpers;
 
namespace DataAccessor.Api.Controllers;
 
/// <summary>
/// Built-in mock HTTP endpoints that stand in for an external REST API.
/// Used by HttpDataAccessController and CompositeDataAccessController so that
/// HTTP data-access scenarios work without any external network dependency.
/// </summary>
[ApiController]
[Route("mock")]
[Produces("application/json")]
[SwaggerVisible]
public class MockController : ControllerBase
{
    private static readonly object[] Todos =
    [
        new { userId = 1, id = 1, title = "Buy groceries",   completed = false },
        new { userId = 1, id = 2, title = "Walk the dog",    completed = true  },
        new { userId = 2, id = 3, title = "Read a book",     completed = false }
    ];
 
    /// <summary>Returns a single todo item by id.</summary>
    [HttpGet("todos/{id:int}")]
    [SwaggerVisible]
    public IActionResult GetTodo(int id)
    {
        var todo = Todos.Cast<dynamic>().FirstOrDefault(t => t.id == id);
        return todo is null ? NotFound() : Ok(todo);
    }
 
    /// <summary>Returns a list of todo items, optionally limited via ?_limit=n.</summary>
    [HttpGet("todos")]
    [SwaggerVisible]
    public IActionResult GetTodos([FromQuery] int _limit = 10)
    {
        var items = Todos.Take(_limit);
        return Ok(items);
    }
 
    /// <summary>Accepts a POST and echoes back the body with a generated id.</summary>
    [HttpPost("posts")]
    public IActionResult CreatePost([FromBody] object body)
    {
        return StatusCode(StatusCodes.Status201Created, new { id = 101, body });
    }
}
 
 