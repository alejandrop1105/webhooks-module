using System.Text.Json;

Console.WriteLine("===========================================");
Console.WriteLine("  Sample ERP Integration - Webhook Listener");
Console.WriteLine("===========================================");
Console.WriteLine();

// Este ejemplo muestra cómo tu ERP consumiría los eventos
// En la práctica, usarías un IWebhookEventHandler en el Core

var apiUrl = args.Length > 0 ? args[0] : "http://localhost:5000";

Console.WriteLine($"Conectando a: {apiUrl}");
Console.WriteLine("Presiona Ctrl+C para salir");
Console.WriteLine();

using var httpClient = new HttpClient { BaseAddress = new Uri(apiUrl) };

// Polling simple para demostración
// En producción usarías SignalR, gRPC streaming, o message queue
while (true)
{
    try
    {
        var response = await httpClient.GetAsync("/api/events?limit=10");

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var events = JsonSerializer.Deserialize<List<WebhookEventDto>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (events?.Count > 0)
            {
                Console.Clear();
                Console.WriteLine($"=== Últimos eventos ({DateTime.Now:HH:mm:ss}) ===\n");

                foreach (var evt in events)
                {
                    var statusIcon = evt.Status switch
                    {
                        "Pending" => "⏳",
                        "Processing" => "🔄",
                        "Completed" => "✅",
                        "Failed" => "❌",
                        "DeadLetter" => "💀",
                        _ => "❓"
                    };

                    Console.WriteLine($"{statusIcon} [{evt.Source}] {evt.EventType}");
                    Console.WriteLine($"   ID: {evt.Id}");
                    Console.WriteLine($"   Recibido: {evt.ReceivedAt:dd/MM/yyyy HH:mm:ss}");
                    Console.WriteLine($"   Firma válida: {(evt.SignatureValid ? "Sí" : "No")}");

                    // Mostrar payload resumido
                    if (!string.IsNullOrEmpty(evt.Payload))
                    {
                        var payloadPreview = evt.Payload.Length > 80
                            ? evt.Payload[..80] + "..."
                            : evt.Payload;
                        Console.WriteLine($"   Payload: {payloadPreview}");
                    }

                    Console.WriteLine();
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error conectando: {ex.Message}");
    }

    await Task.Delay(2000); // Polling cada 2 segundos
}

// DTO para deserializar eventos
record WebhookEventDto(
    Guid Id,
    string Source,
    string EventType,
    string Payload,
    string Status,
    int RetryCount,
    DateTime ReceivedAt,
    DateTime? ProcessedAt,
    bool SignatureValid,
    string? LastError
);
