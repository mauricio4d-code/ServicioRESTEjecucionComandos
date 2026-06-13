namespace ServicioRESTEjecucionComandos.Constants;

/// <summary>
/// Schedule-related constants used for Hangfire job identification.
/// </summary>
public static class Schedule
{
    /// <summary>
    /// Prefix used for Hangfire recurring job IDs tied to ETL schedules.
    /// </summary>
    public const string JobIdPrefix = "etl-schedule-";
}
