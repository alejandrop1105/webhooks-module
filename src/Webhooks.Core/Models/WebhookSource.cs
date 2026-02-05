namespace Webhooks.Core.Models;

/// <summary>
/// Configuración de una fuente de webhooks (ej: WooCommerce).
/// </summary>
public class WebhookSource
{
    /// <summary>
    /// Identificador único de la fuente.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Nombre de la fuente (ej: "woocommerce", "stripe").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Descripción de la fuente.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Clave secreta para validar firmas HMAC.
    /// </summary>
    public string? SecretKey { get; set; }

    /// <summary>
    /// Nombre del header que contiene la firma.
    /// </summary>
    public string SignatureHeader { get; set; } = "X-Webhook-Signature";

    /// <summary>
    /// Algoritmo de firma (ej: "HMAC-SHA256").
    /// </summary>
    public string SignatureAlgorithm { get; set; } = "HMAC-SHA256";

    /// <summary>
    /// Si está activa esta fuente.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Fecha de creación.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fecha de última modificación.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
