# 📬 Webhooks Module

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)](https://opensource.org/licenses/MIT)
[![SQLite](https://img.shields.io/badge/SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white)](https://www.sqlite.org/)
[![SignalR](https://img.shields.io/badge/SignalR-Realtime-red?style=for-the-badge&logo=signalr)](https://dotnet.microsoft.com/apps/aspnet/signalr)
[![HttpListener](https://img.shields.io/badge/LightServer-Standalone-green?style=for-the-badge)](https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistener)

> **Módulo robusto y escalable** para la recepción, validación y procesamiento de webhooks de sistemas externos como **WooCommerce**, **Stripe**, **GitHub**, **Shopify** y más. Diseñado para integrarse perfectamente con sistemas ERP en C#/.NET.

---

## ✨ Características Principales

- 🔐 **Seguridad de Grado Bancario**: Validación de firmas HMAC (SHA-256, SHA-1, SHA-512) con protección contra timing attacks.
- 📡 **Tiempo Real (SignalR)**: Módulo integrado para notificar a clientes de escritorio (WinForms, WPF) o Web instantáneamente.
- 🪶 **Modo Ligero (Nuevo)**: Librería `Webhooks.LightServer` sin dependencias para levantar un servidor simple en cualquier aplicación .NET.
- 🔄 **Resiliencia Automática**: Sistema de reintentos inteligente con backoff exponencial y Dead Letter Queue.
- 📊 **Dashboard de Monitoreo**: Visualización completa de jobs y estado de procesamiento con Hangfire.
- 🗃️ **Persistencia Ligera**: SQLite preconfigurado para un inicio rápido sin complicaciones.
- 🚇 **DevOps Friendly**: Soporte nativo para Cloudflare Tunnels facilitando el desarrollo local.

---

## 📚 Documentación

Hemos preparado guías detalladas para facilitar tu integración:

- 📖 **[Manual de Uso y Configuración](docs/MANUAL_DE_USO.md)**: Guía paso a paso para configurar el servidor y conectar tu cliente ERP.
- 🛠️ **[Plan de Implementación SignalR](docs/SIGNALR_IMPLEMENTATION_PLAN.md)**: Detalles técnicos sobre la arquitectura de tiempo real.
- 🌐 **[Configuración de Túneles](docs/CLOUDFLARE_TUNNEL.md)**: Cómo exponer tu localhost a internet de forma segura.

---

## 🚀 Inicio Rápido

### Prerrequisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- (Opcional) [Cloudflared](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/)

### Opción A: API Completa (SignalR + Hangfire)

1. **Ejecutar la API**:
   ```bash
   cd src/Webhooks.Api
   dotnet run
   ```
   La API estará disponible en `http://localhost:5000`.

2. **Ejecutar Cliente de Ejemplo**:
   ```bash
   cd samples/SampleErpWinForms
   dotnet run
   ```

### Opción B: Librería Ligera (Solo Servidor Webhook)

Ideal si quieres integrar la recepción de webhooks directamente en tu servicio de Windows o aplicación de consola sin levantar una API completa.

1. **Ver ejemplo**:
   ```bash
   cd samples/LightServerSample
   dotnet run
   ```

2. **Uso en tu código**:
   ```csharp
   using Webhooks.LightServer;
   using var server = new SimpleWebhookServer(port: 8080, secretKey: "clave");
   server.OnWebhookReceived += (s, e) => Console.WriteLine($"Evento de {e.Source}");
   server.Start();
   ```

---

## 📁 Estructura del Proyecto

```
webhooks-module/
├── src/
│   ├── Webhooks.Api/           # API REST (ASP.NET Core) con SignalR
│   ├── Webhooks.Core/          # Lógica de negocio, modelos, interfaces
│   ├── Webhooks.Worker/        # Worker para procesamiento background
│   └── Webhooks.LightServer/   # 🆕 Servidor ligero Standalone (HttpListener)
├── samples/
│   ├── SampleErpWinForms/      # Cliente de escritorio con integración SignalR
│   ├── LightServerSample/      # 🆕 Ejemplo de uso de librería ligera
│   └── WooCommerceSimulator/   # Generador de webhooks para pruebas
└── docs/                       # Documentación técnica y guías
```

---

## 🔌 API Endpoints Clave

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| `POST` | `/api/webhooks/{source}` | Recibe webhooks (woocommerce, stripe, etc.) |
| `GET` | `/api/events` | Lista eventos recientes |
| `GET` | `/health` | Estado del servicio |
| `POST` | `/api/events/{id}/retry` | Reintentar procesamiento manual |

**Hub SignalR**: `/hubs/webhooks`

---

## 🤝 Contribuir

¡Las contribuciones son bienvenidas! Por favor lee nuestras guías de contribución antes de enviar un PR.

1. Fork del repositorio
2. Crea tu rama (`git checkout -b feature/nueva-funcionalidad`)
3. Commit de tus cambios (`git commit -am 'Agrega nueva funcionalidad'`)
4. Push a la rama (`git push origin feature/nueva-funcionalidad`)
5. Abre un Pull Request

---

## 📄 Licencia

Este proyecto está bajo la Licencia MIT - ver el archivo [LICENSE](LICENSE) para detalles.

---

**Desarrollado con ❤️ para la comunidad .NET**
