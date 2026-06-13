using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ServicioRESTEjecucionComandos.Hubs;

/// <summary>
/// SignalR hub for broadcasting real-time ETL scheduled task notifications to connected clients.
/// Clients receive <c>TaskStarted</c> and <c>TaskCompleted</c> messages.
/// Requires JWT authentication to connect.
/// </summary>
[Authorize]
public class EtlNotificationHub : Hub
{
}
