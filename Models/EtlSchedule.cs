namespace ServicioRESTEjecucionComandos.Models;

/// <summary>
/// Represents a scheduled ETL execution stored in the <c>etl_schedule</c> table.
/// Serves as the source of truth for recurring ETL jobs synchronized to Hangfire.
/// </summary>
public class EtlSchedule
{
    /// <summary>
    /// Unique identifier for this schedule.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Plain string command-line arguments for this schedule execution.
    /// Optional - when null or empty, the command executes with no additional arguments.
    /// </summary>
    public string? Params { get; set; }

    /// <summary>
    /// Cron expression defining the schedule frequency.
    /// </summary>
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether this schedule is active and should be synchronized to Hangfire.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp when this schedule was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when this schedule was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
