# Walkthrough: Migración a Cloudflare Named Tunnels

## Resumen

Se migró la DLL `Webhooks.LegacyEngine` de Quick Tunnels (URLs aleatorias `.trycloudflare.com`) a **Named Tunnels** provisionados vía API REST de Cloudflare. La solución es **100% desatendida**: auto-descarga cloudflared, crea el túnel, configura DNS, y todo se maneja desde la UI del ERP.

## Cambios Realizados

### Archivos nuevos
| Archivo                                                                                                                  | Descripción                                      |
| ------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------ |
| [CloudflareConfig.cs](file:///d:/DESARROLLO/ANTIGRAVITY/WebHooks/src/Webhooks.LegacyEngine/Managers/CloudflareConfig.cs) | Modelo de configuración con validación integrada |

### Archivos modificados
| Archivo                                                                                                                                | Cambios                                                           |
| -------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------- |
| [CloudflareTunnelManager.cs](file:///d:/DESARROLLO/ANTIGRAVITY/WebHooks/src/Webhooks.LegacyEngine/Managers/CloudflareTunnelManager.cs) | Reescrito completo: API REST, auto-descarga, reset, reutilización |
| [WebhookHost.cs](file:///d:/DESARROLLO/ANTIGRAVITY/WebHooks/src/Webhooks.LegacyEngine/Engine/WebhookHost.cs)                           | Nuevo `SetCloudflareConfig()` y `ResetTunnel()`                   |
| [Form1.cs](file:///d:/DESARROLLO/ANTIGRAVITY/WebHooks/samples/LegacyErpClient/Form1.cs)                                                | UI con campos Cloudflare, botón Reset, persistencia               |
| [CLOUDFLARE_TUNNEL.md](file:///d:/DESARROLLO/ANTIGRAVITY/WebHooks/docs/CLOUDFLARE_TUNNEL.md)                                           | Documentación actualizada con Named Tunnel real                   |

### Configuración Cloudflare realizada
| Recurso       | Valor                                                         |
| ------------- | ------------------------------------------------------------- |
| Dominio       | `dotarsoluciones.com`                                         |
| Túnel inicial | `webhooks-dotar` (ID: `d3cdf7a6-7448-4b86-9c5e-0060aaf3230a`) |
| Config file   | `C:\Users\Ale\.cloudflared\config.yml`                        |

## Arquitectura Final

```
┌──────────────────────────────────┐
│  cloudflared.exe (proceso hijo)  │ ← Vive con la app, muere con la app
│  erp-xxx.dotarsoluciones.com     │
│         ↓ rutea a                │
│    localhost:5000                 │
└──────────────┬───────────────────┘
               │
┌──────────────▼───────────────────┐
│  App WinForms (.NET 4.8)         │
│  ┌─────────────────────────────┐ │
│  │ Webhooks.LegacyEngine (DLL) │ │
│  │  ├── OWIN Server :5000      │ │
│  │  ├── WebhooksController     │ │
│  │  └── CloudflareTunnelMgr    │ │ ← API REST + auto-descarga
│  └─────────────────────────────┘ │
└──────────────────────────────────┘
```

## Funcionalidades

- **Auto-descarga**: Si `cloudflared.exe` no existe, se descarga automáticamente (~60MB).
- **Provisionamiento vía API**: Crea el túnel y DNS CNAME sin `cert.pem`.
- **Reutilización**: Si el túnel ya existe en Cloudflare, lo reutiliza sin duplicar.
- **Reset**: Elimina config local y cloudflared sin tocar el túnel en la nube.
- **Watchdog**: Detecta caídas del proceso y auto-reconecta.
- **Lifecycle**: Túnel vive mientras la app corre, muere al cerrarla.

## Verificación

- ✅ `Webhooks.LegacyEngine` compila correctamente (0 errores)
- ✅ `LegacyErpClient` compila correctamente (0 errores)
- ⏳ Test end-to-end pendiente (requiere crear API Token en Cloudflare Dashboard)

## Próximos Pasos

1. Crear API Token en [Cloudflare Dashboard](https://dash.cloudflare.com/profile/api-tokens) con permisos:
   - `Account` > `Cloudflare Tunnel` > `Edit`
   - `Zone` > `DNS` > `Edit`
2. Obtener Account ID y Zone ID del dashboard
3. Ejecutar LegacyErpClient, llenar campos de Cloudflare, y probar "☁️ API + Tunnel"
