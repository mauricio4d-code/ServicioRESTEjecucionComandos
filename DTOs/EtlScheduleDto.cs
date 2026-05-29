namespace ServicioRESTEjecucionComandos.DTOs;

/// <summary>
/// Request DTO for creating a new ETL schedule.
/// </summary>
public class CreateEtlScheduleDto
{
    /// <summary>
    /// The sending code identifier for the entity.
    /// </summary>
    public string? CodEnvio { get; set; }

    /// <summary>
    /// The entity type associated with this schedule.
    /// </summary>
    public string? TipoEntidad { get; set; }

    /// <summary>
    /// The database code to execute.
    /// </summary>
    public string? Codigo { get; set; }

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
    /// The sending code identifier for the entity.
    /// </summary>
    public string? CodEnvio { get; set; }

    /// <summary>
    /// The entity type associated with this schedule.
    /// </summary>
    public string? TipoEntidad { get; set; }

    /// <summary>
    /// The database code to execute.
    /// </summary>
    public string? Codigo { get; set; }

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
    /// The sending code identifier for the entity.
    /// </summary>
    public string CodEnvio { get; set; } = string.Empty;

    /// <summary>
    /// The entity type associated with this schedule.
    /// </summary>
    public string TipoEntidad { get; set; } = string.Empty;

    /// <summary>
    /// The database code to execute.
    /// </summary>
    public string Codigo { get; set; } = string.Empty;

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
