using Microsoft.AspNetCore.SignalR;

namespace ServicioRESTEjecucionComandos.Hubs;

/// <summary>
/// SignalR hub for broadcasting real-time ETL scheduled task notifications to connected clients.
/// Clients receive <c>TaskStarted</c> and <c>TaskCompleted</c> messages.
/// </summary>
public class EtlNotificationHub : Hub
{
}
