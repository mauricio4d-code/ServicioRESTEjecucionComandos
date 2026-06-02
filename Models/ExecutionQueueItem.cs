namespace ServicioRESTEjecucionComandos.Models;

/// <summary>
/// Represents an item in the execution queue, linked to an ETLExecutionHistory record.
/// </summary>
public class ExecutionQueueItem
{
    /// <summary>
    /// Unique identifier for this queue item.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The Id of the associated ETLExecutionHistory record in the database.
    /// </summary>
    public Guid HistoryId { get; set; }

    /// <summary>
    /// Plain string command-line arguments for this execution.
    /// Optional - when null or empty, the command executes with no additional arguments.
    /// </summary>
    public string? Params { get; set; }

    /// <summary>
    /// Current status of the execution item: PENDIENTE, EN PROCESO, EXITOSO, FALLIDO.
    /// </summary>
    public string Status { get; set; } = "PENDIENTE";

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