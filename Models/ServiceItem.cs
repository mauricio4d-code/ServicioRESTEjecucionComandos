namespace ServicioRESTEjecucionComandos.Models;

/// <summary>
/// Represents a service execution item tracked in the ServiceItem database table.
/// </summary>
public class ServiceItem
{
    /// <summary>
    /// Unique identifier for this service item.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Current execution status: PENDING, RUNNING, SUCCESS, FAILED.
    /// </summary>
    public string Status { get; set; } = "PENDING";

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
