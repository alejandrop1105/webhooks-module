using System;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Webhooks.LegacyEngine.Managers;

namespace Webhooks.LegacyEngine.Engine;

/// <summary>
/// El "Cerebro" de la integración. Fachada unificada (Singleton/Instancia)
/// que expone todos los eventos hacia el WinForms (Front-End).
/// </summary>
public class WebhookHost : IDisposable
{
    /// <summary>
    /// El logger del engine. Sombrea deliberadamente a <see cref="Serilog.Log"/> dentro
    /// de esta clase, de modo que los <c>Log.Information(...)</c> de siempre escriban en
    /// el logger propio en lugar del global de la aplicación anfitriona. Ver
    /// <see cref="EngineLog"/> para el porqué.
    /// </summary>
    private static ILogger Log => EngineLog.Current;

    private IDisposable? _owinServer;
    private CloudflareTunnelManager? _tunnelManager;
    private readonly int _port;
    private readonly System.Timers.Timer _owinWatchdogTimer;
    private CloudflareConfig? _cloudflareConfig;
    private string _listeningUrl = string.Empty;
    private bool _isTunnelEnabled;
    private bool _isTunnelConnected;


    // --- MEGAFONOS (Eventos hacia la UI) ---

    /// <summary>
    /// Se dispara instantáneamente en RAM cuando un Controller recibe un JSON
    /// </summary>
    public event EventHandler<WebhookPayloadEventArgs> OnWebhookReceived;

    /// <summary>
    /// Eventos de salud del servidor (Caídas, Reconexiones)
    /// </summary>
    public event EventHandler<string> OnServerStatusChanged;

    /// <summary>
    /// Eventos de salud de la red Cloudflare
    /// </summary>
    public event EventHandler<string> OnTunnelStatusChanged;

    /// <summary>
    /// Cuando Cloudflare nos otorga una URL pública fija
    /// </summary>
    public event EventHandler<string> OnTunnelUrlGenerated;

    /// <summary>
    /// Cuando el servidor local arranca y tiene una URL accesible
    /// </summary>
    public event EventHandler<string> OnLocalUrlGenerated;

    /// <summary>
    /// Emite los logs de Serilog a la UI
    /// </summary>
    public event EventHandler<LogEventArgs> OnSystemLog;

    /// <summary>
    /// Se dispara cada vez que cambia el estado del engine (activo/inactivo, modo local/público).
    /// </summary>
    public event EventHandler<EngineState> OnStateChanged;

    // --- FIN MEGAFONOS ---

    // Singleton para acceso fácil desde el WebhooksController
    public static WebhookHost? Instance { get; private set; }

    // --- PROPIEDADES DE ESTADO ---

    /// <summary>
    /// Indica si el servidor HTTP está activo y escuchando.
    /// </summary>
    public bool IsRunning => _owinServer != null;

    /// <summary>
    /// Indica si el túnel de Cloudflare está habilitado (se solicitó arranque con tunnel).
    /// </summary>
    public bool IsTunnelEnabled => _isTunnelEnabled;

    /// <summary>
    /// Indica si el túnel de Cloudflare está conectado y público.
    /// </summary>
    public bool IsTunnelConnected => _isTunnelConnected;

    /// <summary>
    /// Modo actual del engine: Stopped, Local o Public.
    /// </summary>
    public EngineMode CurrentMode
    {
        get
        {
            if (!IsRunning) return EngineMode.Stopped;
            return _isTunnelEnabled && _isTunnelConnected ? EngineMode.Public : EngineMode.Local;
        }
    }

    /// <summary>
    /// Indica si el servidor está escuchando en todas las interfaces de red (accesible por IP LAN).
    /// </summary>
    public bool IsListeningOnAllInterfaces => _listeningUrl.Contains("+");

    public WebhookHost(int port = 5000)
    {
        _port = port;
        Instance = this;

        // Inicializar Serilog Rotativo (Retención 5 días) y Sink UI.
        // Se instala en EngineLog, NO en el Serilog global: ese lo administra la
        // aplicación anfitriona y pisarlo le rompe el logging. Ver EngineLog.
        string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "webhook-engine-.txt");
        EngineLog.Configure(new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 5,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Sink(new UiEventLogSink())
            .CreateLogger());

