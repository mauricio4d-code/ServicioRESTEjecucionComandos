using ServicioRESTEjecucionComandos.Constants;

namespace ServicioRESTEjecucionComandos.Models;

/// <summary>
/// Represents a scheduled ETL execution record stored in the <c>hist_etl_execution_scheduled</c> table.
/// Each row captures the execution context for a scheduled (PROGRAMADO) run, linked to an EtlSchedule.
/// </summary>
public class ETLExecutionHistoryScheduled
{
    /// <summary>
    /// Unique identifier for this scheduled execution record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The Id of the associated EtlSchedule record that triggered this execution.
    /// </summary>
    public Guid ScheduleId { get; set; }

    /// <summary>
    /// Plain string command-line arguments for this execution.
    /// </summary>
    public string? Params { get; set; }

    /// <summary>
    /// Current execution status: PENDIENTE, EN PROCESO, EXITOSO, FALLIDO.
    /// </summary>
    public string Status { get; set; } = EtlStatus.Pending;

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

    /// <summary>
    /// Timestamp when this record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
