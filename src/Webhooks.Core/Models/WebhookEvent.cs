namespace Webhooks.Core.Models;

/// <summary>
/// Representa un evento de webhook recibido de un sistema externo.
/// </summary>
public class WebhookEvent
{
    /// <summary>
    /// Identificador único del evento.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Fuente del webhook (ej: "woocommerce", "stripe", etc.).
    /// </summary>
    public string Source { get; set; } = string.Empty;
    
    /// <summary>
    /// Tipo de evento (ej: "order.created", "payment.completed").
    /// </summary>
    public string EventType { get; set; } = string.Empty;
    
    /// <summary>
    /// Payload JSON completo del webhook.
    /// </summary>
    public string Payload { get; set; } = string.Empty;
    
    /// <summary>
    /// Headers HTTP de la solicitud original (serializado como JSON).
    /// </summary>
    public string Headers { get; set; } = string.Empty;
    
    /// <summary>
    /// Estado de procesamiento del evento.
    /// </summary>
    public WebhookEventStatus Status { get; set; } = WebhookEventStatus.Pending;
    
    /// <summary>
    /// Número de intentos de procesamiento.
    /// </summary>
    public int RetryCount { get; set; } = 0;
    
    /// <summary>
    /// Máximo de reintentos permitidos.
    /// </summary>
    public int MaxRetries { get; set; } = 5;
    
    /// <summary>
    /// Mensaje de error del último intento fallido.
    /// </summary>
    public string? LastError { get; set; }
    
    /// <summary>
    /// Fecha de recepción del webhook.
    /// </summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Fecha del último procesamiento.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }
    
    /// <summary>
    /// Firma HMAC recibida para validación.
    /// </summary>
    public string? Signature { get; set; }
    
    /// <summary>
    /// Indica si la firma fue validada correctamente.
    /// </summary>
    public bool SignatureValid { get; set; } = false;
}

/// <summary>
/// Estados posibles de un evento de webhook.
/// </summary>
public enum WebhookEventStatus
{
    /// <summary>Pendiente de procesar.</summary>
    Pending = 0,
    
    /// <summary>En proceso.</summary>
    Processing = 1,
    
    /// <summary>Procesado exitosamente.</summary>
    Completed = 2,
    
    /// <summary>Falló y será reintentado.</summary>
    Failed = 3,
    
    /// <summary>Falló permanentemente (dead letter).</summary>
    DeadLetter = 4
}
