namespace ServicioRESTEjecucionComandos.Constants;

/// <summary>
/// Trigger type values indicating how an ETL execution was initiated.
/// </summary>
public static class TriggerType
{
    /// <summary>
    /// Manual execution triggered by user action (Actualizar).
    /// </summary>
    public const string Manual = "MANUAL";

    /// <summary>
    /// Re-processing execution triggered by user action (Reprocesar).
    /// </summary>
    public const string Reproceso = "REPROCESO";
}
