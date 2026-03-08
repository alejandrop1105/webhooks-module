using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;

namespace Webhooks.LegacyEngine.Managers;

/// <summary>
/// Encargado de administrar el ciclo de vida, auto-descarga y vigilancia (watchdog)
/// del proceso cloudflared.exe
/// </summary>
public class CloudflareTunnelManager : IDisposable
{
    private Process _process;
    private readonly System.Timers.Timer _watchdogTimer;
    private readonly int _localPort;
    private readonly string _exePath;
    private bool _isInstalling;
    private bool _stopRequested;

    // Eventos que dispararemos al WebhookHost
    public event EventHandler<string> OnUrlGenerated;
    public event EventHandler<string> OnStatusChanged;

    public CloudflareTunnelManager(int localPort)
    {
        _localPort = localPort;
        _exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cloudflared.exe");

        // Vigilar cada 5 segundos si el proceso se cayó
        _watchdogTimer = new System.Timers.Timer(5000);
        _watchdogTimer.Elapsed += WatchdogTimer_Elapsed;
    }

    public async Task StartAsync()
    {
        _stopRequested = false;

        if (_isInstalling) return;

        // Auto-Descarga si no existe
        if (!File.Exists(_exePath))
        {
            var systemPath = Environment.GetEnvironmentVariable("PATH");
            var paths = systemPath?.Split(';') ?? Array.Empty<string>();
            bool existsInPath = paths.Any(p => File.Exists(Path.Combine(p, "cloudflared.exe")));

            if (!existsInPath)
            {
                await DownloadBinaryAsync();
                if (!File.Exists(_exePath)) return;
            }
        }

        StartBackgroundProcess();
    }

    public void Stop()
    {
        _stopRequested = true;
        _watchdogTimer.Stop();
        KillProcessSafely();
        NotifyStatus("Detenido intencionalmente", "🔴");
    }

    private void StartBackgroundProcess()
    {
        KillProcessSafely(); // Limpiar por si acaso

        // Destrucción de Zombies: Eliminar cualquier cloudflared huérfano que cause loops o bloquee puertos.
        foreach (var proc in Process.GetProcessesByName("cloudflared"))
        {
            try { proc.Kill(); } catch { }
        }

        NotifyStatus("Iniciando Proceso...", "🟡");

        try
        {
            _process = new Process();
            string finalPath = File.Exists(_exePath) ? _exePath : "cloudflared";
            _process.StartInfo.FileName = finalPath;
            // Se le miente a HttpListener forzando que Cloudflare re-escriba el Header original de la Nube por el esperado "localhost"
            _process.StartInfo.Arguments = $"tunnel --url http://localhost:{_localPort} --http-host-header localhost";
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.RedirectStandardError = true;
            _process.StartInfo.CreateNoWindow = true;

            _process.ErrorDataReceived += Process_ErrorDataReceived;

            _process.Start();
            _process.BeginErrorReadLine();

            _watchdogTimer.Start();
            NotifyStatus("Esperando Handshake con Cloudflare...", "🟡");
        }
        catch (Exception ex)
        {
            NotifyStatus($"Error al iniciar: {ex.Message}", "🔴");
        }
    }

    private async Task DownloadBinaryAsync()
    {
        _isInstalling = true;
        NotifyStatus("Auto-Instalando cloudflared.exe (60MB)...", "🟠");

        try
        {
            using (var hc = new HttpClient())
            {
                var response = await hc.GetAsync("https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe");
                response.EnsureSuccessStatusCode();

                using (var fs = new FileStream(_exePath, FileMode.Create))
                {
                    await response.Content.CopyToAsync(fs);
                }
            }
            NotifyStatus("Instalación Completa", "🟢");
        }
        catch (Exception ex)
        {
            NotifyStatus($"Error de Instalación: {ex.Message}", "🔴");
        }
        finally
        {
            _isInstalling = false;
        }
    }

    private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data)) return;

        // DIAGNÓSTICO PROFUNDO: Imprimir errores HTTP o caídas del proxy a la UI.
        if (e.Data.Contains("ERR ") || e.Data.Contains("INF ") || e.Data.Contains("WRN ") || e.Data.Contains("HTTP"))
        {
            Serilog.Log.Debug($"[Cloudflared Raw] {e.Data}");
            // Si es un error real de ruteo como 404 o 502, lo mostramos en naranja en la UI
            if (e.Data.Contains("ERR") || e.Data.Contains("404") || e.Data.Contains("502") || e.Data.Contains("400"))
            {
                Serilog.Log.Warning($"[Proxy Log] {e.Data}");
            }
        }

        var match = Regex.Match(e.Data, @"https://[a-zA-Z0-9-]+\.trycloudflare\.com");
        if (match.Success)
        {
            NotifyStatus("Establecido y Público", "🟢");
            OnUrlGenerated?.Invoke(this, match.Value);
        }
    }

    private void WatchdogTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        if (_stopRequested) return;

        if (_process == null || _process.HasExited)
        {
            // ¡Detectamos una caída! El ejecutable se cerró de repente.
            _watchdogTimer.Stop(); // Pausar watchdog
            NotifyStatus("Detectada caída de red (Auto-Reconectando...)", "🟠");

            // AutoReconectar silenciosamente
            StartBackgroundProcess();
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

    private void NotifyStatus(string message, string icon)
    {
        OnStatusChanged?.Invoke(this, $"{icon} {message}");
    }

    public void Dispose()
    {
        Stop();
    }
}
