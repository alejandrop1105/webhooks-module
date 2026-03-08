using System;
using System.Diagnostics;
using System.Net.Http;
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
    private IDisposable _owinServer;
    private CloudflareTunnelManager _tunnelManager;
    private readonly int _port;
    private readonly System.Timers.Timer _owinWatchdogTimer;
    private bool _enableTunnelFlag;

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
    /// Cuando Cloudflare nos otorga una URL .trycloudflare.com pública
    /// </summary>
    public event EventHandler<string> OnTunnelUrlGenerated;

    /// <summary>
    /// Emite los logs de Serilog a la UI
    /// </summary>
    public event EventHandler<LogEventArgs> OnSystemLog;

    // --- FIN MEGAFONOS ---

    // Singleton para acceso fácil desde el WebhooksController
    public static WebhookHost Instance { get; private set; }

    public WebhookHost(int port = 5000)
    {
        _port = port;
        Instance = this;

        _tunnelManager = new CloudflareTunnelManager(port);
        _tunnelManager.OnStatusChanged += (s, ev) => OnTunnelStatusChanged?.Invoke(this, ev);
        _tunnelManager.OnUrlGenerated += (s, ev) => OnTunnelUrlGenerated?.Invoke(this, ev);

        // Inicializar Serilog Rotativo (Retención 5 días) y Sink UI
        string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "webhook-engine-.txt");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 5,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Sink(new UiEventLogSink())
            .CreateLogger();

        Log.Information("====== Webhook Engine Inicializado ======");

        // Watchdog de Salud Interna (Verifica que OWIN responda HTTP)
        _owinWatchdogTimer = new System.Timers.Timer(10000); // 10 segundos
        _owinWatchdogTimer.Elapsed += Watchdog_CheckOwinHealth;
    }

    /// <summary>
    /// Arranca el Web Server interno y opcionalmente el puente de red
    /// </summary>
    public void Start(bool enableTunnel = false)
    {
        _enableTunnelFlag = enableTunnel;
        Stop(); // Limpiar basuras previas

        try
        {
            StartOwinInternally();

            if (enableTunnel)
            {
                Log.Information("Arrancando Cloudflare Tunnel Manager...");
                // Disparamos la auto-descarga y vigilancia de forma asincrónica
                _ = _tunnelManager.StartAsync();
            }

            _owinWatchdogTimer.Start();
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

        // BYPASS de Permisos Administrador usando localhost estricto
        string url = $"http://localhost:{_port}/";

        // Levantamos OWIN (Este hilo no bloquea la interfaz)
        _owinServer = WebApp.Start<Server.Startup>(url);

        Log.Information($"API HTTP interna escuchando en {url}");
        OnServerStatusChanged?.Invoke(this, $"🟢 API HTTP escuchando en {url}");
    }

    private async void Watchdog_CheckOwinHealth(object sender, System.Timers.ElapsedEventArgs e)
    {
        // Revisamos si alguien detuvo el engine por completo
        if (_owinServer == null) return;

        try
        {
            using (var hc = new HttpClient { Timeout = TimeSpan.FromSeconds(3) })
            {
                var response = await hc.GetAsync($"http://localhost:{_port}/api/webhooks/ping");
                response.EnsureSuccessStatusCode();
            }
        }
        catch (Exception)
        {
            // El servidor no respondió 2xx o rechazó la conexión. ¡Se cayó el Listener!
            Log.Warning("⚠️ El Listener OWIN dejó de responder peticiones.");
            OnServerStatusChanged?.Invoke(this, "🟠 Detectada caída del servidor HTTP (Auto-Reconectando...)");

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

        Log.Information("API HTTP Apagado");
        OnServerStatusChanged?.Invoke(this, "🔴 API HTTP Apagado");
    }

    // --- Métodos Invocados Internamente por los Controllers ---

    internal void TriggerWebhookReceived(string source, string rawJson)
    {
        Log.Information($"Webhook recibido internamente origen: [{source}] | Largo: {rawJson?.Length} chars");
        OnWebhookReceived?.Invoke(this, new WebhookPayloadEventArgs { Source = source, RawJson = rawJson });
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

    public void Dispose()
    {
        Stop();
        Log.CloseAndFlush();
    }
}

public class WebhookPayloadEventArgs : EventArgs
{
    public string Source { get; set; }
    public string RawJson { get; set; }
}

public class LogEventArgs : EventArgs
{
    public string Level { get; set; }
    public string Message { get; set; }
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
