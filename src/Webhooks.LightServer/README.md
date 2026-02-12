# ⚡ Webhooks.LightServer

Una librería ligera (.NET Standard / .NET 8) para levantar un servidor de webhooks dedicado dentro de tu propia aplicación (ERP, Servicio de Windows, Console App), sin dependencias de ASP.NET Core Hosting completo ni SignalR.

## 🎯 Objetivo

Simpleza absoluta. Levantar un puerto, escuchar POSTs, validar firma HMAC, y disparar un evento C# dentro de tu proceso.

## 📦 Instalación

Referencia este proyecto (`src/Webhooks.LightServer`) en tu solución.

## 💻 Uso Básico

```csharp
using Webhooks.LightServer;

// 1. Configurar
int puerto = 8080;
string secreto = "tu_clave_secreta"; // Para validar HMAC SHA256

// 2. Instanciar
using var server = new SimpleWebhookServer(puerto, secreto);

// 3. Suscribirse a eventos
server.OnWebhookReceived += (sender, e) =>
{
    Console.WriteLine($"Recibido de: {e.Source}"); // ej: "woocommerce"
    Console.WriteLine($"Payload: {e.Payload}");
};

server.OnLog += (s, msg) => Console.WriteLine(msg);

// 4. Iniciar
server.Start();

// El servidor escuchará en: http://localhost:8080/webhook/{source}
// Ejemplo: POST http://localhost:8080/webhook/woocommerce

Console.ReadLine(); // Mantener vivo
```

## 🔒 Seguridad

La librería valida automáticamente las firmas HMAC-SHA256 usando headers estándar:
- `X-WC-Webhook-Signature` (WooCommerce)
- `X-Hub-Signature-256` (GitHub)
- `Stripe-Signature` (Stripe - validación básica)

Si la firma no coincide con tu `secreto`, el servidor responde `401 Unauthorized` y **no dispara el evento**, protegiendo tu ERP de datos falsos.

## 🌐 Exponer a Internet (Cloudflare Tunnel)

Si usas Cloudflare Tunnel, apunta tu túnel a este puerto local:

```bash
cloudflared tunnel --url http://localhost:8080
```

Tu URL pública será algo como `https://mi-tunel.trycloudflare.com`.
El webhook debe configurarse hacia: `https://mi-tunel.trycloudflare.com/webhook/woocommerce`

---
**Nota**: Esta librería usa `HttpListener`, que es parte nativa de .NET. No requiere Kestrel ni IIS.
