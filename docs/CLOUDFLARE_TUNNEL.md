# 🌐 Configuración de Cloudflare Tunnel

Cloudflare Tunnel te permite exponer tu API local a internet de forma segura y gratuita.

## 📋 Prerrequisitos

1. Cuenta de Cloudflare (gratuita): https://dash.cloudflare.com/sign-up
2. Dominio configurado en Cloudflare (ej: `dotarsoluciones.com`)

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

## 🔧 Quick Tunnel (Desarrollo rápido — Sin autenticación)

Para pruebas rápidas sin configuración persistente:

```powershell
# En una terminal, iniciar la API
cd src/Webhooks.Api
dotnet run

# En otra terminal, iniciar el tunnel
cloudflared tunnel --url http://localhost:5000
```

Recibirás una URL como: `https://random-words.trycloudflare.com`

> ⚠️ **Nota**: Esta URL cambia cada vez que reinicias el tunnel. No recomendado para producción.

## 🔐 Named Tunnel (Producción — URL fija)

### Configuración actual del proyecto

- **Dominio**: `dotarsoluciones.com`
- **Subdominio**: `webhooks.dotarsoluciones.com`
- **Tunnel**: `webhooks-dotar` (ID: `d3cdf7a6-7448-4b86-9c5e-0060aaf3230a`)
- **Config**: `%USERPROFILE%\.cloudflared\config.yml`

### Paso a paso (ya completado)

#### 1. Autenticarse
```powershell
cloudflared tunnel login
```

#### 2. Crear un tunnel
```powershell
cloudflared tunnel create webhooks-dotar
```

#### 3. Configurar DNS
```powershell
cloudflared tunnel route dns webhooks-dotar webhooks.dotarsoluciones.com
```

#### 4. Archivo de configuración

Ubicación: `%USERPROFILE%\.cloudflared\config.yml`

```yaml
tunnel: webhooks-dotar
credentials-file: C:\Users\Ale\.cloudflared\d3cdf7a6-7448-4b86-9c5e-0060aaf3230a.json

ingress:
  - hostname: webhooks.dotarsoluciones.com
    service: http://localhost:5000
  - service: http_status:404
```

#### 5. Ejecutar el túnel
```powershell
cloudflared tunnel run webhooks-dotar
```

## 🔄 Ejecutar como Servicio Windows

Para que el túnel se inicie automáticamente con Windows:

```powershell
# Instalar como servicio (requiere Admin)
cloudflared service install

# Iniciar servicio
Start-Service cloudflared

# Verificar estado
Get-Service cloudflared
```

## 🛒 Configurar WooCommerce

1. Ve a tu tienda WooCommerce → Ajustes → Avanzado → Webhooks
2. Añadir webhook:
   - **Nombre**: Ordenes a ERP
   - **Estado**: Activo
   - **Tema**: Orden creada
   - **URL de entrega**: `https://webhooks.dotarsoluciones.com/api/webhooks/woocommerce`
   - **Secreto**: (genera una clave y recuerda configurarla en la API)
3. Guardar

## 🧪 Probar la Conexión

```powershell
# Enviar un webhook de prueba
curl -X POST https://webhooks.dotarsoluciones.com/api/webhooks/test `
  -H "Content-Type: application/json" `
  -d '{"test": true}'

# Verificar eventos recibidos
curl https://webhooks.dotarsoluciones.com/api/events
```

## 📊 Monitoreo

- **Dashboard Hangfire**: `http://localhost:5000/hangfire`
- **Logs**: `src/Webhooks.Api/logs/`
- **Swagger**: `http://localhost:5000/swagger`
- **Tunnel Info**: `cloudflared tunnel info webhooks-dotar`

## ❓ Troubleshooting

### El tunnel no conecta
```powershell
# Verificar estado
cloudflared tunnel info webhooks-dotar

# Ver logs detallados
cloudflared tunnel --loglevel debug run webhooks-dotar
```

### WooCommerce no envía webhooks
1. Verifica que el túnel esté corriendo (`cloudflared tunnel run webhooks-dotar`)
2. Revisa los logs de WooCommerce en: WooCommerce → Estado → Registros
3. Usa el simulador local para probar primero
