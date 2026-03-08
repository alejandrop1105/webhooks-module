using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR.Client;

namespace SampleErpWinForms;

/// <summary>
/// Formulario principal que muestra cómo integrar con el sistema de webhooks
/// usando SignalR para notificaciones en tiempo real.
/// </summary>
public partial class MainForm : Form
{
    private HubConnection? _hubConnection;
    private bool _isPaused = false;
    private readonly BindingSource _eventsBindingSource = new();
    private readonly List<WebhookEventItem> _events = new();

    // Controles UI
    private TextBox txtServerUrl = null!;
    private Button btnConnect = null!;
    private Button btnDisconnect = null!;
    private Button btnPause = null!;
    private Label lblStatus = null!;
    private DataGridView dgvEvents = null!;
    private RichTextBox txtLog = null!;
    private ComboBox cmbFilter = null!;
    private Button btnClear = null!;

    // Cloudflare Tunnel
    private Process? _cloudflaredProcess;
    private System.Windows.Forms.Timer _tunnelMonitorTimer = null!;
    private Button btnStartTunnel = null!;
    private Button btnStopTunnel = null!;
    private Label lblTunnelStatus = null!;
    private TextBox txtTunnelUrl = null!;
    private bool _isInstallingCloudflared = false;

    public MainForm()
    {
        InitializeComponent();
        InitializeCustomControls();
        SetupDataGrid();
    }

    private void InitializeCustomControls()
    {
        this.Text = "ERP Webhook Client - Ejemplo de Integración";
        this.Size = new Size(1000, 700);
        this.StartPosition = FormStartPosition.CenterScreen;

        // Panel superior - Conexión
        var panelTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 130,
            Padding = new Padding(10),
            BackColor = Color.FromArgb(45, 45, 48)
        };

        var lblUrl = new Label
        {
            Text = "URL del Servidor:",
            ForeColor = Color.White,
            Location = new Point(10, 15),
            AutoSize = true
        };

        txtServerUrl = new TextBox
        {
            Text = "http://localhost:5000/hubs/webhooks",
            Location = new Point(120, 12),
            Width = 350,
            Font = new Font("Consolas", 9)
        };

