namespace ServicioRESTEjecucionComandos.Models;

/// <summary>
/// Represents a record from the legacy 'base_datos' table.
/// </summary>
public class BaseDatos
{
    /// <summary>
    /// Database code identifier.
    /// </summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>
    /// Database display name.
    /// </summary>
    public string Nombre { get; set; } = string.Empty;
}
