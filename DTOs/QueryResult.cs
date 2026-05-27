namespace ServicioRESTEjecucionComandos.DTOs;

/// <summary>
/// DTO representing the result of a query operation.
/// </summary>
public class QueryResult
{
    public string TipoEntidad { get; set; } = string.Empty;

    public string CodEnvio { get; set; } = string.Empty;

    public DateOnly? Fechadatos { get; set; }

    /// <summary>
    /// Execution status from hist_etl_execution (PENDIENTE, EN PROCESO, EXITOSO, FALLIDO).
    /// </summary>
    public string? EstadoEjecucion { get; set; }

    /// <summary>
    /// Last execution timestamp from hist_etl_execution.
    /// </summary>
    public DateTime? UltimaFechaEjecucion { get; set; }

    /// <summary>
    /// Standard output from the last successful execution.
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// Error output from the last failed execution.
    /// </summary>
    public string? Error { get; set; }
}