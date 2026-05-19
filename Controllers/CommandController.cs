using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServicioRESTEjecucionComandos.Models;
using ServicioRESTEjecucionComandos.Services;

namespace ServicioRESTEjecucionComandos.Controllers;

/// <summary>
/// Controller providing endpoints for command execution.
/// Requires authentication via JWT bearer token.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CommandController : ControllerBase
{
    private readonly ExecutionQueue _executionQueue;

    /// <summary>
    /// Initializes a new instance of CommandController.
    /// </summary>
    public CommandController(ExecutionQueue executionQueue)
    {
        _executionQueue = executionQueue;
    }

    /// <summary>
    /// Enqueues a new command execution request and returns the item ID immediately.
    /// </summary>
    /// <returns>The unique identifier of the enqueued execution item.</returns>
    [HttpPost("execute-async")]
    public IActionResult ExecuteCommandAsync()
    {
        var item = new ExecutionQueueItem();
        var itemId = _executionQueue.Enqueue(item);

        return Ok(new
        {
            Id = itemId,
            Status = item.Status,
            Message = "Command enqueued successfully"
        });
    }
}