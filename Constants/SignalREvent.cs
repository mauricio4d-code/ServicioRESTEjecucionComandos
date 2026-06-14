namespace ServicioRESTEjecucionComandos.Constants;

/// <summary>
/// SignalR event names used for real-time ETL execution notifications.
/// </summary>
public static class SignalREvent
{
    /// <summary>
    /// Broadcast when a scheduled ETL task starts execution.
    /// </summary>
    public const string TaskStarted = "TaskStarted";

    /// <summary>
    /// Broadcast when a scheduled ETL task completes execution.
    /// </summary>
    public const string TaskCompleted = "TaskCompleted";
}
