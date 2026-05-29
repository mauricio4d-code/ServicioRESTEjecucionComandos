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
/// Controller providing endpoints for ETL execution, base-datos lookup, and query results.
/// Requires authentication via JWT bearer token.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ETLExecutorController : ControllerBase
{
    private readonly EtlJobService _etlJobService;
    private readonly ETLExecutionHistoryRepository _historyRepo;
    private readonly ServiceDbContext _serviceDbContext;
    private readonly string[] _dailyCodes;

    /// <summary>
    /// Initializes a new instance of ETLExecutorController.
    /// </summary>
    public ETLExecutorController(
        EtlJobService etlJobService,
        ETLExecutionHistoryRepository historyRepo,
        ServiceDbContext serviceDbContext,
        IConfiguration configuration)
    {
        _etlJobService = etlJobService;
        _historyRepo = historyRepo;
        _serviceDbContext = serviceDbContext;
        _dailyCodes = configuration.GetSection("QueueConfig:DailyCodes").Get<string[]>() ?? Array.Empty<string>();
    }

    // -----------------------------------------------------------------------
    // Base Datos endpoints
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns all records from the base_datos table for populating the dropdown,
    /// enriched with an IsDayBased flag for codes configured in QueueConfig:DailyCodes.
    /// </summary>
    [HttpGet("base-datos")]
    public async Task<IActionResult> GetBaseDatos()
    {
        var baseDatosList = await _serviceDbContext.Database
            .SqlQueryRaw<BaseDatos>("SELECT codigo, nombre FROM base_datos")
            .ToListAsync();

        var response = baseDatosList.Select(item => new BaseDatosResponse
        {
            Codigo = item.Codigo,
            Nombre = item.Nombre,
            IsDayBased = _dailyCodes.Contains(item.Codigo, StringComparer.OrdinalIgnoreCase)
        }).ToList();

        return Ok(response);
    }

    // -----------------------------------------------------------------------
    // Query results endpoint
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns query results for the given database code, including execution status from hist_etl_execution.
    /// Uses a CTE to get the latest execution status per row.
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
                    @"WITH latest_exec AS (
                        SELECT DISTINCT ON (""CodEnvio"", ""TipoEntidad"", ""FechaDatos"")
                            ""CodEnvio"",
                            ""TipoEntidad"",
                            ""FechaDatos"",
                            ""Status"" AS estado_ejecucion,
                            ""TriggerType"" AS trigger_type,
                            ""CompletedAt"" AS ultima_fecha_ejecucion,
                            ""Output"" AS ""output"",
                            ""Error"" AS ""error""
                        FROM hist_etl_execution
                        ORDER BY ""CodEnvio"", ""TipoEntidad"", ""FechaDatos"", ""CompletedAt"" DESC NULLS LAST
                    )
                    SELECT DISTINCT ON (e.cod_envio)
                        s.tipoentidad AS ""TipoEntidad"",
                        e.cod_envio AS ""CodEnvio"",
                        s.fechadatos AS ""FechaDatos"",
                        le.estado_ejecucion AS ""EstadoEjecucion"",
                        le.trigger_type AS ""TriggerType"",
                        le.ultima_fecha_ejecucion AS ""UltimaFechaEjecucion"",
                        le.""output"" AS ""Output"",
                        le.""error"" AS ""Error""
                    FROM dim_entidad_asfi e
                    JOIN dtx_seguimiento s
                        ON s.cod_envio = e.cod_envio
                    LEFT JOIN latest_exec le
                        ON le.""CodEnvio"" = e.cod_envio
                        AND le.""TipoEntidad"" = s.tipoentidad
                        AND le.""FechaDatos"" = s.fechadatos
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
    /// Enqueues a new command execution linked to a ETLExecutionHistory record.
    /// Accepts full row data (TipoEntidad, CodEnvio, FechaDatos, Codigo) from the request body.
    /// </summary>
    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteCommand([FromBody] ETLExecuteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo))
        {
            return BadRequest(new { error = "El parámetro 'codigo' es requerido." });
        }

        var fechaDatos = request.FechaDatos ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var triggerType = request.Action?.ToUpperInvariant() switch
        {
            "ACTUALIZAR" => "MANUAL",
            "REPROCESAR" => "REPROCESO",
            _ => "MANUAL"
        };

        // Delegate to EtlJobService which creates history record and enqueues via Hangfire
        var historyId = await _etlJobService.EnqueueManualAsync(
            request.TipoEntidad ?? string.Empty,
            request.CodEnvio ?? string.Empty,
            fechaDatos,
            request.Codigo,
            triggerType);

        return Ok(new
        {
            HistoryId = historyId,
            Status = "PENDIENTE",
            Message = $"Command enqueued successfully via Hangfire. Action: {request.Action}"
        });
    }

    /// <summary>
    /// Returns the current status of a ETLExecutionHistory record by its Id (HistoryId).
    /// Used for polling execution progress.
    /// </summary>
    [HttpGet("status/{historyId}")]
    public async Task<IActionResult> GetExecutionStatus(Guid historyId)
    {
        var item = await _historyRepo.GetByIdAsync(historyId);

        if (item == null)
        {
            return NotFound(new { error = $"ETLExecutionHistory with Id {{historyId}} not found." });
        }

        return Ok(item);
    }
}

/// <summary>
/// Request DTO for the ETL execute endpoint.
/// </summary>
public class ETLExecuteRequest
{
    /// <summary>
    /// The action type: "Actualizar" or "Reprocesar".
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// The entity type for this execution.
    /// </summary>
    public string? TipoEntidad { get; set; }

    /// <summary>
    /// The sending code identifier for this execution.
    /// </summary>
    public string? CodEnvio { get; set; }

    /// <summary>
    /// The data date for this execution.
    /// </summary>
    public DateOnly? FechaDatos { get; set; }

    /// <summary>
    /// The database code to execute against.
    /// </summary>
    public string? Codigo { get; set; }

    /// <summary>
    /// Indicates whether this code uses day-based date logic instead of month-based.
    /// </summary>
    public bool IsDayBased { get; set; }
}