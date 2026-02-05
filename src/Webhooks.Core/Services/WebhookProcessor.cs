using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Webhooks.Core.Data;
using Webhooks.Core.Interfaces;
using Webhooks.Core.Models;

namespace Webhooks.Core.Services;

/// <summary>
/// Servicio para procesar webhooks encolados.
/// </summary>
public class WebhookProcessor : IWebhookProcessor
{
    private readonly WebhookDbContext _dbContext;
    private readonly IEnumerable<IWebhookEventHandler> _handlers;
    private readonly ILogger<WebhookProcessor> _logger;

    public WebhookProcessor(
        WebhookDbContext dbContext,
        IEnumerable<IWebhookEventHandler> handlers,
        ILogger<WebhookProcessor> logger)
    {
        _dbContext = dbContext;
        _handlers = handlers;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ProcessAsync(Guid eventId)
    {
        var webhookEvent = await _dbContext.WebhookEvents
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (webhookEvent == null)
        {
            _logger.LogWarning("Evento {EventId} no encontrado", eventId);
            return;
        }

        if (webhookEvent.Status == WebhookEventStatus.Completed)
        {
            _logger.LogInformation("Evento {EventId} ya procesado", eventId);
            return;
        }

        _logger.LogInformation(
            "Procesando evento {EventId} de {Source} tipo {EventType}",
            eventId, webhookEvent.Source, webhookEvent.EventType);

        try
        {
            webhookEvent.Status = WebhookEventStatus.Processing;
            await _dbContext.SaveChangesAsync();

            // Buscar handlers compatibles
            var compatibleHandlers = _handlers
                .Where(h => h.Source == webhookEvent.Source || h.Source == "*")
                .Where(h => h.EventTypes.Contains("*") || h.EventTypes.Contains(webhookEvent.EventType));

            foreach (var handler in compatibleHandlers)
            {
                _logger.LogDebug(
                    "Ejecutando handler {Handler} para evento {EventId}",
                    handler.GetType().Name, eventId);

                await handler.HandleAsync(webhookEvent);
            }

            webhookEvent.Status = WebhookEventStatus.Completed;
            webhookEvent.ProcessedAt = DateTime.UtcNow;

            _logger.LogInformation("Evento {EventId} procesado exitosamente", eventId);
        }
        catch (Exception ex)
        {
            webhookEvent.RetryCount++;
            webhookEvent.LastError = ex.Message;

            if (webhookEvent.RetryCount >= webhookEvent.MaxRetries)
            {
                webhookEvent.Status = WebhookEventStatus.DeadLetter;
                _logger.LogError(ex,
                    "Evento {EventId} enviado a dead letter después de {Retries} intentos",
                    eventId, webhookEvent.RetryCount);
            }
            else
            {
                webhookEvent.Status = WebhookEventStatus.Failed;
                _logger.LogWarning(ex,
                    "Error procesando evento {EventId}, reintento {Retry}/{MaxRetries}",
                    eventId, webhookEvent.RetryCount, webhookEvent.MaxRetries);

                // Re-lanzar para que Hangfire reintente
                await _dbContext.SaveChangesAsync();
                throw;
            }
        }

        await _dbContext.SaveChangesAsync();
    }
}
