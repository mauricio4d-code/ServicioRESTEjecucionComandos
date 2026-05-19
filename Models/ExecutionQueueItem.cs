namespace ServicioRESTEjecucionComandos.Models;

/// <summary>
/// Represents an item in the execution queue.
/// </summary>
public class ExecutionQueueItem
{
    /// <summary>
    /// Unique identifier for this queue item.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The code parameter for the Datax command.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// The start date parameter for the Datax command.
    /// </summary>
    public string Start { get; set; } = string.Empty;

    /// <summary>
    /// The end date parameter for the Datax command.
    /// </summary>
    public string End { get; set; } = string.Empty;

    /// <summary>
    /// The codesend parameter for the Datax command.
    /// </summary>
    public string Codesend { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the execution item.
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Result or error message after execution completes.
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// Timestamp when this item was created and enqueued.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when this item completed execution (if applicable).
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}