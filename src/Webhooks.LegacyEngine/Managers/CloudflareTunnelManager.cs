using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Webhooks.LegacyEngine.Managers;

/// <summary>
/// Administra el ciclo de vida de un Cloudflare Named Tunnel.
/// Provisionamiento automático vía API REST (sin cert.pem).
/// Auto-descarga de cloudflared.exe si no está presente.
/// </summary>
public class CloudflareTunnelManager : IDisposable
{
    private Process _process;
    private readonly System.Timers.Timer _watchdogTimer;
    private readonly int _localPort;
    private readonly string _exePath;
    private readonly string _configPath;
    private readonly CloudflareConfig _config;
    private bool _isInstalling;
    private bool _stopRequested;
    private bool _isConnected;
    private DateTime _processStartTime;
    private TunnelLocalConfig _localConfig;

    private static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32NT;
    private static string ExeName => IsWindows ? "cloudflared.exe" : "cloudflared";
    private static string CloudflaredDownloadUrl => IsWindows 
        ? "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe"
        : "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64";

    private const string CloudflareApiBase = "https://api.cloudflare.com/client/v4";

    // Eventos hacia el WebhookHost / UI
    public event EventHandler<string> OnUrlGenerated;
    public event EventHandler<string> OnStatusChanged;

    public CloudflareTunnelManager(int localPort, CloudflareConfig config)
    {
        _localPort = localPort;
        _config = config;
        _exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ExeName);
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tunnel_config.json");

