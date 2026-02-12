using Microsoft.AspNetCore.SignalR;
using Webhooks.Core.Models;

namespace Webhooks.Api.Hubs;

/// <summary>
/// Hub SignalR para notificaciones de webhooks en tiempo real.
/// Los clientes (ERP) se conectan aquí para recibir eventos instantáneamente.
/// </summary>
public class WebhookHub : Hub
{
    private readonly ILogger<WebhookHub> _logger;

    public WebhookHub(ILogger<WebhookHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Se ejecuta cuando un cliente se conecta.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Cliente conectado: {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("Connected", new
        {
            ConnectionId = Context.ConnectionId,
            Message = "Conectado al hub de webhooks",
            Timestamp = DateTime.UtcNow
        });
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Se ejecuta cuando un cliente se desconecta.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Cliente desconectado: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Permite al cliente suscribirse a eventos de una fuente específica.
    /// </summary>
    public async Task SubscribeToSource(string source)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"source:{source}");
        _logger.LogInformation("Cliente {ConnectionId} suscrito a {Source}", Context.ConnectionId, source);
        await Clients.Caller.SendAsync("Subscribed", new { Source = source });
    }

    /// <summary>
    /// Permite al cliente desuscribirse de una fuente específica.
    /// </summary>
    public async Task UnsubscribeFromSource(string source)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"source:{source}");
        _logger.LogInformation("Cliente {ConnectionId} desuscrito de {Source}", Context.ConnectionId, source);
        await Clients.Caller.SendAsync("Unsubscribed", new { Source = source });
    }

    /// <summary>
    /// Permite al cliente suscribirse a todos los eventos.
    /// </summary>
    public async Task SubscribeToAll()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "all");
        _logger.LogInformation("Cliente {ConnectionId} suscrito a todos los eventos", Context.ConnectionId);
        await Clients.Caller.SendAsync("SubscribedToAll", new { Message = "Suscrito a todos los eventos" });
    }
}