        Log.Information("====== Webhook Engine Inicializado ======");

        // Watchdog de Salud Interna (Verifica que OWIN responda HTTP)
        _owinWatchdogTimer = new System.Timers.Timer(10000); // 10 segundos
        _owinWatchdogTimer.Elapsed += Watchdog_CheckOwinHealth;

        // Conectar el broker de clientes a esta instancia
        WebhookClientBroker.Bind(this);
    }

    /// <summary>
    /// Configura las credenciales de Cloudflare. Debe llamarse antes de Start con tunnel.
    /// </summary>
    public void SetCloudflareConfig(CloudflareConfig config)
    {
        _cloudflareConfig = config;

        // Recrear el tunnel manager con la nueva config
        if (_tunnelManager != null)
        {
            _tunnelManager.OnStatusChanged -= TunnelManager_OnStatusChanged;
            _tunnelManager.OnUrlGenerated -= TunnelManager_OnUrlGenerated;
            _tunnelManager.Dispose();
        }

        _tunnelManager = new CloudflareTunnelManager(_port, config);
        _tunnelManager.OnStatusChanged += TunnelManager_OnStatusChanged;
        _tunnelManager.OnUrlGenerated += TunnelManager_OnUrlGenerated;
    }

    /// <summary>
    /// Desactiva el túnel: detiene el proceso cloudflared local y elimina los recursos
    /// asociados (cfd_tunnel + CNAME) del lado de Cloudflare. Pensado para que la UI
    /// lo llame cuando el usuario apaga el switch del túnel — así Cloudflare queda
    /// siempre limpio sin acumular recursos huérfanos.
    /// </summary>
    public async Task<bool> DesactivarYLimpiarTunelAsync()
    {
        _isTunnelEnabled = false;
        _isTunnelConnected = false;

        if (_tunnelManager == null) return true;

        bool ok = await _tunnelManager.EliminarRecursosCloudflareAsync();
        EmitStateChanged();
        return ok;
    }

    private void TunnelManager_OnStatusChanged(object sender, string e)
    {
        // Detectar cambio de conexión del túnel por el icono de estado
        bool wasConnected = _isTunnelConnected;
        if (e.Contains("🟢") && e.Contains("Establecido"))
            _isTunnelConnected = true;
        else if (e.Contains("🔴") || e.Contains("Detenido"))
            _isTunnelConnected = false;

        OnTunnelStatusChanged?.Invoke(this, e);

        if (wasConnected != _isTunnelConnected)
            EmitStateChanged();
    }

    private void TunnelManager_OnUrlGenerated(object sender, string e)
    {
        OnTunnelUrlGenerated?.Invoke(this, e);
    }

    /// <summary>
    /// Arranca el Web Server interno y opcionalmente el puente de red
    /// </summary>
    public async Task Start(bool enableTunnel = false)
    {
        Stop(); // Limpiar basuras previas
        _isTunnelEnabled = enableTunnel;
        _isTunnelConnected = false;

        try
        {
            StartOwinInternally();

            if (enableTunnel)
            {
                if (_tunnelManager == null || _cloudflareConfig == null || !_cloudflareConfig.IsValid)
                {
                    Log.Warning("No se puede iniciar el túnel: configuración de Cloudflare no establecida o incompleta.");
                    OnTunnelStatusChanged?.Invoke(this, "🔴 Configuración de Cloudflare incompleta. Configure antes de iniciar.");
                    return;
                }

                Log.Information("Arrancando Cloudflare Named Tunnel...");
                await _tunnelManager.StartAsync();
            }

            _owinWatchdogTimer.Start();
            EmitStateChanged();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Falló el arranque del servidor OWIN interno");
            OnServerStatusChanged?.Invoke(this, $"🔴 Error de Motor OWIN: {ex.Message}");
        }
    }

    private void StartOwinInternally()
    {
        if (_owinServer != null) return; // Ya está corriendo

        string allInterfacesUrl = $"http://+:{_port}/";
        string localhostUrl = $"http://localhost:{_port}/";

        // Verificar si la URL ACL ya está registrada en el sistema
        bool aclExists = IsUrlAclRegistered();

        if (aclExists)
        {
            // La ACL existe → usar exclusivamente http://+:port (localhost genera conflicto)
            _owinServer = WebApp.Start<Server.Startup>(allInterfacesUrl);
            _listeningUrl = allInterfacesUrl;
            //Log.Information($"API HTTP escuchando en TODAS las interfaces (puerto {_port})");
            OnServerStatusChanged?.Invoke(this, $"🟢 API HTTP escuchando en todas las interfaces (puerto {_port})");
            EmitLocalUrl();
            return;
        }

        // No hay ACL → solicitar elevación UAC
        Log.Warning("Sin permisos para escuchar en todas las interfaces. Solicitando elevación...");
        OnServerStatusChanged?.Invoke(this, "🟠 Se requiere permiso para aceptar conexiones por IP LAN...");

        if (TryRegisterUrlAcl())
        {
            // ACL registrada acabamos de registrar → usar http://+:port
            _owinServer = WebApp.Start<Server.Startup>(allInterfacesUrl);
            _listeningUrl = allInterfacesUrl;
            //Log.Information($"URL ACL registrada. API HTTP escuchando en TODAS las interfaces (puerto {_port})");
            OnServerStatusChanged?.Invoke(this, $"🟢 API HTTP escuchando en todas las interfaces (puerto {_port})");
            EmitLocalUrl();
            return;
        }

        // UAC rechazado → fallback seguro a localhost (no hay ACL, así que no hay conflicto)
        _owinServer = WebApp.Start<Server.Startup>(localhostUrl);
        _listeningUrl = localhostUrl;
        //Log.Warning("Escuchando solo en localhost (sin acceso por IP LAN). Acepte el permiso UAC para habilitar acceso LAN.");
        OnServerStatusChanged?.Invoke(this, $"🟠 API HTTP solo en localhost:{_port} (sin acceso LAN)");
        EmitLocalUrl();
    }

    /// <summary>
    /// Emite la URL local accesible hacia la UI.
    /// </summary>
    private void EmitLocalUrl()
    {
        string localUrl;
        if (IsListeningOnAllInterfaces)
        {
            // Mostrar la primera IP de red local del equipo
            string localIp = GetLocalIpAddress();
            localUrl = $"http://{localIp}:{_port}";
        }
        else
        {
            localUrl = $"http://localhost:{_port}";
        }
        OnLocalUrlGenerated?.Invoke(this, localUrl);
    }

    /// <summary>
    /// Obtiene la IP local de la primera interfaz de red activa.
    /// </summary>
    private static string GetLocalIpAddress()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
            }
        }
        catch { }
        return "localhost";
    }

    /// <summary>
    /// Verifica si la URL ACL para http://+:{port}/ ya está registrada en Windows.
    /// </summary>
    private bool IsUrlAclRegistered()
    {
        try
        {
            var psi = new ProcessStartInfo("netsh", "http show urlacl")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            var process = Process.Start(psi);
            if (process == null) return false;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            string expectedUrl = $"http://+:{_port}/";
            return output.IndexOf(expectedUrl, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch (Exception ex)
        {
            Log.Debug($"Error verificando URL ACL: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Registra la URL ACL via netsh con elevación UAC.
    /// Usa el SID S-1-1-0 (Everyone/TODOS) para compatibilidad con cualquier idioma del SO.
    /// </summary>
    private bool TryRegisterUrlAcl()
    {
        try
        {
            // Resolver nombre localizado del grupo "Everyone" (TODOS en español, Everyone en inglés)
            var sid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            string everyoneName = sid.Translate(typeof(NTAccount)).Value;

            string arguments = $"http add urlacl url=http://+:{_port}/ user=\"{everyoneName}\"";

            var psi = new ProcessStartInfo("netsh", arguments)
            {
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            var process = Process.Start(psi);
            process?.WaitForExit(10000); // Timeout de 10 segundos

            bool success = process?.ExitCode == 0;

            if (success)
            {
                Log.Information($"URL ACL registrada exitosamente para usuario: {everyoneName}");
            }
            else
            {
                Log.Warning($"No se pudo registrar URL ACL (exit code: {process?.ExitCode})");
            }

            return success;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // El usuario rechazó el UAC
            Log.Warning("El usuario rechazó la elevación UAC. Continuando con localhost.");
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning($"Error al intentar registrar URL ACL: {ex.Message}");
            return false;
        }
    }

    private async void Watchdog_CheckOwinHealth(object sender, System.Timers.ElapsedEventArgs e)
    {
        // Revisamos si alguien detuvo el engine por completo
        if (_owinServer == null) return;

        try
        {
            // El watchdog siempre verifica contra localhost (funciona en ambos modos)
            using (var hc = new HttpClient { Timeout = TimeSpan.FromSeconds(3) })
            {
                var response = await hc.GetAsync($"http://localhost:{_port}/api/webhooks/ping");
                response.EnsureSuccessStatusCode();
            }
        }
        catch (Exception)
        {
            // El servidor no respondió 2xx o rechazó la conexión. ¡Se cayó el Listener!
            Log.Warning("El Listener OWIN dejó de responder peticiones.");
            OnServerStatusChanged?.Invoke(this, "Detectada caída del servidor HTTP (Auto-Reconectando...)");

            // Reanimarlo
            _owinServer?.Dispose();
            _owinServer = null;

            try
            {
                StartOwinInternally();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Intento de auto-reconexión OWIN fallido");
            }
        }
    }

    public void Stop()
    {
        _owinWatchdogTimer?.Stop();

        _owinServer?.Dispose();
        _owinServer = null;

        _tunnelManager?.Stop();

        _isTunnelEnabled = false;
        _isTunnelConnected = false;
        _listeningUrl = string.Empty;

        //
        //
        //Log.Information("API HTTP Apagado");
        OnServerStatusChanged?.Invoke(this, "🔴 API HTTP Apagado");
        EmitStateChanged();
    }

    /// <summary>
    /// Reset completo del túnel: elimina config local y cloudflared.exe.
    /// El túnel persiste en Cloudflare para reutilización futura.
    /// </summary>
    public void ResetTunnel()
    {
        if (_tunnelManager == null)
        {
            Log.Warning("No hay Tunnel Manager configurado para resetear.");
            return;
        }

        _tunnelManager.Reset();
        Log.Information("Reset de túnel Cloudflare completado");
    }

    /// <summary>
    /// Diagnóstico on-demand: valida que el cfd_tunnel y el CNAME existan en Cloudflare
    /// y re-provisiona los recursos faltantes. Pensado para un botón en la UI que el
    /// usuario aprieta cuando sospecha que el túnel quedó huérfano. Si el túnel estaba
    /// corriendo, lo reinicia con la config nueva.
    /// </summary>
    public async Task<bool> RevalidarYReprovisionarTunelAsync()
    {
        if (_tunnelManager == null)
        {
            Log.Warning("No hay Tunnel Manager configurado para revalidar.");
            return false;
        }

        bool ok = await _tunnelManager.ValidarYRepararAsync();
        EmitStateChanged();
        return ok;
    }

    // --- Métodos Invocados Internamente por los Controllers ---

    /// <summary>
    /// Procesador async opcional. Si esta seteado, el controller espera su resultado y
    /// responde 200/404/503 al sender segun corresponda. Si es null, se cae al comportamiento
    /// legacy (siempre 200) y solo se dispara el evento OnWebhookReceived.
    /// </summary>
    public Func<string, string, IDictionary<string, string>, Task<WebhookProcessResult>> Processor { get; set; }

    internal void TriggerWebhookReceived(string source, string rawJson)
    {
        TriggerWebhookReceived(source, rawJson, null);
    }

    internal void TriggerWebhookReceived(string source, string rawJson, IDictionary<string, string> headers)
    {
        //Log.Information($"Webhook recibido internamente origen: [{source}] | Largo: {rawJson?.Length} chars");
        OnWebhookReceived?.Invoke(this, new WebhookPayloadEventArgs
        {
            Source = source,
            RawJson = rawJson,
            Headers = headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        });
    }

    /// <summary>
    /// Dispara el evento de notificacion (UI/logging) y, si hay Processor registrado,
    /// devuelve su resultado para que el controller responda con el status HTTP apropiado.
    /// </summary>
    internal async Task<WebhookProcessResult> TriggerWebhookProcessAsync(string source, string rawJson, IDictionary<string, string> headers)
    {
        // 1) Disparar el evento legacy (UI logging). No bloquea ni afecta el resultado.
        TriggerWebhookReceived(source, rawJson, headers);

        // 2) Si no hay processor registrado: comportamiento histórico (200 OK).
        var processor = Processor;
        if (processor == null) return WebhookProcessResult.Processed;

        try
        {
            return await processor(source, rawJson, headers);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error en WebhookHost.Processor para source '{source}'");
            return WebhookProcessResult.NotProcessed;
        }
    }

    internal void TriggerWebhookError(string source, string error)
    {
        Log.Error($"Error procesando webhook interno [{source}]: {error}");
        Debug.WriteLine($"Error en webhook [{source}]: {error}");
    }

    internal void TriggerLogEmitted(string level, string message)
    {
        OnSystemLog?.Invoke(this, new LogEventArgs { Level = level, Message = message });
    }

    /// <summary>
    /// Emite el estado actual del engine a los suscriptores.
    /// </summary>
    private void EmitStateChanged()
    {
        var state = new EngineState
        {
            IsRunning = IsRunning,
            IsTunnelEnabled = _isTunnelEnabled,
            IsTunnelConnected = _isTunnelConnected,
            Mode = CurrentMode
        };
        OnStateChanged?.Invoke(this, state);
    }

    public void Dispose()
    {
        Stop();
        WebhookClientBroker.Unbind();
        _tunnelManager?.Dispose();
        // Solo el logger del engine. NUNCA Log.CloseAndFlush(): eso apagaba el logger
        // global, que pertenece a la aplicación anfitriona. Ver EngineLog.
        EngineLog.Shutdown();
    }
}

/// <summary>
/// Modos de operación del engine.
/// </summary>
public enum EngineMode
{
    /// <summary>Detenido completamente.</summary>
    Stopped,
    /// <summary>Servidor HTTP activo, solo accesible localmente (o por LAN).</summary>
    Local,
    /// <summary>Servidor HTTP activo + túnel Cloudflare conectado (accesible desde internet).</summary>
    Public
}

/// <summary>
/// Estado actual del engine, emitido por OnStateChanged.
/// </summary>
public class EngineState : EventArgs
{
    public bool IsRunning { get; set; }
    public bool IsTunnelEnabled { get; set; }
    public bool IsTunnelConnected { get; set; }
    public EngineMode Mode { get; set; }
}

public class WebhookPayloadEventArgs : EventArgs
{
    public string? Source { get; set; }
    public string? RawJson { get; set; }
    /// <summary>
    /// Headers HTTP relevantes capturados de la peticion entrante.
    /// Las claves son case-insensitive (StringComparer.OrdinalIgnoreCase).
    /// </summary>
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Resultado de procesar un webhook. El controller mapea cada valor a un status HTTP
/// para que el sistema externo (worker/MP/WC/etc.) sepa si debe reintentar.
/// </summary>
public enum WebhookProcessResult
{
    /// <summary>El webhook fue procesado correctamente (200 OK).</summary>
    Processed,
    /// <summary>No hay provider registrado para ese source (404 - no reintentar).</summary>
    NoProvider,
    /// <summary>El provider existe pero no pudo procesar (503 - reintentar).</summary>
    NotProcessed
}

public class LogEventArgs : EventArgs
{
    public string? Level { get; set; }
    public string? Message { get; set; }
}

public class UiEventLogSink : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        // Intercepta todo lo que escupe Serilog y lo empuja al form visual
        string levelStr = logEvent.Level.ToString().Substring(0, 3).ToUpper();
        string msg = logEvent.RenderMessage();
        if (logEvent.Exception != null) msg += "\n" + logEvent.Exception.ToString();

        WebhookHost.Instance?.TriggerLogEmitted(levelStr, msg);
    }
}
