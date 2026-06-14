namespace ServicioRESTEjecucionComandos.Constants;

/// <summary>
/// DTX Process status values used for tracking Datax process execution state.
/// </summary>
public static class DtxProcessStatus
{
    /// <summary>
    /// Process is currently running.
    /// </summary>
    public const string Running = "RUNNING";

    /// <summary>
    /// Process completed successfully.
    /// </summary>
    public const string Completed = "COMPLETED";

    /// <summary>
    /// Process did not complete successfully.
    /// </summary>
    public const string NotCompleted = "NOTCOMPLETED";
}
