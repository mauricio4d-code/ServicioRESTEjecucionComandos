namespace ServicioRESTEjecucionComandos.Models;

/// <summary>
/// Represents a record from the legacy 'dtx_process' table shared across systems.
/// This model is ignored by EF Core and queried via raw SQL only.
/// </summary>
public class DtxProcess
{
    /// <summary>
    /// Primary key identifier for the process record.
    /// </summary>
    public long IdProcess { get; set; }

    /// <summary>
    /// Application name that owns this process.
    /// </summary>
    public string AppName { get; set; } = string.Empty;

    /// <summary>
    /// Process name identifier.
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the process (e.g., RUNNING, COMPLETED, NOTCOMPLETED).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the process started.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Timestamp when the process ended. Null if still running.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Whether this process record is active.
    /// </summary>
    public bool Active { get; set; } = true;

    /// <summary>
    /// Optional details about the process.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Timestamp when the record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
