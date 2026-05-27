namespace ServicioRESTEjecucionComandos.DTOs;

/// <summary>
/// Response DTO for base-datos lookup, extending BaseDatos with an IsDayBased flag.
/// </summary>
public class BaseDatosResponse
{
    /// <summary>
    /// Database code identifier.
    /// </summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>
    /// Database display name.
    /// </summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether this code uses day-based date logic instead of month-based.
    /// </summary>
    public bool IsDayBased { get; set; }
}