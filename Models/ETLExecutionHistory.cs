namespace ServicioRESTEjecucionComandos.Models;

/// <summary>
/// Represents an ETL execution history record stored in the <c>hist_etl_execution</c> table.
/// Each row captures the full execution context and result status.
/// </summary>
public class ETLExecutionHistory
{
    /// <summary>
    /// Unique identifier for this execution history record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The sending code identifier for the entity.
    /// </summary>
    public string CodEnvio { get; set; } = string.Empty;

    /// <summary>
    /// The entity type associated with this execution.
    /// </summary>
    public string TipoEntidad { get; set; } = string.Empty;

    /// <summary>
    /// The data date associated with this execution.
    /// </summary>
    public DateOnly FechaDatos { get; set; }

    /// <summary>
    /// The database code executed.
    /// </summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>
    /// Current execution status: PENDIENTE, EN PROCESO, EXITOSO, FALLIDO.
    /// </summary>
    public string Status { get; set; } = "PENDIENTE";

    /// <summary>
    /// Trigger type that initiated this execution: MANUAL, PROGRAMADO, REPROCESO.
    /// </summary>
    public string TriggerType { get; set; } = "MANUAL";

    /// <summary>
    /// Exit code from the command execution.
    /// </summary>
    public int? ExitCode { get; set; }

    /// <summary>
    /// Standard output captured during execution.
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// Error output captured during execution.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Timestamp when execution started.
    /// </summary>
    public DateTime? ExecutedAt { get; set; }

    /// <summary>
    /// Timestamp when execution completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}