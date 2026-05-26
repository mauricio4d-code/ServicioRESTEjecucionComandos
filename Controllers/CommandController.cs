using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServicioRESTEjecucionComandos.Data;
using ServicioRESTEjecucionComandos.DTOs;
using ServicioRESTEjecucionComandos.Models;
using ServicioRESTEjecucionComandos.Repositories;
using ServicioRESTEjecucionComandos.Services;

namespace ServicioRESTEjecucionComandos.Controllers;

/// <summary>
/// Controller providing endpoints for command execution, base-datos lookup, and query results.
/// Requires authentication via JWT bearer token.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CommandController : ControllerBase
{
    private readonly ExecutionQueue _executionQueue;
    private readonly ServiceItemRepository _serviceItemRepo;
    private readonly ServiceDbContext _serviceDbContext;

    /// <summary>
    /// Initializes a new instance of CommandController.
    /// </summary>
    public CommandController(
        ExecutionQueue executionQueue,
        ServiceItemRepository serviceItemRepo,
        ServiceDbContext serviceDbContext)
    {
        _executionQueue = executionQueue;
        _serviceItemRepo = serviceItemRepo;
        _serviceDbContext = serviceDbContext;
    }

    // -----------------------------------------------------------------------
    // Base Datos endpoints
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns all records from the base_datos table for populating the dropdown.
    /// </summary>
    [HttpGet("base-datos")]
    public async Task<IActionResult> GetBaseDatos()
    {
        var baseDatosList = await _serviceDbContext.Database
            .SqlQueryRaw<BaseDatos>("SELECT codigo, nombre FROM base_datos")
            .ToListAsync();
        return Ok(baseDatosList);
    }

    // -----------------------------------------------------------------------
    // Query results endpoint
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns query results for the given database code.
    /// Executes: SELECT DISTINCT ON (e.cod_envio) s.tipoentidad, e.cod_envio, s.fechadatos
    /// FROM dim_entidad_asfi e JOIN dtx_seguimiento s ON e.tipo_entidad_asfi_codigo = s.tipoentidad
    /// WHERE e.cod_envio IS NOT NULL AND e.cod_envio <> '' AND s.codigo = {codigo}
    /// ORDER BY e.cod_envio, s.fechadatos DESC
    /// </summary>
    [HttpGet("query-results")]
    public async Task<IActionResult> GetQueryResults([FromQuery] string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return BadRequest(new { error = "El parámetro 'codigo' es requerido." });
        }

        try
        {
            var results = await _serviceDbContext.Database
                .SqlQueryRaw<QueryResult>(
                    @"SELECT DISTINCT ON (e.cod_envio)
                        s.tipoentidad AS ""TipoEntidad"",
                        e.cod_envio AS ""CodEnvio"",
                        s.fechadatos AS ""FechaDatos""
                    FROM dim_entidad_asfi e
                    JOIN dtx_seguimiento s
                        ON e.tipo_entidad_asfi_codigo = s.tipoentidad
                    WHERE e.cod_envio IS NOT NULL
                        AND e.cod_envio <> ''
                        AND s.codigo = {0}
                    ORDER BY e.cod_envio, s.fechadatos DESC",
                    codigo)
                .ToListAsync();

            return Ok(results);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Error al ejecutar la consulta.", detail = ex.Message });
        }
    }

    // -----------------------------------------------------------------------
    // Execution endpoints
    // -----------------------------------------------------------------------

    /// <summary>
    /// Enqueues a new command execution linked to a ServiceItem record.
    /// Accepts 'action' (Actualizar or Reprocesar) and 'codigo' from the request body.
    /// </summary>
    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteCommand([FromBody] ExecuteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo))
        {
            return BadRequest(new { error = "El parámetro 'codigo' es requerido." });
        }

        // Create ServiceItem record with PENDING status
        var serviceItem = new ServiceItem
        {
            Status = "PENDING"
        };
        await _serviceItemRepo.CreateAsync(serviceItem);

        // Create queue item linked to the ServiceItem
        var queueItem = new ExecutionQueueItem
        {
            ServiceItemId = serviceItem.ItemId,
            Code = request.Codigo,
            Start = DateTime.Now.ToString("yyyy-MM-dd"),
            End = DateTime.Now.ToString("yyyy-MM-dd"),
            Codesend = request.Codigo,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        var queueItemId = _executionQueue.Enqueue(queueItem);

        return Ok(new
        {
            QueueItemId = queueItemId,
            ServiceItemId = serviceItem.ItemId,
            Status = serviceItem.Status,
            Message = $"Command enqueued successfully. Action: {request.Action}"
        });
    }

    /// <summary>
    /// Returns the current status of a ServiceItem by its ItemId.
    /// Used for polling execution progress.
    /// </summary>
    [HttpGet("status/{itemId}")]
    public async Task<IActionResult> GetServiceItemStatus(Guid itemId)
    {
        var item = await _serviceItemRepo.GetByIdAsync(itemId);

        if (item == null)
        {
            return NotFound(new { error = $"ServiceItem with Id {itemId} not found." });
        }

        return Ok(item);
    }

    /// <summary>
    /// Returns all ServiceItem records ordered by creation time descending.
    /// </summary>
    [HttpGet("service-items")]
    public async Task<IActionResult> GetAllServiceItems()
    {
        var items = await _serviceItemRepo.GetAllAsync();
        return Ok(items);
    }
}

/// <summary>
/// Request DTO for the execute endpoint.
/// </summary>
public class ExecuteRequest
{
    /// <summary>
    /// The action type: "Actualizar" or "Reprocesar".
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// The database code to execute against.
    /// </summary>
    public string? Codigo { get; set; }
}