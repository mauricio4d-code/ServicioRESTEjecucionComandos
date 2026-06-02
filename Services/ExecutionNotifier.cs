using Microsoft.AspNetCore.SignalR;
using ServicioRESTEjecucionComandos.Hubs;

namespace ServicioRESTEjecucionComandos.Services;

/// <summary>
/// Broadcasts scheduled ETL execution events to all connected SignalR clients.
/// </summary>
public class ExecutionNotifier
{
    private readonly IHubContext<EtlNotificationHub> _hubContext;

    /// <summary>
    /// Initializes a new instance of the ExecutionNotifier.
    /// </summary>
    public ExecutionNotifier(IHubContext<EtlNotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Broadcasts a notification that a scheduled ETL task has started.
    /// </summary>
    /// <param name="params">The command-line parameters for this execution.</param>
    public async Task BroadcastTaskStartedAsync(string? @params)
    {
        await _hubContext.Clients.All.SendAsync("TaskStarted", @params);
    }

    /// <summary>
    /// Broadcasts a notification that a scheduled ETL task has completed.
    /// </summary>
    /// <param name="success">Whether the task completed successfully.</param>
    /// <param name="params">The command-line parameters for this execution.</param>
    public async Task BroadcastTaskCompletedAsync(bool success, string? @params)
    {
        await _hubContext.Clients.All.SendAsync("TaskCompleted", success, @params);
    }
}
