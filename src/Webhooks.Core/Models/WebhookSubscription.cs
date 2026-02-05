namespace Webhooks.Core.Models;

/// <summary>
/// Suscripción de webhook para emisión futura.
/// Permite registrar endpoints que recibirán eventos de este sistema.
/// </summary>
public class WebhookSubscription
{
    /// <summary>
    /// Identificador único de la suscripción.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Nombre descriptivo de la suscripción.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL destino donde enviar los webhooks.
    /// </summary>
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>
    /// Clave secreta para firmar los webhooks salientes.
    /// </summary>
    public string? SecretKey { get; set; }

    /// <summary>
    /// Tipos de eventos a los que está suscrito (separados por coma).
    /// Ej: "order.created,order.updated"
    /// </summary>
    public string EventTypes { get; set; } = "*";

    /// <summary>
    /// Si la suscripción está activa.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Fecha de expiración de la suscripción (null = no expira).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Fecha de creación.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Última vez que se envió un webhook exitosamente.
    /// </summary>
    public DateTime? LastDeliveryAt { get; set; }

    /// <summary>
    /// Número de entregas exitosas.
    /// </summary>
    public int SuccessfulDeliveries { get; set; } = 0;

    /// <summary>
    /// Número de entregas fallidas.
    /// </summary>
    public int FailedDeliveries { get; set; } = 0;
}
