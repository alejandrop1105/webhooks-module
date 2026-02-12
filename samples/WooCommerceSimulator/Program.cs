using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

Console.WriteLine("===========================================");
Console.WriteLine("  WooCommerce Webhook Simulator");
Console.WriteLine("===========================================");
Console.WriteLine();

// Configuración
var webhookUrl = args.Length > 0 ? args[0] : "http://124.0.0.24:500";
//    "https://tests-agrees-encountered-emotional.trycloudflare.com/api/webhooks/woocommerce";
// "http://localhost:5000/api/webhooks/woocommerce";
var secretKey = args.Length > 1 ? args[1] : "mi_clave_secreta";

Console.WriteLine($"URL del webhook: {webhookUrl}");
Console.WriteLine($"Clave secreta: {secretKey}");
Console.WriteLine();

// Crear cliente HTTP
using var httpClient = new HttpClient();

// Menú de eventos
while (true)
{
    Console.WriteLine("Selecciona el tipo de evento a simular:");
    Console.WriteLine("1. order.created - Nueva orden");
    Console.WriteLine("2. order.updated - Orden actualizada");
    Console.WriteLine("3. product.created - Nuevo producto");
    Console.WriteLine("4. customer.created - Nuevo cliente");
    Console.WriteLine("5. Enviar evento personalizado");
    Console.WriteLine("0. Salir");
    Console.WriteLine();
    Console.Write("Opción: ");

    var option = Console.ReadLine();

    if (option == "0") break;

    var (eventType, payload) = option switch
    {
        "1" => ("order.created", CreateOrderPayload("created")),
        "2" => ("order.updated", CreateOrderPayload("updated")),
        "3" => ("product.created", CreateProductPayload()),
        "4" => ("customer.created", CreateCustomerPayload()),
        "5" => GetCustomEvent(),
        _ => ("unknown", "{}")
    };

    await SendWebhook(httpClient, webhookUrl, eventType, payload, secretKey);
    Console.WriteLine();
}

Console.WriteLine("¡Hasta luego!");

// ========================================
// FUNCIONES
// ========================================

static async Task SendWebhook(HttpClient client, string url, string eventType, string payload, string secret)
{
    Console.WriteLine($"\nEnviando evento: {eventType}");
    Console.WriteLine($"Payload: {payload[..Math.Min(100, payload.Length)]}...");

    // Calcular firma HMAC
    var signature = ComputeHmacSignature(payload, secret);

    // Crear request
    var request = new HttpRequestMessage(HttpMethod.Post, url)
    {
        Content = new StringContent(payload, Encoding.UTF8, "application/json")
    };

    // Headers de WooCommerce
    request.Headers.Add("X-WC-Webhook-Topic", eventType);
    request.Headers.Add("X-WC-Webhook-Signature", signature);
    request.Headers.Add("X-WC-Webhook-Source", "https://mitienda.com/");
    request.Headers.Add("X-WC-Webhook-ID", Guid.NewGuid().ToString());
    request.Headers.Add("X-WC-Webhook-Delivery-ID", Guid.NewGuid().ToString());

    try
    {
        var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"Respuesta: {response.StatusCode}");
        Console.WriteLine($"Body: {responseBody}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}

static string ComputeHmacSignature(string payload, string secret)
{
    var keyBytes = Encoding.UTF8.GetBytes(secret);
    var payloadBytes = Encoding.UTF8.GetBytes(payload);
    var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
    return Convert.ToBase64String(hash);
}

static string CreateOrderPayload(string status)
{
    var order = new
    {
        id = Random.Shared.Next(1000, 9999),
        status = status == "created" ? "pending" : "processing",
        currency = "ARS",
        total = Math.Round(Random.Shared.NextDouble() * 50000 + 1000, 2).ToString("F2"),
        customer_id = Random.Shared.Next(1, 100),
        billing = new
        {
            first_name = "Juan",
            last_name = "Pérez",
            email = "juan@email.com",
            phone = "+5491123456789"
        },
        line_items = new[]
        {
            new
            {
                id = Random.Shared.Next(1, 1000),
                name = "Producto de prueba",
                quantity = Random.Shared.Next(1, 5),
                price = Math.Round(Random.Shared.NextDouble() * 10000 + 500, 2).ToString("F2")
            }
        },
        date_created = DateTime.UtcNow.ToString("O")
    };

    return JsonSerializer.Serialize(order, new JsonSerializerOptions { WriteIndented = false });
}

static string CreateProductPayload()
{
    var product = new
    {
        id = Random.Shared.Next(1000, 9999),
        name = $"Producto {Random.Shared.Next(1, 100)}",
        slug = $"producto-{Random.Shared.Next(1, 100)}",
        type = "simple",
        status = "publish",
        regular_price = Math.Round(Random.Shared.NextDouble() * 10000 + 500, 2).ToString("F2"),
        stock_quantity = Random.Shared.Next(0, 100),
        categories = new[] { new { id = 1, name = "General" } }
    };

    return JsonSerializer.Serialize(product, new JsonSerializerOptions { WriteIndented = false });
}

static string CreateCustomerPayload()
{
    var customer = new
    {
        id = Random.Shared.Next(1000, 9999),
        email = $"cliente{Random.Shared.Next(1, 1000)}@email.com",
        first_name = "Cliente",
        last_name = $"Número {Random.Shared.Next(1, 1000)}",
        role = "customer",
        date_created = DateTime.UtcNow.ToString("O")
    };

    return JsonSerializer.Serialize(customer, new JsonSerializerOptions { WriteIndented = false });
}

static (string eventType, string payload) GetCustomEvent()
{
    Console.Write("Tipo de evento: ");
    var eventType = Console.ReadLine() ?? "custom.event";

    Console.Write("Payload JSON (o Enter para payload vacío): ");
    var payload = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(payload))
    {
        payload = JsonSerializer.Serialize(new { message = "Evento personalizado", timestamp = DateTime.UtcNow });
    }

    return (eventType, payload);
}
