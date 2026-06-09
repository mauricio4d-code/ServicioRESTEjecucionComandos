namespace ServicioRESTEjecucionComandos.Models;

/// <summary>
/// Represents the <c>reiniciar_servicio</c> table row used to signal
/// that a Windows service should be restarted.
/// Query via <c>ServiceDbContext.Database.SqlQueryRaw<ServiceRestartStatus>()</c>.
/// </summary>
public class ServiceRestartStatus
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// <c>true</c> when the target Windows service should be restarted.
    /// </summary>
    public bool Reiniciar { get; set; }
}
