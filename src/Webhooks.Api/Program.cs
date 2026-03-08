using Hangfire;
using Hangfire.Storage.SQLite;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Webhooks.Api.Hubs;
using Webhooks.Core.Data;
using Webhooks.Core.Interfaces;
using Webhooks.Core.Services;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/webhooks-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Usar Serilog
builder.Host.UseSerilog();

// Configurar base de datos SQLite
var dbPath = Path.Combine(AppContext.BaseDirectory, "webhooks.db");
builder.Services.AddDbContext<WebhookDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Configurar Hangfire con SQLite
var hangfireDbPath = Path.Combine(AppContext.BaseDirectory, "hangfire.db");
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSQLiteStorage(hangfireDbPath, new SQLiteStorageOptions
    {
        QueuePollInterval = TimeSpan.FromSeconds(1)
    }));
builder.Services.AddHangfireServer();

// Registrar servicios
builder.Services.AddScoped<ISignatureValidator, SignatureValidator>();
builder.Services.AddScoped<IWebhookReceiver, WebhookReceiver>();
builder.Services.AddScoped<IWebhookProcessor, WebhookProcessor>();
builder.Services.AddScoped<IWebhookNotifier, SignalRWebhookNotifier>();

// SignalR para notificaciones en tiempo real
builder.Services.AddSignalR();

// CORS para permitir conexiones desde WinForms
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials();
    });
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Webhooks API", Version = "v1" });
});

var app = builder.Build();

// Habilitar CORS
app.UseCors("AllowAll");

// Crear base de datos si no existe
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();
    db.Database.EnsureCreated();
}

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Dashboard de Hangfire (solo en desarrollo)
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire");
}

// ========================================
// ENDPOINTS DE WEBHOOKS
// ========================================

/// <summary>
/// Endpoint genérico para recibir webhooks.
/// Ruta: POST /api/webhooks/{source}
/// </summary>
app.MapPost("/api/webhooks/{source}", async (
    string source,
    HttpRequest request,
    IWebhookReceiver receiver,
    IBackgroundJobClient backgroundJobs) =>
{
    // Leer el body
    using var reader = new StreamReader(request.Body);
    var payload = await reader.ReadToEndAsync();

    // Extraer headers relevantes
    var headers = new Dictionary<string, string>();
    foreach (var header in request.Headers)
    {
        headers[header.Key] = header.Value.ToString();
    }

    // Determinar tipo de evento según la fuente
    string eventType = DetermineEventType(source, request.Headers);

    // Obtener firma si existe
    string? signature = GetSignature(source, request.Headers);

    // Recibir y encolar
    var eventId = await receiver.ReceiveAsync(source, eventType, payload, headers, signature);

    // Encolar para procesamiento con Hangfire
    backgroundJobs.Enqueue<IWebhookProcessor>(p => p.ProcessAsync(eventId));

    return Results.Ok(new { eventId, message = "Webhook recibido y encolado" });
})
.WithName("ReceiveWebhook")
.WithOpenApi()
.WithDescription("Recibe un webhook de cualquier fuente y lo encola para procesamiento");

/// <summary>
/// Endpoint de health check.
/// </summary>
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithOpenApi();

/// <summary>
/// Obtener todos los eventos (para debug).
/// </summary>
app.MapGet("/api/events", async (WebhookDbContext db, int? limit) =>
{
    var events = await db.WebhookEvents
        .OrderByDescending(e => e.ReceivedAt)
        .Take(limit ?? 50)
        .ToListAsync();
    return Results.Ok(events);
})
.WithName("GetEvents")
.WithOpenApi();

/// <summary>
/// Obtener un evento específico.
/// </summary>
app.MapGet("/api/events/{id:guid}", async (Guid id, WebhookDbContext db) =>
{
    var evt = await db.WebhookEvents.FindAsync(id);
    return evt is null ? Results.NotFound() : Results.Ok(evt);
})
.WithName("GetEvent")
.WithOpenApi();

/// <summary>
/// Reprocesar un evento fallido.
/// </summary>
app.MapPost("/api/events/{id:guid}/retry", async (
    Guid id,
    WebhookDbContext db,
    IBackgroundJobClient backgroundJobs) =>
{
    var evt = await db.WebhookEvents.FindAsync(id);
    if (evt is null) return Results.NotFound();

    evt.Status = Webhooks.Core.Models.WebhookEventStatus.Pending;
    evt.RetryCount = 0;
    await db.SaveChangesAsync();

    backgroundJobs.Enqueue<IWebhookProcessor>(p => p.ProcessAsync(id));

    return Results.Ok(new { message = "Evento reencolado" });
})
.WithName("RetryEvent")
.WithOpenApi();

// Mapear SignalR Hub
app.MapHub<WebhookHub>("/hubs/webhooks");

app.Run();

// ========================================
// FUNCIONES AUXILIARES
// ========================================

static string DetermineEventType(string source, IHeaderDictionary headers)
{
    return source.ToLower() switch
    {
        "woocommerce" => headers["X-WC-Webhook-Topic"].ToString() ?? "unknown",
        "stripe" => headers["Stripe-Event"].ToString() ?? "unknown",
        "github" => headers["X-GitHub-Event"].ToString() ?? "unknown",
        "shopify" => headers["X-Shopify-Topic"].ToString() ?? "unknown",
        _ => headers["X-Event-Type"].ToString() ?? "unknown"
    };
}

static string? GetSignature(string source, IHeaderDictionary headers)
{
    return source.ToLower() switch
    {
        "woocommerce" => headers["X-WC-Webhook-Signature"].ToString(),
        "stripe" => headers["Stripe-Signature"].ToString(),
        "github" => headers["X-Hub-Signature-256"].ToString(),
        "shopify" => headers["X-Shopify-Hmac-Sha256"].ToString(),
        _ => headers["X-Webhook-Signature"].ToString()
    };
}
