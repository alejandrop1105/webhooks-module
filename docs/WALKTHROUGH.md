# Walkthrough: Sistema de Webhooks

## ✅ Objetivo Cumplido

Se implementó un **módulo independiente de webhooks** para integrar con ERP C# WinForms, capaz de:
- Recibir webhooks de sistemas externos (WooCommerce, Stripe, GitHub, Shopify)
- Procesar eventos de forma asíncrona con reintentos
- Preparado para emitir webhooks en el futuro

---

## 📁 Estructura Creada

```
WebHooks/
├── src/
│   ├── Webhooks.Api/        # API REST receiver (ASP.NET Core 8)
│   ├── Webhooks.Core/       # Lógica de negocio, modelos, servicios
│   └── Webhooks.Worker/     # Procesador background (preparado)
├── tests/
│   └── Webhooks.Tests.Unit/ # 5 tests - todos pasando ✅
├── samples/
│   ├── WooCommerceSimulator/    # Simulador interactivo
│   └── SampleErpIntegration/    # Ejemplo de integración
├── docs/
│   └── CLOUDFLARE_TUNNEL.md     # Guía de tunneling
├── README.md
└── nuget.config              # Configuración NuGet local
```

---

## 🔧 Componentes Implementados

| Componente | Archivo | Descripción |
|------------|---------|-------------|
| Modelos | `WebhookEvent.cs`, `WebhookSource.cs`, `WebhookSubscription.cs` | Entidades para persistencia |
| DbContext | `WebhookDbContext.cs` | EF Core con SQLite |
| Validador | `SignatureValidator.cs` | Validación HMAC-SHA256/1/512 |
| Receptor | `WebhookReceiver.cs` | Recibe y encola webhooks |
| Procesador | `WebhookProcessor.cs` | Procesa con reintentos y dead letter |
| API | `Program.cs` | Endpoints REST + Hangfire |

---

## 🧪 Tests Ejecutados

```
Resumen de pruebas: total: 5; con errores: 0; correcto: 5
```

| Test | Estado |
|------|--------|
| `Validate_WithValidSignature_ReturnsTrue` | ✅ |
| `Validate_WithInvalidSignature_ReturnsFalse` | ✅ |
| `Validate_WithNullPayload_ReturnsFalse` | ✅ |
| `Validate_WithNullSignature_ReturnsFalse` | ✅ |
| `Validate_WithPrefixedSignature_ReturnsTrue` | ✅ |

---

## 🚀 Cómo Ejecutar

### 1. Iniciar la API
```powershell
cd d:\DESARROLLO\ANTIGRAVITY\WebHooks\src\Webhooks.Api
dotnet run
```

Endpoints disponibles en `http://localhost:5000`:
- **Swagger**: `/swagger`
- **Hangfire Dashboard**: `/hangfire`
- **Health Check**: `/health`

### 2. Simular Webhook de WooCommerce
```powershell
cd d:\DESARROLLO\ANTIGRAVITY\WebHooks\samples\WooCommerceSimulator
dotnet run
```

### 3. Exponer con Cloudflare Tunnel
```powershell
cloudflared tunnel --url http://localhost:5000
```

---

## 📌 Próximos Pasos Sugeridos

1. **Configurar clave secreta de WooCommerce** en la base de datos
2. **Implementar handlers específicos** para eventos (ej: `OrderCreatedHandler`)
3. **Conectar con tu ERP** via SignalR o polling
4. **Prueba end-to-end** con WooCommerce real

---

## 🛠️ Correcciones Realizadas

- **Bug fix**: `SignatureValidator.NormalizeSignature` incorrectamente dividía firmas Base64 con padding `=`. Corregido para solo normalizar prefijos de algoritmo conocidos (`sha256=`, `sha1=`, etc.).

---

## 📊 Stack Tecnológico Final

- **.NET 8** - Framework
- **SQLite** - Base de datos (desarrollo)
- **Hangfire + SQLite** - Cola de trabajos
- **Serilog** - Logging estructurado
- **Cloudflare Tunnel** - Tunneling gratuito
