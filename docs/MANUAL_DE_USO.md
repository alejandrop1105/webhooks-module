# 📚 Manual de Uso: Librería de Webhooks

Este documento describe cómo configurar y utilizar la librería de Webhooks para recibir notificaciones en tiempo real en tu sistema ERP (.NET / WinForms).

## 📋 Requisitos Previos

- **.NET 8 SDK** instalado.
- **SQLite** (incluido por defecto para desarrollo).
- **Cloudflared** (opcional, para exponer tu localhost a internet).

---

## 🚀 1. Configuración del Servidor (Webhooks.Api)

El servidor es una API REST que recibe los webhooks, los valida y los retransmite a los clientes conectados vía SignalR.

### Configuración (`appsettings.json`)

El archivo de configuración principal se encuentra en `src/Webhooks.Api/appsettings.json`.

```json
{
  "WebhookSettings": {
    "DefaultMaxRetries": 5,       // Intentos máximos de reenvío si falla el procesamiento
    "RetryDelaySeconds": 30       // Espera entre reintentos
  },
  "AllowedHosts": "*"             // Permitir conexiones desde cualquier host (útil para túneles)
}
```

### Ejecutar el Servidor

Desde la terminal en la carpeta raíz del proyecto:

```bash
cd src/Webhooks.Api
dotnet run
```

El servidor iniciará en:
- API: `http://localhost:5000`
- Swagger UI: `http://localhost:5000/swagger`
- Dashboard Hangfire: `http://localhost:5000/hangfire`
- **SignalR Hub**: `http://localhost:5000/hubs/webhooks`

---

## 💻 2. Integración con Cliente ERP (WinForms / WPF)

Para recibir notificaciones en tu aplicación de escritorio, debes implementar un cliente de SignalR.

### Paso 2.1: Instalar Dependencia

En tu proyecto cliente (WinForms/WPF), instala el paquete NuGet:

```bash
dotnet add package Microsoft.AspNetCore.SignalR.Client
```

### Paso 2.2: Implementar la Conexión (`MainForm.cs`)

Debes crear una instancia de `HubConnection` y suscribirte a los eventos.

**Ejemplo básico de implementación:**

```csharp
using Microsoft.AspNetCore.SignalR.Client;

public partial class MainForm : Form
{
    private HubConnection? _hubConnection;

    private async void BtnConnect_Click(object sender, EventArgs e)
    {
        // 1. Configurar la conexión
        _hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/hubs/webhooks") // URL del Hub
            .WithAutomaticReconnect() // Reconexión automática
            .Build();

        // 2. Definir manejadores de eventos
        _hubConnection.On<object>("WebhookReceived", (data) =>
        {
            // PROCESAR WEBHOOK RECIBIDO
            // data contiene: EventId, Source (woocommerce, stripe), EventType, Payload, etc.
            Invoke(() => Log($"Nuevo Evento: {data}"));
        });
        
        _hubConnection.On<object>("WebhookProcessed", (data) => 
        {
            // Notificación de que el worker procesó el evento exitosamente
            Invoke(() => Log("Evento Procesado Correctamente"));
        });

        _hubConnection.On<object>("WebhookFailed", (data) =>
        {
             // Notificación de error en procesamiento
             Invoke(() => Log("Error Procesando Evento"));
        });

        try
        {
            // 3. Iniciar conexión
            await _hubConnection.StartAsync();
            
            // 4. Suscribirse al grupo de notificaciones
            await _hubConnection.InvokeAsync("SubscribeToAll");
            
            Log("Conectado exitosamente");
        }
        catch (Exception ex)
        {
            Log($"Error de conexión: {ex.Message}");
        }
    }
}
```

### Eventos Disponibles

| Nombre del Evento | Descripción | Datos Recibidos |
|-------------------|-------------|-----------------|
| `WebhookReceived` | Se recibe un nuevo webhook en la API. | Objeto con `EventId`, `Source`, `EventType`. |
| `WebhookProcessed` | El worker procesó el evento correctamente. | Info del evento procesado. |
| `WebhookFailed` | El procesamiento falló (se reintentará). | Info del evento y mensaje de `Error`. |

---

## 🛠️ 3. Probando la Integración

### Usando el Simulador (WooCommerce)

El proyecto incluye un simulador para enviar eventos de prueba sin necesitar una tienda real.

1. Abre una nueva terminal.
2. Ejecuta el simulador:
   ```bash
   cd samples/WooCommerceSimulator
   dotnet run
   ```
3. El simulador enviará una serie de eventos (`order.created`, `product.updated`, etc.) a tu API local.
4. Si tu cliente WinForms está conectado, verás aparecer los eventos **instantáneamente**.

### Usando Webhooks Reales (Cloudflare Tunnel)

Para recibir eventos desde internet (ej. una tienda real de WooCommerce o Stripe):

1. Instala `cloudflared`.
2. Ejecuta el túnel:
   ```bash
   cloudflared tunnel --url http://localhost:5000
   ```
3. Copia la URL generada (ej. `https://mi-tunel.trycloudflare.com`).
4. Configura el webhook en tu proveedor externo usando esa URL:
   - URL: `https://mi-tunel.trycloudflare.com/api/webhooks/woocommerce` (o `/stripe`, `/github`).
   - Secret: Configura el mismo secreto en la base de datos `webhooks.db` (tabla `WebhookSources`).

---

## 🔍 Solución de Problemas comunes

- **Error de Conexión SignalR**: Asegúrate de que `Webhooks.Api` está corriendo y que la URL en el cliente es correcta (`/hubs/webhooks`).
- **No llegan eventos**: Verifica que te has suscrito llamando a `await _hubConnection.InvokeAsync("SubscribeToAll");` después de conectar.
- **CORS Error**: Si tu cliente es Web (Blazor/React), asegúrate de que el puerto del cliente esté permitido en `Program.cs` de la API (actualmente permite `AllowAll`).
