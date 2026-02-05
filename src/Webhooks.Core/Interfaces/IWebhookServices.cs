using Webhooks.Core.Models;

namespace Webhooks.Core.Interfaces;

/// <summary>
/// Interfaz para el servicio de recepción de webhooks.
/// </summary>
public interface IWebhookReceiver
{
    /// <summary>
    /// Recibe y encola un webhook para procesamiento asíncrono.
    /// </summary>
    /// <param name="source">Nombre de la fuente (ej: "woocommerce").</param>
    /// <param name="eventType">Tipo de evento.</param>
    /// <param name="payload">Cuerpo JSON del webhook.</param>
    /// <param name="headers">Headers de la solicitud.</param>
    /// <param name="signature">Firma para validación.</param>
    /// <returns>ID del evento creado.</returns>
    Task<Guid> ReceiveAsync(
        string source,
        string eventType,
        string payload,
        Dictionary<string, string> headers,
        string? signature = null);
}

/// <summary>
/// Interfaz para el procesador de webhooks.
/// </summary>
public interface IWebhookProcessor
{
    /// <summary>
    /// Procesa un evento de webhook encolado.
    /// </summary>
    /// <param name="eventId">ID del evento a procesar.</param>
    Task ProcessAsync(Guid eventId);
}

/// <summary>
/// Interfaz para validar firmas de webhooks.
/// </summary>
public interface ISignatureValidator
{
    /// <summary>
    /// Valida la firma de un webhook.
    /// </summary>
    /// <param name="payload">Payload recibido.</param>
    /// <param name="signature">Firma recibida.</param>
    /// <param name="secretKey">Clave secreta.</param>
    /// <param name="algorithm">Algoritmo (ej: "HMAC-SHA256").</param>
    /// <returns>True si la firma es válida.</returns>
    bool Validate(string payload, string signature, string secretKey, string algorithm);
}

/// <summary>
/// Interfaz para handlers de eventos de webhook.
/// Implementa este interfaz para procesar eventos específicos.
/// </summary>
public interface IWebhookEventHandler
{
    /// <summary>
    /// Fuente que maneja este handler.
    /// </summary>
    string Source { get; }

    /// <summary>
    /// Tipos de eventos que maneja (o "*" para todos).
    /// </summary>
    IEnumerable<string> EventTypes { get; }

    /// <summary>
    /// Procesa el evento.
    /// </summary>
    /// <param name="webhookEvent">Evento a procesar.</param>
    Task HandleAsync(WebhookEvent webhookEvent);
}
