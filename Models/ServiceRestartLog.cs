namespace ServicioRESTEjecucionComandos.Models;

/// <summary>
/// Tracks when a Windows service was last restarted and whether the operation succeeded.
/// Stored locally in the SQLite database alongside refresh tokens and schedules.
/// </summary>
public class ServiceRestartLog
{
    /// <summary>
    /// Auto-incrementing primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Name of the Windows service that was restarted.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the restart was performed.
    /// </summary>
    public DateTime RestartedAt { get; set; }

    /// <summary>
    /// Result of the restart operation: "Success" or "Failed".
    /// </summary>
    public string Status { get; set; } = string.Empty;
}