        btnConnect = new Button
        {
            Text = "▶ Conectar",
            Location = new Point(490, 10),
            Size = new Size(100, 28),
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnConnect.Click += BtnConnect_Click;

        btnDisconnect = new Button
        {
            Text = "⬛ Detener",
            Location = new Point(600, 10),
            Size = new Size(100, 28),
            BackColor = Color.FromArgb(200, 50, 50),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        btnDisconnect.Click += BtnDisconnect_Click;

        btnPause = new Button
        {
            Text = "⏸ Pausar",
            Location = new Point(710, 10),
            Size = new Size(100, 28),
            BackColor = Color.FromArgb(200, 150, 0),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        btnPause.Click += BtnPause_Click;

        lblStatus = new Label
        {
            Text = "● Desconectado",
            ForeColor = Color.Gray,
            Location = new Point(10, 50),
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        var lblFilter = new Label
        {
            Text = "Filtrar por fuente:",
            ForeColor = Color.White,
            Location = new Point(200, 50),
            AutoSize = true
        };

        cmbFilter = new ComboBox
        {
            Location = new Point(320, 47),
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbFilter.Items.AddRange(new[] { "Todos", "woocommerce", "stripe", "github", "shopify" });
        cmbFilter.SelectedIndex = 0;
        cmbFilter.SelectedIndexChanged += CmbFilter_SelectedIndexChanged;

        btnClear = new Button
        {
            Text = "🗑 Limpiar",
            Location = new Point(490, 45),
            Size = new Size(80, 25),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(100, 100, 100)
        };
        btnClear.Click += (s, e) => { _events.Clear(); RefreshGrid(); AppendLog("Lista limpiada"); };

        var lblTunnel = new Label { Text = "Cloudflare Tunnel:", ForeColor = Color.White, Location = new Point(10, 85), AutoSize = true };
        btnStartTunnel = new Button { Text = "⚡ Iniciar Túnel", Location = new Point(130, 82), Size = new Size(110, 28), BackColor = Color.FromArgb(244, 129, 32), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnStartTunnel.Click += BtnStartTunnel_Click;
        btnStopTunnel = new Button { Text = "⬛ Detener", Location = new Point(250, 82), Size = new Size(80, 28), BackColor = Color.DimGray, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Enabled = false };
        btnStopTunnel.Click += BtnStopTunnel_Click;
        lblTunnelStatus = new Label { Text = "● Apagado", ForeColor = Color.Gray, Location = new Point(340, 88), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        txtTunnelUrl = new TextBox { Text = "Esperando URL...", Location = new Point(440, 85), Width = 350, ReadOnly = true, Font = new Font("Consolas", 9), BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.Orange };

        panelTop.Controls.AddRange(new Control[] {
            lblUrl, txtServerUrl, btnConnect, btnDisconnect, btnPause,
            lblStatus, lblFilter, cmbFilter, btnClear,
            lblTunnel, btnStartTunnel, btnStopTunnel, lblTunnelStatus, txtTunnelUrl
        });

        _tunnelMonitorTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _tunnelMonitorTimer.Tick += TunnelMonitorTimer_Tick;

        // Panel central - Grid de eventos
        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 350,
            BackColor = Color.FromArgb(30, 30, 30)
        };

        dgvEvents = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White,
            GridColor = Color.FromArgb(60, 60, 60),
            BorderStyle = BorderStyle.None,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ReadOnly = true,
            AllowUserToAddRows = false,
            RowHeadersVisible = false
        };
        dgvEvents.DefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
        dgvEvents.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 122, 204);
        dgvEvents.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(60, 60, 60);
        dgvEvents.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        dgvEvents.EnableHeadersVisualStyles = false;

        txtLog = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 20, 20),
            ForeColor = Color.LightGreen,
            Font = new Font("Consolas", 9),
            ReadOnly = true,
            BorderStyle = BorderStyle.None
        };

        var lblLog = new Label
        {
            Text = "📋 Log de Actividad",
            Dock = DockStyle.Top,
            Height = 25,
            BackColor = Color.FromArgb(45, 45, 48),
            ForeColor = Color.White,
            Padding = new Padding(5),
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };

        var panelLog = new Panel { Dock = DockStyle.Fill };
        panelLog.Controls.Add(txtLog);
        panelLog.Controls.Add(lblLog);

        splitContainer.Panel1.Controls.Add(dgvEvents);
        splitContainer.Panel2.Controls.Add(panelLog);

        this.Controls.Add(splitContainer);
        this.Controls.Add(panelTop);
    }

    private void SetupDataGrid()
    {
        _eventsBindingSource.DataSource = _events;
        dgvEvents.DataSource = _eventsBindingSource;
    }

    private async void BtnConnect_Click(object? sender, EventArgs e)
    {
        try
        {
            AppendLog($"Conectando a {txtServerUrl.Text}...");
            UpdateStatus("Conectando...", Color.Orange);

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(txtServerUrl.Text)
                .WithAutomaticReconnect()
                .Build();

            // Configurar eventos
            _hubConnection.On<object>("Connected", OnConnected);
            _hubConnection.On<object>("WebhookReceived", OnWebhookReceived);
            _hubConnection.On<object>("WebhookProcessed", OnWebhookProcessed);
            _hubConnection.On<object>("WebhookFailed", OnWebhookFailed);
            _hubConnection.On<object>("SubscribedToAll", OnSubscribed);

            _hubConnection.Closed += async (error) =>
            {
                await Task.Delay(100);
                Invoke(() =>
                {
                    UpdateStatus("Desconectado", Color.Red);
                    AppendLog($"Conexión cerrada: {error?.Message ?? "Normal"}");
                    UpdateButtonStates(false);
                });
            };

            _hubConnection.Reconnecting += (error) =>
            {
                Invoke(() =>
                {
                    UpdateStatus("Reconectando...", Color.Orange);
                    AppendLog("Intentando reconectar...");
                });
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += (connectionId) =>
            {
                Invoke(async () =>
                {
                    UpdateStatus("Conectado", Color.LimeGreen);
                    AppendLog("Reconectado exitosamente");
                    await _hubConnection.InvokeAsync("SubscribeToAll");
                });
                return Task.CompletedTask;
            };

            await _hubConnection.StartAsync();
            await _hubConnection.InvokeAsync("SubscribeToAll");

            UpdateStatus("Conectado", Color.LimeGreen);
            AppendLog("Conexión establecida");
            UpdateButtonStates(true);
        }
        catch (Exception ex)
        {
            UpdateStatus("Error", Color.Red);
            AppendLog($"Error de conexión: {ex.Message}");
            MessageBox.Show($"Error al conectar: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnDisconnect_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_hubConnection != null)
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
            }

            UpdateStatus("Desconectado", Color.Gray);
            AppendLog("Desconectado del servidor");
            UpdateButtonStates(false);
            _isPaused = false;
        }
        catch (Exception ex)
        {
            AppendLog($"Error al desconectar: {ex.Message}");
        }
    }

    private void BtnPause_Click(object? sender, EventArgs e)
    {
        _isPaused = !_isPaused;

        if (_isPaused)
        {
            btnPause.Text = "▶ Reanudar";
            btnPause.BackColor = Color.FromArgb(0, 150, 0);
            UpdateStatus("Pausado", Color.Yellow);
            AppendLog("Recepción pausada (los mensajes se ignoran)");
        }
        else
        {
            btnPause.Text = "⏸ Pausar";
            btnPause.BackColor = Color.FromArgb(200, 150, 0);
            UpdateStatus("Conectado", Color.LimeGreen);
            AppendLog("Recepción reanudada");
        }
    }

    private void OnConnected(object data)
    {
        Invoke(() => AppendLog($"Conectado al hub: {JsonSerializer.Serialize(data)}"));
    }

    private void OnSubscribed(object data)
    {
        Invoke(() => AppendLog("Suscrito a todos los eventos"));
    }

    private void OnWebhookReceived(object data)
    {
        if (_isPaused) return;

        Invoke(() =>
        {
            try
            {
                var json = JsonSerializer.Serialize(data);
                var evt = JsonSerializer.Deserialize<WebhookNotification>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (evt != null && ShouldShowEvent(evt.Source))
                {
                    _events.Insert(0, new WebhookEventItem
                    {
                        EventId = evt.EventId,
                        Source = evt.Source,
                        EventType = evt.EventType,
                        ReceivedAt = evt.ReceivedAt ?? DateTime.Now,
                        Status = "Recibido"
                    });

                    RefreshGrid();
                    AppendLog($"📥 Webhook recibido: {evt.Source}/{evt.EventType}");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error parseando evento: {ex.Message}");
            }
        });
    }

    private void OnWebhookProcessed(object data)
    {
        if (_isPaused) return;

        Invoke(() =>
        {
            try
            {
                var json = JsonSerializer.Serialize(data);
                var evt = JsonSerializer.Deserialize<WebhookNotification>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (evt != null)
                {
                    var existing = _events.FirstOrDefault(e => e.EventId == evt.EventId);
                    if (existing != null)
                    {
                        existing.Status = "Procesado ✓";
                        RefreshGrid();
                    }
                    AppendLog($"✅ Webhook procesado: {evt.EventId}");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error: {ex.Message}");
            }
        });
    }

    private void OnWebhookFailed(object data)
    {
        if (_isPaused) return;

        Invoke(() =>
        {
            try
            {
                var json = JsonSerializer.Serialize(data);
                var evt = JsonSerializer.Deserialize<WebhookNotification>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (evt != null)
                {
                    var existing = _events.FirstOrDefault(e => e.EventId == evt.EventId);
                    if (existing != null)
                    {
                        existing.Status = $"Error: {evt.Error}";
                        RefreshGrid();
                    }
                    AppendLog($"❌ Webhook falló: {evt.EventId} - {evt.Error}");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error: {ex.Message}");
            }
        });
    }

    private bool ShouldShowEvent(string? source)
    {
        if (cmbFilter.SelectedIndex == 0) return true;
        return source?.ToLower() == cmbFilter.SelectedItem?.ToString()?.ToLower();
    }

    private void CmbFilter_SelectedIndexChanged(object? sender, EventArgs e)
    {
        RefreshGrid();
    }

    private void RefreshGrid()
    {
        var filtered = cmbFilter.SelectedIndex == 0
            ? _events
            : _events.Where(e => e.Source?.ToLower() == cmbFilter.SelectedItem?.ToString()?.ToLower()).ToList();

        _eventsBindingSource.DataSource = null;
        _eventsBindingSource.DataSource = filtered;
        dgvEvents.Refresh();
    }

    private void UpdateStatus(string status, Color color)
    {
        lblStatus.Text = $"● {status}";
        lblStatus.ForeColor = color;
    }

    private void UpdateButtonStates(bool connected)
    {
        btnConnect.Enabled = !connected;
        btnDisconnect.Enabled = connected;
        btnPause.Enabled = connected;
        txtServerUrl.Enabled = !connected;

        if (!connected)
        {
            btnPause.Text = "⏸ Pausar";
            btnPause.BackColor = Color.FromArgb(200, 150, 0);
        }
    }

    private void AppendLog(string message)
    {
        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
        txtLog.ScrollToCaret();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _hubConnection?.StopAsync().Wait(1000);
        _hubConnection?.DisposeAsync();
        StopCloudflaredProcess();
        base.OnFormClosing(e);
    }

    private void MainForm_Load(object sender, EventArgs e)
    {

    }

    // --- Cloudflare Tunnel Methods ---
    private async void BtnStartTunnel_Click(object? sender, EventArgs e)
    {
        if (_isInstallingCloudflared) return;

        var exePath = Path.Combine(AppContext.BaseDirectory, "cloudflared.exe");

        // Determinar si debemos instalar
        if (!File.Exists(exePath))
        {
            var systemPath = Environment.GetEnvironmentVariable("PATH");
            var paths = systemPath?.Split(';') ?? Array.Empty<string>();
            bool existsInPath = paths.Any(p => File.Exists(Path.Combine(p, "cloudflared.exe")));

            if (!existsInPath)
            {
                await DownloadCloudflaredAsync(exePath);
                if (!File.Exists(exePath)) return; // Falló la descarga
            }
            else
            {
                exePath = "cloudflared"; // Asumimos que correrá porque está en path
            }
        }

        StartCloudflaredProcess(exePath);
    }

    private void BtnStopTunnel_Click(object? sender, EventArgs e)
    {
        StopCloudflaredProcess();
    }

    private async Task DownloadCloudflaredAsync(string targetPath)
    {
        try
        {
            _isInstallingCloudflared = true;
            btnStartTunnel.Enabled = false;
            UpdateTunnelStatus("Descargando cloudflared...", Color.Orange);
            AppendLog("Descargando Cloudflare Tunnel desde GitHub (60MB approx)...");

            using var httpClient = new HttpClient();
            var url = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe";
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream);

            AppendLog("cloudflared descargado con éxito.");
        }
        catch (Exception ex)
        {
            UpdateTunnelStatus("Error Descarga", Color.Red);
            AppendLog($"Error al descargar cloudflared: {ex.Message}");
            MessageBox.Show($"No se pudo descargar cloudflared: {ex.Message}");
        }
        finally
        {
            _isInstallingCloudflared = false;
            btnStartTunnel.Enabled = true;
        }
    }

    private void StartCloudflaredProcess(string exePath)
    {
        try
        {
            StopCloudflaredProcess();

            _cloudflaredProcess = new Process();
            _cloudflaredProcess.StartInfo.FileName = exePath;
            // --url apunta al API local por defecto de Webhooks (puerto 5000)
            _cloudflaredProcess.StartInfo.Arguments = "tunnel --url http://localhost:5000";
            _cloudflaredProcess.StartInfo.UseShellExecute = false;
            _cloudflaredProcess.StartInfo.RedirectStandardError = true;
            _cloudflaredProcess.StartInfo.CreateNoWindow = true;

            _cloudflaredProcess.ErrorDataReceived += (sender, args) =>
            {
                if (string.IsNullOrEmpty(args.Data)) return;

                // Capturar el regex de la URL de cloudflare trycloudflare.com
                var match = Regex.Match(args.Data, @"https://[a-zA-Z0-9-]+\.trycloudflare\.com");
                if (match.Success)
                {
                    Invoke(() =>
                    {
                        txtTunnelUrl.Text = match.Value;
                        UpdateTunnelStatus("Túnel Activo", Color.LimeGreen);
                        AppendLog($"Túnel iniciado en URL pública: {match.Value}");
                    });
                }
            };

            _cloudflaredProcess.Start();
            _cloudflaredProcess.BeginErrorReadLine();

            _tunnelMonitorTimer.Start();

            btnStartTunnel.Enabled = false;
            btnStopTunnel.Enabled = true;
            UpdateTunnelStatus("Iniciando...", Color.Yellow);
            txtTunnelUrl.Text = "Esperando URL...";
            AppendLog("Proceso cloudflared arrancado. Esperando handshake...");
        }
        catch (Exception ex)
        {
            UpdateTunnelStatus("Error", Color.Red);
            AppendLog($"No se pudo iniciar túnel: {ex.Message}");
            MessageBox.Show($"Error ejecutando cloudflared: {ex.Message}", "Error");
            StopCloudflaredProcess();
        }
    }

    private void StopCloudflaredProcess()
    {
        if (_cloudflaredProcess != null && !_cloudflaredProcess.HasExited)
        {
            try
            {
                _cloudflaredProcess.Kill();
            }
            catch { }
        }

        _cloudflaredProcess?.Dispose();
        _cloudflaredProcess = null;

        _tunnelMonitorTimer.Stop();

        if (IsHandleCreated && !Disposing && !IsDisposed)
        {
            Invoke(() =>
            {
                UpdateTunnelStatus("Apagado", Color.Gray);
                btnStartTunnel.Enabled = true;
                btnStopTunnel.Enabled = false;
                txtTunnelUrl.Text = "Esperando URL...";
                AppendLog("Túnel local detenido.");
            });
        }
    }

    private void TunnelMonitorTimer_Tick(object? sender, EventArgs e)
    {
        if (_cloudflaredProcess != null && _cloudflaredProcess.HasExited)
        {
            _tunnelMonitorTimer.Stop();
            UpdateTunnelStatus("Caído", Color.Red);
            AppendLog("¡El proceso de cloudflared ha terminado inesperadamente!");

            btnStartTunnel.Enabled = true;
            btnStopTunnel.Enabled = false;
        }
    }

    private void UpdateTunnelStatus(string status, Color color)
    {
        lblTunnelStatus.Text = $"● {status}";
        lblTunnelStatus.ForeColor = color;
    }
}

// Clases auxiliares para deserialización
public class WebhookNotification
{
    public Guid EventId { get; set; }
    public string? Source { get; set; }
    public string? EventType { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Status { get; set; }
    public string? Error { get; set; }
}

public class WebhookEventItem
{
    public Guid EventId { get; set; }
    public string? Source { get; set; }
    public string? EventType { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string? Status { get; set; }
}
