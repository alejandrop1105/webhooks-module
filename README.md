# 📬 Webhooks Module

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)](https://opensource.org/licenses/MIT)
[![SQLite](https://img.shields.io/badge/SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white)](https://www.sqlite.org/)
[![SignalR](https://img.shields.io/badge/SignalR-Realtime-red?style=for-the-badge&logo=signalr)](https://dotnet.microsoft.com/apps/aspnet/signalr)

> **Módulo robusto y escalable** para la recepción, validación y procesamiento de webhooks de sistemas externos como **WooCommerce**, **Stripe**, **GitHub**, **Shopify** y más. Diseñado para integrarse perfectamente con sistemas ERP en C#/.NET.

---

## ✨ Características Principales

- 🔐 **Seguridad de Grado Bancario**: Validación de firmas HMAC (SHA-256, SHA-1, SHA-512) con protección contra timing attacks.
- 📡 **Tiempo Real (SignalR)**: Módulo integrado para notificar a clientes de escritorio (WinForms, WPF) o Web instantáneamente.
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

### Instalación y Ejecución

1. **Clonar el repositorio**:
   ```bash
   git clone https://github.com/alejandrop1105/webhooks-module.git
   cd webhooks-module
   ```

2. **Ejecutar la API**:
   ```bash
   cd src/Webhooks.Api
   dotnet run
   ```
   La API estará disponible en `http://localhost:5000`.

3. **Ejecutar el Cliente de Ejemplo (WinForms)**:
   ```bash
   # En una nueva terminal
   cd samples/SampleErpWinForms
   dotnet run
   ```
   Verás una interfaz gráfica lista para recibir eventos en tiempo real.

4. **Simular Eventos**:
   ```bash
   # En una tercera terminal
   cd samples/WooCommerceSimulator
   dotnet run
   ```

---

## 📁 Estructura del Proyecto

```
webhooks-module/
├── src/
│   ├── Webhooks.Api/           # API REST (ASP.NET Core) con SignalR
│   ├── Webhooks.Core/          # Lógica de negocio, modelos, interfaces
│   └── Webhooks.Worker/        # Worker para procesamiento background
├── samples/
│   ├── SampleErpWinForms/      # 🆕 Cliente de escritorio con integración SignalR
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
