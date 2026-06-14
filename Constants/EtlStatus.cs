namespace ServicioRESTEjecucionComandos.Constants;

/// <summary>
/// ETL execution status values used across the application for tracking job lifecycle.
/// </summary>
public static class EtlStatus
{
    /// <summary>
    /// Job has been queued and is waiting for execution.
    /// </summary>
    public const string Pending = "PENDIENTE";

    /// <summary>
    /// Job is currently being executed.
    /// </summary>
    public const string InProgress = "EN PROCESO";

    /// <summary>
    /// Job completed successfully.
    /// </summary>
    public const string Success = "EXITOSO";

    /// <summary>
    /// Job failed during execution.
    /// </summary>
    public const string Failed = "FALLIDO";
}
