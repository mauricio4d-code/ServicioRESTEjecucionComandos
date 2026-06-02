namespace ServicioRESTEjecucionComandos.DTOs;

/// <summary>
/// Request DTO for creating a new ETL schedule.
/// </summary>
public class CreateEtlScheduleDto
{
    /// <summary>
    /// Plain string command-line arguments for this schedule execution.
    /// Optional - when null or empty, the command executes with no additional arguments.
    /// </summary>
    public string? Params { get; set; }

    /// <summary>
    /// Cron expression defining the schedule frequency.
    /// </summary>
    public string? CronExpression { get; set; }
}

/// <summary>
/// Request DTO for updating an existing ETL schedule.
/// </summary>
public class UpdateEtlScheduleDto
{
    /// <summary>
    /// Plain string command-line arguments for this schedule execution.
    /// Optional - when null or empty, the command executes with no additional arguments.
    /// </summary>
    public string? Params { get; set; }

    /// <summary>
    /// Cron expression defining the schedule frequency.
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Indicates whether this schedule is active.
    /// </summary>
    public bool? IsActive { get; set; }
}

/// <summary>
/// Response DTO for ETL schedule information.
/// </summary>
public class EtlScheduleDto
{
    /// <summary>
    /// Unique identifier for this schedule.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Plain string command-line arguments for this schedule execution.
    /// </summary>
    public string? Params { get; set; }

    /// <summary>
    /// Cron expression defining the schedule frequency.
    /// </summary>
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether this schedule is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Timestamp when this schedule was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when this schedule was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
