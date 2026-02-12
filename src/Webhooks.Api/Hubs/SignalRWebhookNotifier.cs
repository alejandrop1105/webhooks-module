using Microsoft.AspNetCore.SignalR;
using Webhooks.Core.Interfaces;
using Webhooks.Core.Models;

namespace Webhooks.Api.Hubs;

/// <summary>
/// Implementación de IWebhookNotifier usando SignalR.
/// </summary>
public class SignalRWebhookNotifier : IWebhookNotifier
{
    private readonly IHubContext<WebhookHub> _hubContext;

    public SignalRWebhookNotifier(IHubContext<WebhookHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyWebhookReceivedAsync(WebhookEvent webhookEvent)
    {
        var notification = new
        {
            Type = "received",
            EventId = webhookEvent.Id,
            Source = webhookEvent.Source,
            EventType = webhookEvent.EventType,
            ReceivedAt = webhookEvent.ReceivedAt,
            Payload = webhookEvent.Payload,
            SignatureValid = webhookEvent.SignatureValid
        };

        // Notificar a todos los suscriptores
        await _hubContext.Clients.Group("all").SendAsync("WebhookReceived", notification);

        // Notificar a suscriptores de la fuente específica
        await _hubContext.Clients.Group($"source:{webhookEvent.Source}").SendAsync("WebhookReceived", notification);
    }

    public async Task NotifyWebhookProcessedAsync(WebhookEvent webhookEvent)
    {
        var notification = new
        {
            Type = "processed",
            EventId = webhookEvent.Id,
            Source = webhookEvent.Source,
            EventType = webhookEvent.EventType,
            ProcessedAt = webhookEvent.ProcessedAt,
            Status = webhookEvent.Status.ToString()
        };

        await _hubContext.Clients.Group("all").SendAsync("WebhookProcessed", notification);
        await _hubContext.Clients.Group($"source:{webhookEvent.Source}").SendAsync("WebhookProcessed", notification);
    }

    public async Task NotifyWebhookFailedAsync(WebhookEvent webhookEvent, string error)
    {
        var notification = new
        {
            Type = "failed",
            EventId = webhookEvent.Id,
            Source = webhookEvent.Source,
            EventType = webhookEvent.EventType,
            Error = error,
            RetryCount = webhookEvent.RetryCount,
            Status = webhookEvent.Status.ToString()
        };

        await _hubContext.Clients.Group("all").SendAsync("WebhookFailed", notification);
        await _hubContext.Clients.Group($"source:{webhookEvent.Source}").SendAsync("WebhookFailed", notification);
    }
}
