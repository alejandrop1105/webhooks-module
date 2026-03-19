using System;
using System.Collections.Generic;

namespace Webhooks.LegacyEngine.Engine;

/// <summary>
/// Broker estático que permite registrar clientes para recibir eventos del WebhookHost.
/// Los registros pueden hacerse ANTES de instanciar el WebhookHost.
/// Cuando el Host se instancia, conecta automáticamente a todos los clientes pre-registrados.
/// Cada método Register devuelve un IDisposable para desuscribirse individualmente.
/// </summary>
public static class WebhookClientBroker
{
    private static readonly object _lock = new object();

    // Colecciones de suscripciones
    private static readonly List<WebhookSubscription> _webhookClients = new List<WebhookSubscription>();
    private static readonly List<Action<EngineState>> _stateClients = new List<Action<EngineState>>();
    private static readonly List<Action<string>> _tunnelStatusClients = new List<Action<string>>();
    private static readonly List<Action<string>> _tunnelUrlClients = new List<Action<string>>();
    private static readonly List<Action<string>> _serverStatusClients = new List<Action<string>>();
    private static readonly List<Action<string>> _localUrlClients = new List<Action<string>>();
    private static readonly List<Action<string, string>> _logClients = new List<Action<string, string>>();

    // Referencia al host activo
    private static WebhookHost _boundHost;

    // ──────────────────────────────────────────────
    // Métodos de Registro (devuelven IDisposable)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Registra un cliente para recibir webhooks filtrados por source.
    /// Ejemplo: var sub = RegisterClient_WebhookReceived("woocommerce", (source, json) => { ... });
    /// Para desuscribirse: sub.Dispose();
    /// </summary>
    public static IDisposable RegisterClient_WebhookReceived(string source, Action<string, string> callback)
    {
        var subscription = new WebhookSubscription
        {
            SourceFilter = source,
            Callback = callback
        };

        lock (_lock) { _webhookClients.Add(subscription); }

        return new BrokerSubscription(() =>
        {
            lock (_lock) { _webhookClients.Remove(subscription); }
        });
    }

    /// <summary>
    /// Registra un cliente para recibir TODOS los webhooks sin filtro.
    /// </summary>
    public static IDisposable RegisterClient_WebhookReceived(Action<string, string> callback)
    {
        var subscription = new WebhookSubscription
        {
            SourceFilter = null,
            Callback = callback
        };

        lock (_lock) { _webhookClients.Add(subscription); }

        return new BrokerSubscription(() =>
        {
            lock (_lock) { _webhookClients.Remove(subscription); }
        });
    }

    /// <summary>
    /// Registra un cliente para recibir cambios de estado del engine.
    /// </summary>
    public static IDisposable RegisterClient_StateChanged(Action<EngineState> callback)
    {
        lock (_lock) { _stateClients.Add(callback); }
        return new BrokerSubscription(() => { lock (_lock) { _stateClients.Remove(callback); } });
    }

    /// <summary>
    /// Registra un cliente para recibir cambios de estado del túnel.
    /// </summary>
    public static IDisposable RegisterClient_TunnelStatusChanged(Action<string> callback)
    {
        lock (_lock) { _tunnelStatusClients.Add(callback); }
        return new BrokerSubscription(() => { lock (_lock) { _tunnelStatusClients.Remove(callback); } });
    }

    /// <summary>
    /// Registra un cliente para recibir la URL pública cuando se genera.
    /// </summary>
    public static IDisposable RegisterClient_TunnelUrlGenerated(Action<string> callback)
    {
        lock (_lock) { _tunnelUrlClients.Add(callback); }
        return new BrokerSubscription(() => { lock (_lock) { _tunnelUrlClients.Remove(callback); } });
    }

    /// <summary>
    /// Registra un cliente para recibir cambios de estado del servidor HTTP.
    /// </summary>
    public static IDisposable RegisterClient_ServerStatusChanged(Action<string> callback)
    {
        lock (_lock) { _serverStatusClients.Add(callback); }
        return new BrokerSubscription(() => { lock (_lock) { _serverStatusClients.Remove(callback); } });
    }

    /// <summary>
    /// Registra un cliente para recibir la URL local cuando se genera.
    /// </summary>
    public static IDisposable RegisterClient_LocalUrlGenerated(Action<string> callback)
    {
        lock (_lock) { _localUrlClients.Add(callback); }
        return new BrokerSubscription(() => { lock (_lock) { _localUrlClients.Remove(callback); } });
    }

    /// <summary>
    /// Registra un cliente para recibir logs del sistema.
    /// </summary>
    public static IDisposable RegisterClient_SystemLog(Action<string, string> callback)
    {
        lock (_lock) { _logClients.Add(callback); }
        return new BrokerSubscription(() => { lock (_lock) { _logClients.Remove(callback); } });
    }

    // ──────────────────────────────────────────────
    // Desregistro masivo
    // ──────────────────────────────────────────────

    /// <summary>
    /// Elimina TODAS las suscripciones registradas.
    /// </summary>
    public static void UnregisterAll()
    {
        lock (_lock)
        {
            _webhookClients.Clear();
            _stateClients.Clear();
            _tunnelStatusClients.Clear();
            _tunnelUrlClients.Clear();
            _serverStatusClients.Clear();
            _localUrlClients.Clear();
            _logClients.Clear();
        }
    }

