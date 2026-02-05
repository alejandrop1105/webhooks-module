using Microsoft.EntityFrameworkCore;
using Webhooks.Core.Models;

namespace Webhooks.Core.Data;

/// <summary>
/// Contexto de base de datos para el sistema de webhooks.
/// </summary>
public class WebhookDbContext : DbContext
{
    public WebhookDbContext(DbContextOptions<WebhookDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Eventos de webhook recibidos.
    /// </summary>
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    /// <summary>
    /// Fuentes de webhook configuradas.
    /// </summary>
    public DbSet<WebhookSource> WebhookSources => Set<WebhookSource>();

    /// <summary>
    /// Suscripciones para emisión de webhooks.
    /// </summary>
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración de WebhookEvent
        modelBuilder.Entity<WebhookEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Source);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ReceivedAt);
            entity.Property(e => e.Payload).IsRequired();
        });

        // Configuración de WebhookSource
        modelBuilder.Entity<WebhookSource>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configuración de WebhookSubscription
        modelBuilder.Entity<WebhookSubscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.IsActive);
        });

        // Seed data: fuente WooCommerce por defecto
        modelBuilder.Entity<WebhookSource>().HasData(
            new WebhookSource
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "woocommerce",
                Description = "WooCommerce Webhooks",
                SignatureHeader = "X-WC-Webhook-Signature",
                SignatureAlgorithm = "HMAC-SHA256",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        );
    }
}
