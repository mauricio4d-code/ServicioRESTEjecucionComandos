namespace ServicioRESTEjecucionComandos.DTOs;

/// <summary>
/// DTO representing the result of a query operation.
/// </summary>
public class QueryResult
{
    public string TipoEntidad { get; set; } = string.Empty;

    public string CodEnvio { get; set; } = string.Empty;

    public DateTime? Fechadatos { get; set; }
}