    // ──────────────────────────────────────────────
    // Bind / Unbind (llamado internamente por WebhookHost)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Conecta el broker a una instancia de WebhookHost.
    /// </summary>
    internal static void Bind(WebhookHost host)
    {
        Unbind();

        _boundHost = host;

        host.OnWebhookReceived += Host_OnWebhookReceived;
        host.OnStateChanged += Host_OnStateChanged;
        host.OnTunnelStatusChanged += Host_OnTunnelStatusChanged;
        host.OnTunnelUrlGenerated += Host_OnTunnelUrlGenerated;
        host.OnServerStatusChanged += Host_OnServerStatusChanged;
        host.OnLocalUrlGenerated += Host_OnLocalUrlGenerated;
        host.OnSystemLog += Host_OnSystemLog;

        Serilog.Log.Information($"WebhookClientBroker vinculado al host (clientes webhook: {_webhookClients.Count})");
    }

    /// <summary>
    /// Desconecta el broker del WebhookHost actual.
    /// </summary>
    internal static void Unbind()
    {
        if (_boundHost == null) return;

        _boundHost.OnWebhookReceived -= Host_OnWebhookReceived;
        _boundHost.OnStateChanged -= Host_OnStateChanged;
        _boundHost.OnTunnelStatusChanged -= Host_OnTunnelStatusChanged;
        _boundHost.OnTunnelUrlGenerated -= Host_OnTunnelUrlGenerated;
        _boundHost.OnServerStatusChanged -= Host_OnServerStatusChanged;
        _boundHost.OnLocalUrlGenerated -= Host_OnLocalUrlGenerated;
        _boundHost.OnSystemLog -= Host_OnSystemLog;

        _boundHost = null;
        Serilog.Log.Information("WebhookClientBroker desvinculado del host");
    }

    // ──────────────────────────────────────────────
    // Distribución de eventos a clientes
    // ──────────────────────────────────────────────

    private static void Host_OnWebhookReceived(object sender, WebhookPayloadEventArgs e)
    {
        List<WebhookSubscription> snapshot;
        lock (_lock) { snapshot = new List<WebhookSubscription>(_webhookClients); }

        foreach (var sub in snapshot)
        {
            try
            {
                if (sub.SourceFilter == null ||
                    string.Equals(sub.SourceFilter, e.Source, StringComparison.OrdinalIgnoreCase))
                {
                    sub.Callback?.Invoke(e.Source ?? string.Empty, e.RawJson ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Error en cliente webhook (source filter: {sub.SourceFilter ?? "*"})");
            }
        }
    }

    private static void Host_OnStateChanged(object sender, EngineState e)
    {
        List<Action<EngineState>> snapshot;
        lock (_lock) { snapshot = new List<Action<EngineState>>(_stateClients); }

        foreach (var cb in snapshot)
        {
            try { cb?.Invoke(e); }
            catch (Exception ex) { Serilog.Log.Error(ex, "Error en cliente de estado"); }
        }
    }

    private static void Host_OnTunnelStatusChanged(object sender, string e)
    {
        List<Action<string>> snapshot;
        lock (_lock) { snapshot = new List<Action<string>>(_tunnelStatusClients); }

        foreach (var cb in snapshot)
        {
            try { cb?.Invoke(e); }
            catch (Exception ex) { Serilog.Log.Error(ex, "Error en cliente de túnel status"); }
        }
    }

    private static void Host_OnTunnelUrlGenerated(object sender, string e)
    {
        List<Action<string>> snapshot;
        lock (_lock) { snapshot = new List<Action<string>>(_tunnelUrlClients); }

        foreach (var cb in snapshot)
        {
            try { cb?.Invoke(e); }
            catch (Exception ex) { Serilog.Log.Error(ex, "Error en cliente de túnel URL"); }
        }
    }

    private static void Host_OnServerStatusChanged(object sender, string e)
    {
        List<Action<string>> snapshot;
        lock (_lock) { snapshot = new List<Action<string>>(_serverStatusClients); }

        foreach (var cb in snapshot)
        {
            try { cb?.Invoke(e); }
            catch (Exception ex) { Serilog.Log.Error(ex, "Error en cliente de server status"); }
        }
    }

    private static void Host_OnLocalUrlGenerated(object sender, string e)
    {
        List<Action<string>> snapshot;
        lock (_lock) { snapshot = new List<Action<string>>(_localUrlClients); }

        foreach (var cb in snapshot)
        {
            try { cb?.Invoke(e); }
            catch (Exception ex) { Serilog.Log.Error(ex, "Error en cliente de local URL"); }
        }
    }

    private static void Host_OnSystemLog(object sender, LogEventArgs e)
    {
        List<Action<string, string>> snapshot;
        lock (_lock) { snapshot = new List<Action<string, string>>(_logClients); }

        foreach (var cb in snapshot)
        {
            try { cb?.Invoke(e.Level ?? string.Empty, e.Message ?? string.Empty); }
            catch (Exception ex) { Serilog.Log.Error(ex, "Error en cliente de logs"); }
        }
    }
}

/// <summary>
/// Suscripción interna de webhook con filtro opcional por source.
/// </summary>
internal class WebhookSubscription
{
    public string SourceFilter { get; set; }
    public Action<string, string> Callback { get; set; }
}

/// <summary>
/// Token de suscripción devuelto por cada RegisterClient.
/// Llamar Dispose() para desuscribirse individualmente.
/// </summary>
internal class BrokerSubscription : IDisposable
{
    private Action _unsubscribe;
    private bool _disposed;

    public BrokerSubscription(Action unsubscribe)
    {
        _unsubscribe = unsubscribe;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _unsubscribe?.Invoke();
        _unsubscribe = null;
    }
}
