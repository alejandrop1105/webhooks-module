using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Webhooks.Core.Data;
using Webhooks.Core.Interfaces;
using Webhooks.Core.Models;

namespace Webhooks.Core.Services;

/// <summary>
/// Servicio para recibir y encolar webhooks.
/// </summary>
public class WebhookReceiver : IWebhookReceiver
{
    private readonly WebhookDbContext _dbContext;
    private readonly ISignatureValidator _signatureValidator;
    private readonly ILogger<WebhookReceiver> _logger;

    public WebhookReceiver(
        WebhookDbContext dbContext,
        ISignatureValidator signatureValidator,
        ILogger<WebhookReceiver> logger)
    {
        _dbContext = dbContext;
        _signatureValidator = signatureValidator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Guid> ReceiveAsync(
        string source,
        string eventType,
        string payload,
        Dictionary<string, string> headers,
        string? signature = null)
    {
        _logger.LogInformation(
            "Recibiendo webhook de {Source} tipo {EventType}",
            source, eventType);

        // Buscar configuración de la fuente
        var webhookSource = await _dbContext.WebhookSources
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == source && s.IsActive);

        // Validar firma si hay clave configurada
        bool signatureValid = false;
        if (webhookSource?.SecretKey != null && signature != null)
        {
            signatureValid = _signatureValidator.Validate(
                payload,
                signature,
                webhookSource.SecretKey,
                webhookSource.SignatureAlgorithm);

            if (!signatureValid)
            {
                _logger.LogWarning(
                    "Firma inválida para webhook de {Source}", source);
            }
        }

        // Crear evento
        var webhookEvent = new WebhookEvent
        {
            Source = source,
            EventType = eventType,
            Payload = payload,
            Headers = JsonSerializer.Serialize(headers),
            Signature = signature,
            SignatureValid = signatureValid,
            Status = WebhookEventStatus.Pending,
            ReceivedAt = DateTime.UtcNow
        };

        _dbContext.WebhookEvents.Add(webhookEvent);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Webhook encolado con ID {EventId} de {Source}",
            webhookEvent.Id, source);

        return webhookEvent.Id;
    }
}
