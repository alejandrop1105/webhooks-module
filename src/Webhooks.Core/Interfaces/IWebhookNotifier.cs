using Webhooks.Core.Models;

namespace Webhooks.Core.Interfaces;

/// <summary>
/// Interfaz para notificar eventos de webhooks en tiempo real.
/// </summary>
public interface IWebhookNotifier
{
    /// <summary>
    /// Notifica a todos los clientes conectados que se recibió un webhook.
    /// </summary>
    Task NotifyWebhookReceivedAsync(WebhookEvent webhookEvent);

    /// <summary>
    /// Notifica a todos los clientes conectados que se procesó un webhook.
    /// </summary>
    Task NotifyWebhookProcessedAsync(WebhookEvent webhookEvent);

    /// <summary>
    /// Notifica a todos los clientes conectados que falló el procesamiento.
    /// </summary>
    Task NotifyWebhookFailedAsync(WebhookEvent webhookEvent, string error);
}
