# 🌐 Configuración de Cloudflare Tunnel

Cloudflare Tunnel te permite exponer tu API local a internet de forma segura y gratuita.

## 📋 Prerrequisitos

1. Cuenta de Cloudflare (gratuita): https://dash.cloudflare.com/sign-up
2. Opcional: dominio propio configurado en Cloudflare

## 🚀 Instalación

### Windows (Winget)
```powershell
winget install cloudflare.cloudflared
```

### Windows (Descarga directa)
```powershell
# Descargar
Invoke-WebRequest -Uri "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe" -OutFile "cloudflared.exe"

# Mover a PATH
Move-Item cloudflared.exe "C:\Windows\System32\"
```

## 🔧 Uso Básico (Quick Tunnel - Sin autenticación)

Para desarrollo rápido sin configuración:

```powershell
# En una terminal, iniciar la API
cd src/Webhooks.Api
dotnet run

# En otra terminal, iniciar el tunnel
cloudflared tunnel --url http://localhost:5000
```

Recibirás una URL como: `https://random-words.trycloudflare.com`

> ⚠️ **Nota**: Esta URL cambia cada vez que reinicias el tunnel.

## 🔐 Configuración Persistente (Con cuenta Cloudflare)

Para URLs fijas y configuración persistente:

### 1. Autenticarse
```powershell
cloudflared tunnel login
```

### 2. Crear un tunnel
```powershell
cloudflared tunnel create webhooks-dev
```

### 3. Configurar routing (si tienes dominio)
```powershell
# Crear registro DNS para tu dominio
cloudflared tunnel route dns webhooks-dev webhooks-dev.tudominio.com
```

### 4. Crear archivo de configuración

Crear archivo `%USERPROFILE%\.cloudflared\config.yml`:

```yaml
tunnel: webhooks-dev
credentials-file: C:\Users\TuUsuario\.cloudflared\<tunnel-id>.json

ingress:
  - hostname: webhooks-dev.tudominio.com
    service: http://localhost:5000
  - service: http_status:404
```

### 5. Ejecutar con configuración
```powershell
cloudflared tunnel run webhooks-dev
```

## 🛒 Configurar WooCommerce

1. Ve a tu tienda WooCommerce → Ajustes → Avanzado → Webhooks
2. Añadir webhook:
   - **Nombre**: Ordenes a ERP
   - **Estado**: Activo
   - **Tema**: Orden creada
   - **URL de entrega**: `https://tu-tunnel.trycloudflare.com/api/webhooks/woocommerce`
   - **Secreto**: (genera una clave y recuerda configurarla en la API)
3. Guardar

## 🧪 Probar la Conexión

```powershell
# Enviar un webhook de prueba
curl -X POST https://tu-tunnel.trycloudflare.com/api/webhooks/test `
  -H "Content-Type: application/json" `
  -d '{"test": true}'

# Verificar eventos recibidos
curl https://tu-tunnel.trycloudflare.com/api/events
```

## 📊 Monitoreo

- **Dashboard Hangfire**: `http://localhost:5000/hangfire`
- **Logs**: `src/Webhooks.Api/logs/`
- **Swagger**: `http://localhost:5000/swagger`

## 🔄 Ejecutar como Servicio Windows (Opcional)

```powershell
# Instalar como servicio
cloudflared service install

# Iniciar servicio
Start-Service cloudflared
```

## ❓ Troubleshooting

### El tunnel no conecta
```powershell
# Verificar estado
cloudflared tunnel info webhooks-dev

# Ver logs detallados
cloudflared tunnel --loglevel debug --url http://localhost:5000
```

### WooCommerce no envía webhooks
1. Verifica que la URL sea accesible desde internet
2. Revisa los logs de WooCommerce en: WooCommerce → Estado → Registros
3. Usa el simulador local para probar primero
