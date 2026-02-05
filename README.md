# 📬 Webhooks Module

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](https://opensource.org/licenses/MIT)
[![SQLite](https://img.shields.io/badge/SQLite-003B57?style=flat-square&logo=sqlite&logoColor=white)](https://www.sqlite.org/)

> Módulo independiente para recibir y procesar webhooks de sistemas externos como **WooCommerce**, **Stripe**, **GitHub**, **Shopify** y más. Diseñado para integrarse con sistemas ERP en C#/.NET.

## ✨ Características

- 🔐 **Validación de firmas HMAC** (SHA-256, SHA-1, SHA-512) con protección contra timing attacks
- 📥 **Recepción asíncrona** de webhooks con respuesta inmediata
- 🔄 **Sistema de reintentos** automático con backoff exponencial
- 💀 **Dead Letter Queue** para eventos fallidos
- 📊 **Dashboard visual** con Hangfire para monitorear jobs
- 🗃️ **Sin dependencias externas** - usa SQLite para desarrollo
- 🚇 **Soporte para tunneling** con Cloudflare Tunnel para desarrollo local

## 🚀 Quick Start

### Prerrequisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- (Opcional) [Cloudflared](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/) para exponer tu localhost

### Instalación

```bash
# Clonar el repositorio
git clone https://github.com/alejandrop1105/webhooks-module.git
cd webhooks-module

# Restaurar dependencias
dotnet restore

# Compilar
dotnet build
```

### Ejecutar la API

```bash
cd src/Webhooks.Api
dotnet run
```

La API estará disponible en `http://localhost:5000`

### URLs Importantes

| URL | Descripción |
|-----|-------------|
| http://localhost:5000/swagger | Documentación Swagger UI |
| http://localhost:5000/hangfire | Dashboard de jobs |
| http://localhost:5000/health | Health check |
| http://localhost:5000/api/events | Ver eventos recibidos |

### Probar con el Simulador

```bash
# En otra terminal
cd samples/WooCommerceSimulator
dotnet run
```

El simulador enviará webhooks de prueba de WooCommerce (orders, products, customers).

## 📁 Estructura del Proyecto

```
webhooks-module/
├── src/
│   ├── Webhooks.Api/           # API REST (ASP.NET Core)
│   ├── Webhooks.Core/          # Lógica de negocio, modelos, servicios
│   └── Webhooks.Worker/        # Worker para procesamiento background
├── tests/
│   └── Webhooks.Tests.Unit/    # Tests unitarios
├── samples/
│   ├── WooCommerceSimulator/   # Simulador de webhooks WooCommerce
│   └── SampleErpIntegration/   # Ejemplo de integración con ERP
└── docs/
    ├── CLOUDFLARE_TUNNEL.md    # Guía de configuración de tunnel
    └── WALKTHROUGH.md          # Resumen del proyecto
```

## 🔌 API Endpoints

### Recibir Webhooks

```http
POST /api/webhooks/{source}
```

| Parámetro | Descripción |
|-----------|-------------|
| `source` | Identificador del origen (ej: `woocommerce`, `stripe`, `github`) |

**Headers esperados:**

| Header | Descripción |
|--------|-------------|
| `X-WC-Webhook-Signature` | Firma HMAC del payload (WooCommerce) |
| `X-WC-Webhook-Topic` | Tipo de evento (ej: `order.created`) |

**Respuesta exitosa:**
```json
{
  "eventId": "f7e66b79-3924-4790-a54b-fbc64475fe3a",
  "message": "Webhook recibido y encolado"
}
```

### Consultar Eventos

```http
GET /api/events                 # Lista los últimos 50 eventos
GET /api/events/{id}            # Detalle de un evento específico
POST /api/events/{id}/retry     # Reintentar un evento fallido
```

## 🔧 Configuración

### Agregar una nueva fuente de webhooks

Las fuentes se configuran en la base de datos SQLite. El sistema viene preconfigurado con `woocommerce`.

Para agregar nuevas fuentes, inserta en la tabla `WebhookSources`:

```sql
INSERT INTO WebhookSources (Id, Name, Description, SecretKey, SignatureHeader, SignatureAlgorithm, IsActive, CreatedAt)
VALUES ('nuevo-guid', 'stripe', 'Stripe Webhooks', 'tu_secret_key', 'Stripe-Signature', 'HMAC-SHA256', 1, datetime('now'));
```

### Variables de Configuración

En `appsettings.json`:

```json
{
  "Webhook": {
    "MaxRetries": 5,
    "RetryDelaySeconds": 60
  }
}
```

## 🌐 Exponer con Cloudflare Tunnel

Para recibir webhooks reales en tu entorno de desarrollo:

```bash
# Instalar cloudflared (una vez)
winget install Cloudflare.cloudflared

# Crear tunnel temporal
cloudflared tunnel --url http://localhost:5000
```

Te dará una URL pública como `https://random-name.trycloudflare.com` que puedes configurar en WooCommerce.

📖 Ver [docs/CLOUDFLARE_TUNNEL.md](docs/CLOUDFLARE_TUNNEL.md) para configuración persistente.

## 🛡️ Seguridad

- ✅ Validación HMAC de firmas con comparación de tiempo constante
- ✅ Protección contra timing attacks usando `CryptographicOperations.FixedTimeEquals`
- ✅ Sanitización de payloads antes de persistir
- ✅ Soporte para múltiples algoritmos (SHA-256, SHA-1, SHA-512)

## 🧪 Tests

```bash
# Ejecutar todos los tests
dotnet test

# Con cobertura
dotnet test --collect:"XPlat Code Coverage"
```

## 📚 Stack Tecnológico

| Tecnología | Uso |
|------------|-----|
| .NET 8 | Framework principal |
| ASP.NET Core | API REST |
| Entity Framework Core | ORM |
| SQLite | Base de datos (desarrollo) |
| Hangfire | Cola de trabajos / Background jobs |
| Serilog | Logging estructurado |
| xUnit | Testing |

## 🤝 Contribuir

1. Fork del repositorio
2. Crear rama feature (`git checkout -b feature/nueva-funcionalidad`)
3. Commit de cambios (`git commit -am 'Agregar nueva funcionalidad'`)
4. Push a la rama (`git push origin feature/nueva-funcionalidad`)
5. Crear Pull Request

## 📄 Licencia

Este proyecto está bajo la Licencia MIT - ver el archivo [LICENSE](LICENSE) para detalles.

---

**Desarrollado con ❤️ para la comunidad .NET**
