# 📬 Webhooks.LegacyEngine — Guía de Uso v1

> Guía completa para configurar, integrar y operar la DLL `Webhooks.LegacyEngine` en cualquier sistema .NET Framework 4.8.

---

## 📋 Índice

1. [Requisitos Previos](#-requisitos-previos)
2. [Configuración de Cloudflare (una sola vez)](#-configuración-de-cloudflare-una-sola-vez)
3. [Integración en tu Proyecto](#-integración-en-tu-proyecto)
4. [Referencia de API](#-referencia-de-api)
5. [Operaciones de Mantenimiento](#-operaciones-de-mantenimiento)
6. [Troubleshooting](#-troubleshooting)

---

## 🔧 Requisitos Previos

| Requisito                 | Detalle                                                       |
| ------------------------- | ------------------------------------------------------------- |
| **.NET Framework**        | 4.8 o superior                                                |
| **Cuenta Cloudflare**     | Gratuita — [dash.cloudflare.com](https://dash.cloudflare.com) |
| **Dominio en Cloudflare** | Registrado o migrado al DNS de Cloudflare                     |
| **cloudflared.exe**       | Se auto-descarga automáticamente si no está presente          |

---

## ☁️ Configuración de Cloudflare (una sola vez)

### Paso 1: Obtener Account ID y Zone ID

1. Ir a [dash.cloudflare.com](https://dash.cloudflare.com)
2. Click en tu dominio (ej: `dotarsoluciones.com`)
3. En la página **Overview**, columna derecha, sección **API**:
   - **Zone ID**: primer valor hexadecimal de 32 caracteres
   - **Account ID**: justo debajo

### Paso 2: Crear un API Token

1. Ir a [dash.cloudflare.com/profile/api-tokens](https://dash.cloudflare.com/profile/api-tokens)
2. Click en **"Create Token"**
3. Click en **"Create Custom Token"** (al final de la lista)
4. Configurar:

| Campo                 | Valor                                          |
| --------------------- | ---------------------------------------------- |
| **Token name**        | `webhooks-engine` (o el nombre que prefieras)  |
| **Permissions**       | `Account` → `Cloudflare Tunnel` → `Edit`       |
|                       | `Zone` → `DNS` → `Edit`                        |
| **Account Resources** | Seleccionar tu cuenta específica               |
| **Zone Resources**    | Seleccionar `Specific zone` → `tu-dominio.com` |

5. Click en **"Continue to summary"** → **"Create Token"**
6. **Copiar el token** (solo se muestra una vez)

> ⚠️ **Importante**: Este token permite crear túneles y registros DNS. No da acceso a facturación, usuarios ni otros dominios. Guardalo de forma segura.

### Paso 3: Elegir un nombre de subdominio

Cada instalación del ERP necesita un nombre único. Ejemplos:
- `erp-sucursal1` → `erp-sucursal1.dotarsoluciones.com`
- `webhook-cliente-abc` → `webhook-cliente-abc.dotarsoluciones.com`

---

## 🔌 Integración en tu Proyecto

### 1. Agregar referencia a la DLL

```
Webhooks.LegacyEngine.dll
```

Dependencias NuGet requeridas (se incluyen automáticamente):
- `Microsoft.AspNet.WebApi.OwinSelfHost` (5.3.0)
- `Microsoft.Owin.Cors` (4.2.3)
- `Microsoft.Owin.Host.HttpListener` (4.2.2)
- `Newtonsoft.Json` (13.0.3)
- `Serilog` (4.3.1)
- `Serilog.Sinks.File` (7.0.0)

### 2. Código mínimo de integración

```csharp
using Webhooks.LegacyEngine.Engine;
using Webhooks.LegacyEngine.Managers;

// --- En tu Form o Servicio ---

// 1. Crear el motor
var engine = new WebhookHost(port: 5000);

// 2. Suscribirse a webhooks entrantes
engine.OnWebhookReceived += (sender, e) =>
{
    Console.WriteLine($"Webhook de [{e.Source}]: {e.RawJson}");
    // Aquí procesás el JSON según tu lógica de negocio
};

// 3. Suscribirse a eventos de estado (opcional)
engine.OnServerStatusChanged += (s, status) => Console.WriteLine($"API: {status}");
engine.OnTunnelStatusChanged += (s, status) => Console.WriteLine($"Tunnel: {status}");
engine.OnTunnelUrlGenerated += (s, url) => Console.WriteLine($"URL pública: {url}");
engine.OnSystemLog += (s, log) => Console.WriteLine($"[{log.Level}] {log.Message}");

// 4. Configurar Cloudflare (antes de iniciar con túnel)
engine.SetCloudflareConfig(new CloudflareConfig
{
    TunnelName = "erp-sucursal1",           // Nombre único de esta instalación
    Domain     = "dotarsoluciones.com",      // Tu dominio en Cloudflare
    ApiToken   = "tu-api-token-aquí",        // Token creado en Paso 2
    AccountId  = "tu-account-id",            // Del Paso 1
    ZoneId     = "tu-zone-id"                // Del Paso 1
});

// 5a. Iniciar SOLO servidor local (sin tunnel)
engine.Start(enableTunnel: false);

// 5b. O iniciar con tunnel (expone a internet)
engine.Start(enableTunnel: true);

// 6. Al cerrar la aplicación
engine.Dispose(); // Detiene todo y mata el proceso cloudflared
```

### 3. Recepción de webhooks

Una vez iniciado, la DLL escucha en:
- **Local**: `http://localhost:5000/api/webhooks/{source}`
- **Público**: `https://{tunnelName}.{domain}/api/webhooks/{source}`

El parámetro `{source}` es libre — puede ser cualquier string que identifique el origen:
- `/api/webhooks/woocommerce`
- `/api/webhooks/stripe`
- `/api/webhooks/mercadopago`
- `/api/webhooks/mi-sistema`

Endpoint de diagnóstico:
- **Ping**: `GET /api/webhooks/ping` → responde `"pong"`

---

## 📖 Referencia de API

### WebhookHost

| Método                        | Descripción                                                        |
| ----------------------------- | ------------------------------------------------------------------ |
| `SetCloudflareConfig(config)` | Configura las credenciales de Cloudflare. Llamar antes de `Start`. |
| `Start(enableTunnel)`         | Arranca el servidor OWIN. Si `enableTunnel=true`, inicia el túnel. |
| `Stop()`                      | Detiene servidor y túnel. No elimina configuración.                |
| `ResetTunnel()`               | Elimina `tunnel_config.json` y `cloudflared.exe` local.            |
| `Dispose()`                   | Llama `Stop()` y libera recursos.                                  |

### Eventos

| Evento                  | Tipo                      | Cuándo se dispara                                        |
| ----------------------- | ------------------------- | -------------------------------------------------------- |
| `OnWebhookReceived`     | `WebhookPayloadEventArgs` | Llega un webhook (contiene `Source` y `RawJson`)         |
| `OnServerStatusChanged` | `string`                  | Cambios de estado del servidor HTTP                      |
| `OnTunnelStatusChanged` | `string`                  | Cambios de estado del túnel Cloudflare                   |
| `OnTunnelUrlGenerated`  | `string`                  | URL pública disponible (ej: `https://erp-x.dominio.com`) |
| `OnSystemLog`           | `LogEventArgs`            | Logs internos de Serilog (contiene `Level` y `Message`)  |

### CloudflareConfig

| Propiedad    | Tipo     | Descripción                                            |
| ------------ | -------- | ------------------------------------------------------ |
| `TunnelName` | `string` | Nombre del subdominio (ej: `erp-cliente1`)             |
| `Domain`     | `string` | Dominio base (ej: `dotarsoluciones.com`)               |
| `ApiToken`   | `string` | Token API de Cloudflare                                |
| `AccountId`  | `string` | Account ID de Cloudflare                               |
| `ZoneId`     | `string` | Zone ID de la zona DNS                                 |
| `Hostname`   | `string` | (Calculado) `{TunnelName}.{Domain}`                    |
| `IsValid`    | `bool`   | (Calculado) `true` si todos los campos están completos |

---

## 🔄 Operaciones de Mantenimiento

### Reset (reinstalación limpia)

```csharp
engine.ResetTunnel();
```

Esto elimina:
- ✅ `tunnel_config.json` (configuración local del túnel)
- ✅ `cloudflared.exe` (se re-descargará la próxima vez)
- ❌ **NO elimina** el túnel en Cloudflare (se reutiliza automáticamente)

### Ciclo de vida del túnel

| Acción                  | Resultado                                                                 |
| ----------------------- | ------------------------------------------------------------------------- |
| Primera ejecución       | Auto-descarga cloudflared → Crea túnel vía API → Crea DNS → Guarda config |
| Siguientes ejecuciones  | Lee config local → Ejecuta túnel directo                                  |
| App se cierra           | Proceso cloudflared muere automáticamente                                 |
| Reset + nueva ejecución | Re-descarga cloudflared → Reutiliza túnel existente                       |

### Archivos generados

| Archivo                     | Ubicación     | Contenido                      |
| --------------------------- | ------------- | ------------------------------ |
| `tunnel_config.json`        | Junto al .exe | ID, token y hostname del túnel |
| `cloudflared.exe`           | Junto al .exe | Binario de Cloudflare (~60MB)  |
| `logs/webhook-engine-*.txt` | Junto al .exe | Logs rotativos (5 días)        |

---

## ❓ Troubleshooting

### Error 400 Bad Request

**Causa**: OWIN/HttpListener rechaza requests con Host header diferente a `localhost`.
**Solución**: Esto se maneja automáticamente con `httpHostHeader: localhost` en la configuración de ingress. Si persiste, ejecutar Reset y reiniciar.

### El túnel no conecta

```powershell
# Verificar que no haya procesos zombie
Get-Process cloudflared -ErrorAction SilentlyContinue

# Verificar conectividad a Cloudflare
Test-NetConnection -ComputerName dash.cloudflare.com -Port 443
```

### Error "Configuración de Cloudflare incompleta"

Verificar que los 5 campos estén completos:
1. **TunnelName**: Sin espacios, solo letras, números y guiones
2. **Domain**: Dominio completo registrado en Cloudflare
3. **ApiToken**: Token con permisos `Tunnel:Edit` + `DNS:Edit`
4. **AccountId**: Hexadecimal de 32 caracteres
5. **ZoneId**: Hexadecimal de 32 caracteres

### Error al descargar cloudflared.exe

Verificar acceso a internet y que no haya un firewall bloqueando:
```
https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe
```

---

**Versión**: 1.0 | **Última actualización**: Marzo 2026 | **Dominio actual**: `dotarsoluciones.com`
