using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServicioRESTEjecucionComandos.Constants;
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
    private readonly ILogger<ETLExecutorController> _logger;
    private readonly string[] _dailyCodes;
    private readonly string[] _excludedCodes;

    /// <summary>
    /// Initializes a new instance of ETLExecutorController.
    /// </summary>
    public ETLExecutorController(
        EtlJobService etlJobService,
        ETLExecutionHistoryRepository historyRepo,
        ServiceDbContext serviceDbContext,
        ILogger<ETLExecutorController> logger,
        IConfiguration configuration)
    {
        _etlJobService = etlJobService;
        _historyRepo = historyRepo;
        _serviceDbContext = serviceDbContext;
        _logger = logger;
        _dailyCodes = configuration.GetSection("QueueConfig:DailyCodes").Get<string[]>() ?? Array.Empty<string>();
        _excludedCodes = configuration.GetSection("QueueConfig:ExcludedCodes").Get<string[]>() ?? Array.Empty<string>();
    }

    // -----------------------------------------------------------------------
    // Base Datos endpoints
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns all records from the base_datos table for populating the dropdown,
    /// enriched with an IsDayBased flag for codes configured in QueueConfig:DailyCodes.
    /// Codes listed in QueueConfig:ExcludedCodes are filtered out at the SQL level.
    /// </summary>
    [HttpGet("base-datos")]
    public async Task<IActionResult> GetBaseDatos()
    {
        string sql;
        object[]? sqlParams = null;
        if (_excludedCodes.Length > 0)
        {
            var excludedPlaceholders = string.Join(", ", _excludedCodes.Select((_, i) => $"@p{i}"));
            sql = $"SELECT codigo, nombre FROM base_datos WHERE codigo NOT IN ({excludedPlaceholders})";
            sqlParams = _excludedCodes;
        }
        else
        {
            sql = "SELECT codigo, nombre FROM base_datos";
        }

        _logger.LogInformation("[DB] Querying base_datos table for all records ({Sql}).", sql.Replace("@p", ""));
        var baseDatosList = sqlParams is not null
            ? await _serviceDbContext.Database.SqlQueryRaw<BaseDatos>(sql, sqlParams).ToListAsync()
            : await _serviceDbContext.Database.SqlQueryRaw<BaseDatos>(sql).ToListAsync();

        _logger.LogInformation("[DB] base_datos query completed. Records returned: {Count}.", baseDatosList.Count);

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
            _logger.LogInformation("[DB] Executing query-results SQL for codigo='{Codigo}' against dtx_seguimiento + hist_etl_execution.", codigo);
            var results = await _serviceDbContext.Database
                .SqlQueryRaw<QueryResult>(
                    @"WITH ranked_exec AS (
                        SELECT
                            ""CodEnvio"",
                            ""TipoEntidad"",
                            ""FechaDatos"",
                            ""Codigo"",
                            ""Status"" AS estado_ejecucion,
                            ""TriggerType"" AS trigger_type,
                            ""CompletedAt"" AS ultima_fecha_ejecucion,
                            ""Output"" AS ""output"",
                            ""Error"" AS ""error"",
                            ROW_NUMBER() OVER (PARTITION BY ""CodEnvio"", ""TipoEntidad"", ""FechaDatos"", ""Codigo"" ORDER BY ""CompletedAt"" DESC) AS rn
                        FROM hist_etl_execution
                        WHERE ""Codigo"" = {0}
                    ),
                    latest_exec AS (
                        SELECT
                            ""CodEnvio"",
                            ""TipoEntidad"",
                            ""FechaDatos"",
                            ""Codigo"",
                            estado_ejecucion,
                            trigger_type,
                            ultima_fecha_ejecucion,
                            ""output"",
                            ""error""
                        FROM ranked_exec
                        WHERE rn = 1
                    ),
                    seguimiento AS (
                        SELECT
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
                            AND le.""Codigo"" = {0}
                        WHERE e.cod_envio IS NOT NULL
                            AND e.cod_envio <> ''
                            AND s.codigo = {0}
                    ),
                    ejecuciones_pendientes AS (
                        SELECT
                            le.""TipoEntidad"",
                            le.""CodEnvio"",
                            le.""FechaDatos"",
                            le.estado_ejecucion AS ""EstadoEjecucion"",
                            le.trigger_type AS ""TriggerType"",
                            le.ultima_fecha_ejecucion AS ""UltimaFechaEjecucion"",
                            le.""output"" AS ""Output"",
                            le.""error"" AS ""Error""
                        FROM latest_exec le
                        WHERE NOT EXISTS (
                            SELECT 1
                            FROM dtx_seguimiento s
                            WHERE s.cod_envio = le.""CodEnvio""
                              AND s.tipoentidad = le.""TipoEntidad""
                              AND s.fechadatos = le.""FechaDatos""
                              AND s.codigo = {0}
                        )
                    ),
                    union_data AS (
                        SELECT * FROM seguimiento
                        UNION ALL
                        SELECT * FROM ejecuciones_pendientes
                    ),
                    ranked_union AS (
                        SELECT
                            ""TipoEntidad"",
                            ""CodEnvio"",
                            ""FechaDatos"",
                            ""EstadoEjecucion"",
                            ""TriggerType"",
                            ""UltimaFechaEjecucion"",
                            ""Output"",
                            ""Error"",
                            ROW_NUMBER() OVER (PARTITION BY ""CodEnvio"" ORDER BY ""FechaDatos"" DESC) AS rn
                        FROM union_data
                    )
                    SELECT
                        ""TipoEntidad"",
                        ""CodEnvio"",
                        ""FechaDatos"",
                        ""EstadoEjecucion"",
                        ""TriggerType"",
                        ""UltimaFechaEjecucion"",
                        ""Output"",
                        ""Error""
                    FROM ranked_union
                    WHERE rn = 1",
                    codigo)
                .ToListAsync();

            _logger.LogInformation("[DB] Query-results completed for codigo='{Codigo}'. Records returned: {Count}.", codigo, results.Count);
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
            "ACTUALIZAR" => TriggerType.Manual,
            "REPROCESAR" => TriggerType.Reproceso,
            _ => TriggerType.Manual
        };

        // For "Actualizar" (MANUAL) action, compute the target period so the history record
        // stores the date that matches what the external ETL will create in dtx_seguimiento.
        // For "Reprocesar" (REPROCESO), keep the original FechaDatos unchanged.
        bool isDayBased = _dailyCodes.Contains(request.Codigo, StringComparer.OrdinalIgnoreCase);
        bool isActualizar = triggerType == TriggerType.Manual;
        DateOnly targetFecha;

        if (isActualizar)
        {
            if (isDayBased)
            {
                // Day-based: target is the next day
                targetFecha = fechaDatos.AddDays(1);
            }
            else
            {
                // Month-based: target is the last day of the next month
                var nextMonth = fechaDatos.AddMonths(1);
                var lastDay = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
                targetFecha = new DateOnly(nextMonth.Year, nextMonth.Month, lastDay);
            }
        }
        else
        {
            targetFecha = fechaDatos;
        }

        // Delegate to EtlJobService which creates history record and enqueues via Hangfire
        var historyId = await _etlJobService.EnqueueManualAsync(
            request.TipoEntidad ?? string.Empty,
            request.CodEnvio ?? string.Empty,
            targetFecha,
            request.Codigo,
            triggerType);

        return Ok(new
        {
            HistoryId = historyId,
            Status = EtlStatus.Pending,
            Message = $"Command enqueued successfully via Hangfire. Action: {request.Action}"
        });
    }

    /// <summary>
    /// Returns the current status of all active (PENDIENTE or EN PROCESO) ETLExecutionHistory records.
    /// Used for batch polling execution progress, replacing per-historyId polling to reduce database load.
    /// </summary>
    [HttpGet("status/active")]
    public async Task<IActionResult> GetAllActiveExecutionStatus()
    {
        _logger.LogInformation("[DB] Querying all active ETLExecutionHistory records from hist_etl_execution table.");
        var items = await _historyRepo.GetAllActiveAsync();
        _logger.LogInformation("[DB] Batch execution status query completed. Active records returned: {Count}.", items.Count);
        return Ok(items);
    }

    /// <summary>
    /// Returns the current status of a ETLExecutionHistory record by its Id (HistoryId).
    /// Used for polling execution progress.
    /// </summary>
    [HttpGet("status/{historyId}")]
    public async Task<IActionResult> GetExecutionStatus(Guid historyId)
    {
        _logger.LogInformation("[DB] Querying ETLExecutionHistory by Id {HistoryId} from hist_etl_execution table.", historyId);
        var item = await _historyRepo.GetByIdAsync(historyId);
        _logger.LogInformation("[DB] Execution status query completed for HistoryId {HistoryId}. Record found: {Found}.", historyId, item != null);

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