        // Watchdog: vigilar cada 5 segundos si el proceso se cayó
        _watchdogTimer = new System.Timers.Timer(5000);
        _watchdogTimer.Elapsed += WatchdogTimer_Elapsed;
    }

    /// <summary>
    /// Arranca el túnel completo: auto-descarga → provisionar → ejecutar.
    /// </summary>
    public async Task StartAsync()
    {
        _stopRequested = false;

        if (_isInstalling) return;

        if (!_config.IsValid)
        {
            NotifyStatus("Configuración de Cloudflare incompleta", "🔴");
            return;
        }

        // Paso 1: Auto-descargar cloudflared si no existe
        if (!CloudflaredExists())
        {
            await DownloadBinaryAsync();
            if (!CloudflaredExists()) return;
        }

        // Paso 2: Cargar o provisionar túnel
        if (File.Exists(_configPath))
        {
            _localConfig = LoadLocalConfig();

            // Actualizar hostname con el valor sanitizado actual (por si el usuario corrigió el dominio)
            if (_localConfig.Hostname != _config.Hostname)
            {
                _localConfig.Hostname = _config.Hostname;
                SaveLocalConfig(_localConfig);
            }

            NotifyStatus("Configuración local encontrada, conectando túnel existente...", "🟡");
            Serilog.Log.Information($"Reutilizando túnel existente: {_localConfig.Hostname}");
        }
        else
        {
            NotifyStatus("Primera ejecución: provisionando túnel en Cloudflare...", "🟡");
            bool provisioned = await ProvisionTunnelAsync();
            if (!provisioned) return;
        }

        // Paso 3: Ejecutar el túnel (usar _config.Hostname que aplica CleanDomain)
        OnUrlGenerated?.Invoke(this, $"https://{_config.Hostname}");
        StartTunnelProcess();
    }

    /// <summary>
    /// Detiene el proceso cloudflared (no elimina el túnel de Cloudflare).
    /// </summary>
    public void Stop()
    {
        _stopRequested = true;
        _isConnected = false;
        _watchdogTimer.Stop();
        KillProcessSafely();
        NotifyStatus("Detenido intencionalmente", "🔴");
    }

    /// <summary>
    /// Auto-limpieza: elimina config local y cloudflared.exe.
    /// El túnel PERSISTE en Cloudflare para poder reutilizarlo.
    /// </summary>
    public void Reset()
    {
        Stop();

        // Eliminar config local
        if (File.Exists(_configPath))
        {
            File.Delete(_configPath);
            Serilog.Log.Information("tunnel_config.json eliminado");
        }

        // Eliminar binario local
        if (File.Exists(_exePath))
        {
            File.Delete(_exePath);
            Serilog.Log.Information($"{ExeName} eliminado");
        }

        _localConfig = null;
        NotifyStatus("Reset completo. Próxima ejecución re-descargará y re-conectará.", "🟠");
    }

    // ──────────────────────────────────────────────
    // Provisionamiento vía API Cloudflare
    // ──────────────────────────────────────────────

    private async Task<bool> ProvisionTunnelAsync()
    {
        try
        {
            using (var http = CreateHttpClient())
            {
                // 1. Buscar si el túnel ya existe en Cloudflare
                string tunnelId = null;
                string tunnelToken = null;

                var existing = await FindExistingTunnelAsync(http);
                if (existing != null)
                {
                    tunnelId = existing.Value.id;
                    NotifyStatus($"Túnel '{_config.TunnelName}' ya existe en Cloudflare, reutilizando...", "🟡");
                    Serilog.Log.Information($"Túnel existente encontrado: {tunnelId}");

                    // Obtener token del túnel existente
                    tunnelToken = await GetTunnelTokenAsync(http, tunnelId);
                }
                else
                {
                    // 2. Crear túnel nuevo
                    NotifyStatus($"Creando túnel '{_config.TunnelName}'...", "🟡");
                    var created = await CreateTunnelAsync(http);
                    if (created == null) return false;

                    tunnelId = created.Value.id;
                    tunnelToken = created.Value.token;
                    Serilog.Log.Information($"Túnel creado: {tunnelId}");
                }

                // 3. Asegurar que el CNAME DNS exista
                await EnsureDnsRecordAsync(http, tunnelId);

                // 4. Actualizar configuración de ingress (siempre, para aplicar cambios)
                await ConfigureTunnelIngressAsync(http, tunnelId);

                // 5. Guardar configuración local
                _localConfig = new TunnelLocalConfig
                {
                    TunnelName = _config.TunnelName,
                    TunnelId = tunnelId,
                    TunnelToken = tunnelToken,
                    Hostname = _config.Hostname
                };
                SaveLocalConfig(_localConfig);

                NotifyStatus("Provisionamiento exitoso", "🟢");
                return true;
            }
        }
        catch (Exception ex)
        {
            NotifyStatus($"Error de provisionamiento: {ex.Message}", "🔴");
            Serilog.Log.Error(ex, "Error durante provisionamiento de túnel Cloudflare");
            return false;
        }
    }

    private async Task<(string id, string token)?> FindExistingTunnelAsync(HttpClient http)
    {
        string url = $"{CloudflareApiBase}/accounts/{_config.AccountId}/cfd_tunnel?name={_config.TunnelName}&is_deleted=false";
        var response = await http.GetAsync(url);
        var json = JObject.Parse(await response.Content.ReadAsStringAsync());

        if (!response.IsSuccessStatusCode || !(bool)json["success"])
            return null;

        var tunnels = json["result"] as JArray;
        if (tunnels == null || tunnels.Count == 0)
            return null;

        var tunnel = tunnels[0];
        string id = tunnel["id"]?.ToString();
        string token = await GetTunnelTokenAsync(http, id);

        return (id, token);
    }

    private async Task<string> GetTunnelTokenAsync(HttpClient http, string tunnelId)
    {
        string url = $"{CloudflareApiBase}/accounts/{_config.AccountId}/cfd_tunnel/{tunnelId}/token";
        var response = await http.GetAsync(url);
        var json = JObject.Parse(await response.Content.ReadAsStringAsync());

        if (response.IsSuccessStatusCode && (bool)json["success"])
        {
            return json["result"]?.ToString();
        }

        throw new Exception($"No se pudo obtener token del túnel: {json["errors"]}");
    }

    private async Task<(string id, string token)?> CreateTunnelAsync(HttpClient http)
    {
        string url = $"{CloudflareApiBase}/accounts/{_config.AccountId}/cfd_tunnel";

        // Generar secret aleatorio para el túnel (32 bytes en base64)
        var secretBytes = new byte[32];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(secretBytes);
        }
        string tunnelSecret = Convert.ToBase64String(secretBytes);

        var body = new
        {
            name = _config.TunnelName,
            tunnel_secret = tunnelSecret,
            config_src = "cloudflare"
        };

        var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
        var response = await http.PostAsync(url, content);
        var json = JObject.Parse(await response.Content.ReadAsStringAsync());

        if (!response.IsSuccessStatusCode || !(bool)json["success"])
        {
            string errors = json["errors"]?.ToString() ?? "Error desconocido";
            NotifyStatus($"Error al crear túnel: {errors}", "🔴");
            Serilog.Log.Error($"API Cloudflare error al crear túnel: {errors}");
            return null;
        }

        string tunnelId = json["result"]?["id"]?.ToString();

        // Obtener el token del túnel recién creado
        string token = await GetTunnelTokenAsync(http, tunnelId);

        // Configurar ingress del túnel vía API
        await ConfigureTunnelIngressAsync(http, tunnelId);

        return (tunnelId, token);
    }

    private async Task ConfigureTunnelIngressAsync(HttpClient http, string tunnelId)
    {
        string url = $"{CloudflareApiBase}/accounts/{_config.AccountId}/cfd_tunnel/{tunnelId}/configurations";

        var configBody = new
        {
            config = new
            {
                ingress = new object[]
                {
                    new
                    {
                        hostname = _config.Hostname,
                        service = $"http://localhost:{_localPort}",
                        originRequest = new { httpHostHeader = "localhost" }
                    },
                    new { hostname = (string)null, service = "http_status:404" }
                }
            }
        };

        var content = new StringContent(JsonConvert.SerializeObject(configBody), Encoding.UTF8, "application/json");
        var response = await http.PutAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorJson = await response.Content.ReadAsStringAsync();
            Serilog.Log.Warning($"Advertencia al configurar ingress: {errorJson}");
        }
        else
        {
            Serilog.Log.Information("Ingress del túnel configurado vía API");
        }
    }

    private async Task EnsureDnsRecordAsync(HttpClient http, string tunnelId)
    {
        string hostname = _config.Hostname;

        // Verificar si ya existe
        string checkUrl = $"{CloudflareApiBase}/zones/{_config.ZoneId}/dns_records?name={hostname}&type=CNAME";
        var checkResponse = await http.GetAsync(checkUrl);
        var checkJson = JObject.Parse(await checkResponse.Content.ReadAsStringAsync());

        if (checkResponse.IsSuccessStatusCode && (bool)checkJson["success"])
        {
            var records = checkJson["result"] as JArray;
            if (records != null && records.Count > 0)
            {
                Serilog.Log.Information($"Registro DNS ya existe: {hostname}");
                return;
            }
        }

        // Crear CNAME
        NotifyStatus($"Creando registro DNS: {hostname}...", "🟡");

        var dnsBody = new
        {
            type = "CNAME",
            name = hostname,
            content = $"{tunnelId}.cfargotunnel.com",
            proxied = true
        };

        var content = new StringContent(JsonConvert.SerializeObject(dnsBody), Encoding.UTF8, "application/json");
        var response = await http.PostAsync($"{CloudflareApiBase}/zones/{_config.ZoneId}/dns_records", content);
        var json = JObject.Parse(await response.Content.ReadAsStringAsync());

        if (response.IsSuccessStatusCode && (bool)json["success"])
        {
            Serilog.Log.Information($"DNS CNAME creado: {hostname}");
        }
        else
        {
            string errors = json["errors"]?.ToString() ?? "Error desconocido";
            Serilog.Log.Warning($"Advertencia DNS: {errors}");
        }
    }

    // ──────────────────────────────────────────────
    // Ejecución del proceso cloudflared
    // ──────────────────────────────────────────────

    private void StartTunnelProcess()
    {
        KillProcessSafely();
        _isConnected = false;

        // Nota: NO matamos todos los cloudflared del sistema.
        // Puede haber otros proyectos con sus propios túneles corriendo.

        NotifyStatus("Iniciando proceso cloudflared...", "🟡");

        try
        {
            _process = new Process();
            string finalPath = File.Exists(_exePath) ? _exePath : ExeName;
            _process.StartInfo.FileName = finalPath;
            //si se quiere mayor informacion del log se puede sacar el --loglevel fatal
            _process.StartInfo.Arguments = $"tunnel run --token {_localConfig.TunnelToken}  --loglevel fatal";
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.RedirectStandardError = true;
            _process.StartInfo.CreateNoWindow = true;

            _process.ErrorDataReceived += Process_ErrorDataReceived;

            _process.Start();
            _processStartTime = DateTime.UtcNow;
            _process.BeginErrorReadLine();

            _watchdogTimer.Start();
            NotifyStatus("Conectando a Cloudflare Edge...", "🟡");
        }
        catch (Exception ex)
        {
            NotifyStatus($"Error al iniciar cloudflared: {ex.Message}", "🔴");
        }
    }

    private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data)) return;

        // Loguear warnings y errores relevantes de cloudflared
        if (e.Data.Contains("ERR ") || e.Data.Contains("WRN "))
        {
            Serilog.Log.Warning($"[Proxy Log] {e.Data}");
        }

        // Detectar conexión exitosa (solo notificar una vez, cloudflared crea ~4 conexiones)
        if (e.Data.Contains("Registered tunnel connection") && !_isConnected)
        {
            _isConnected = true;
            NotifyStatus("Establecido y Público", "🟢");
        }
    }

    // ──────────────────────────────────────────────
    // Auto-descarga de cloudflared.exe
    // ──────────────────────────────────────────────

    private bool CloudflaredExists()
    {
        if (File.Exists(_exePath)) return true;

        // Buscar en PATH del sistema
        var systemPath = Environment.GetEnvironmentVariable("PATH");
        var paths = systemPath?.Split(Path.PathSeparator) ?? Array.Empty<string>();
        return paths.Any(p => File.Exists(Path.Combine(p, ExeName)));
    }

    private async Task DownloadBinaryAsync()
    {
        _isInstalling = true;
        NotifyStatus($"Auto-descargando {ExeName} (~60MB)...", "🟠");

        try
        {
            using (var hc = new HttpClient())
            {
                var response = await hc.GetAsync(CloudflaredDownloadUrl);
                response.EnsureSuccessStatusCode();

                using (var fs = new FileStream(_exePath, FileMode.Create))
                {
                    await response.Content.CopyToAsync(fs);
                }
            }

            // Asignar permisos de ejecución en Linux/Docker
            if (!IsWindows)
            {
                try
                {
                    var proc = Process.Start(new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{_exePath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    });
                    proc?.WaitForExit();
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "No se pudo asignar a chmod +x automáticamente. Es posible que el binario cloudflared no arranque.");
                }
            }

            NotifyStatus($"{ExeName} descargado exitosamente", "🟢");
            Serilog.Log.Information($"{ExeName} auto-descargado");
        }
        catch (Exception ex)
        {
            NotifyStatus($"Error descargando cloudflared: {ex.Message}", "🔴");
            Serilog.Log.Error(ex, "Error al descargar cloudflared.exe");
        }
        finally
        {
            _isInstalling = false;
        }
    }

    // ──────────────────────────────────────────────
    // Watchdog & Proceso
    // ──────────────────────────────────────────────

    private void WatchdogTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        if (_stopRequested) return;

        if (_process == null || _process.HasExited)
        {
            _watchdogTimer.Stop();

            // Backoff: si el proceso duró menos de 15s, esperar antes de reintentar
            var uptime = DateTime.UtcNow - _processStartTime;
            if (uptime.TotalSeconds < 15)
            {
                NotifyStatus("Proceso inestable. Reintentando en 10 segundos...", "🟠");
                System.Threading.Thread.Sleep(10000);
            }
            else
            {
                NotifyStatus("Detectada caída de red (Auto-Reconectando...)", "🟠");
            }

            if (!_stopRequested) StartTunnelProcess();
        }
    }

    private void KillProcessSafely()
    {
        if (_process != null && !_process.HasExited)
        {
            try { _process.Kill(); } catch { }
        }
        _process?.Dispose();
        _process = null;
    }

    // ──────────────────────────────────────────────
    // Persistencia local
    // ──────────────────────────────────────────────

    private TunnelLocalConfig LoadLocalConfig()
    {
        string json = File.ReadAllText(_configPath);
        return JsonConvert.DeserializeObject<TunnelLocalConfig>(json);
    }

    private void SaveLocalConfig(TunnelLocalConfig config)
    {
        string json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(_configPath, json);
        Serilog.Log.Information($"Config local guardada en: {_configPath}");
    }

    private HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiToken}");
        return client;
    }

    private void NotifyStatus(string message, string icon)
    {
        OnStatusChanged?.Invoke(this, $"{icon} {message}");
    }

    public void Dispose()
    {
        Stop();
    }
}

/// <summary>
/// Config local persistida en tunnel_config.json junto al ejecutable.
/// Almacena los datos del túnel para reutilizarlo entre sesiones.
/// </summary>
internal class TunnelLocalConfig
{
    public string TunnelName { get; set; }
    public string TunnelId { get; set; }
    public string TunnelToken { get; set; }
    public string Hostname { get; set; }
}
