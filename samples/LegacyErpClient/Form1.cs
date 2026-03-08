using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LegacyErpClient.Adapters;
using Webhooks.LegacyEngine.Engine;

namespace LegacyErpClient;

public partial class Form1 : Form
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern Int32 SendMessage(IntPtr hWnd, int msg, int wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);
    private const int EM_SETCUEBANNER = 0x1501;

    private WebhookHost _engine;

    // UI Elements
    private Button btnStartServer;
    private Button btnStartTunnel;
    private Button btnStopAll;
    private Label lblStatus;
    private TextBox txtUrl;

    // Configuración Adapters
    private TextBox txtWcUrl;
    private TextBox txtWcKeys;
    private TextBox txtWcWebhookId;
    private TextBox txtMpToken;

    private RichTextBox rtbLog;

    private readonly List<IWebhookRegistrar> _adapters = new List<IWebhookRegistrar>();

    private readonly string _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "adapters.cfg");

    public Form1()
    {
        InitializeComponent();
        LoadConfig();

        // 1. Instanciamos el Motor Inyector y su configuración
        _engine = new WebhookHost(port: 5000);

        // 2. Oído al "Megáfono" interno: Cuando ingresa un Payload
        _engine.OnWebhookReceived += Engine_OnWebhookReceived;

        // Oído al "Megáfono" de Salud (Túneles, Caídas, Reinicios)
        _engine.OnServerStatusChanged += (s, e) => UiLog("API", e);
        _engine.OnTunnelStatusChanged += (s, e) => UiLog("PROXY", e);
        _engine.OnSystemLog += (s, e) => UiLog(e.Level, e.Message);

        _engine.OnTunnelUrlGenerated += async (s, url) =>
        {
            Invoke(new Action(() => txtUrl.Text = url));
            await RunAdaptersAsync(url);
        };
    }

    private async System.Threading.Tasks.Task RunAdaptersAsync(string newUrl)
    {
        _adapters.Clear();

        // 1. Cargar WooCommerce si hay datos
        if (!string.IsNullOrWhiteSpace(txtWcUrl.Text) && txtWcKeys.Text.Contains(":"))
        {
            var parts = txtWcKeys.Text.Split(':');
            _adapters.Add(new WooCommerceRegistrar(txtWcUrl.Text, parts[0], parts[1], txtWcWebhookId.Text));
        }

        // 2. Cargar MercadoPago si hay datos
        if (!string.IsNullOrWhiteSpace(txtMpToken.Text))
        {
            _adapters.Add(new MercadoPagoRegistrar(txtMpToken.Text));
        }

        if (_adapters.Count == 0)
        {
            UiLog("ADAPTERS", "No hay credenciales cargadas. Omitiendo auto-registro.");
            return;
        }

        UiLog("ADAPTERS", $"Empujando nueva URL ({newUrl}) a {_adapters.Count} plataforma(s)...");

        foreach (var adapter in _adapters)
        {
            try
            {
                bool success = await adapter.RegisterUrlAsync(newUrl);
                if (success) UiLog("ADAPTERS", $"✅ {adapter.GetType().Name} actualizado.");
                else UiLog("ADAPTERS", $"❌ Falló {adapter.GetType().Name}.");
            }
            catch (Exception ex)
            {
                UiLog("ADAPTERS", $"❌ Error Crítico en {adapter.GetType().Name}: {ex.Message}");
            }
        }
    }

    private void Engine_OnWebhookReceived(object sender, WebhookPayloadEventArgs e)
    {
        // Llegó como rayo del servidor OWIN a la memoria del formulario
        UiLog("NOTIFICACIÓN", $"RECIBIDO de [{e.Source}]\n{e.RawJson}");
    }

    private void btnStartServer_Click(object sender, EventArgs e)
    {
        UiLog("UI", "Arrancando motor internamente...");
        _engine.Start(enableTunnel: false);
    }

    private void btnStartTunnel_Click(object sender, EventArgs e)
    {
        UiLog("UI", "Arrancando motor + proxy en la nube.");
        _engine.Start(enableTunnel: true);
    }

    private void btnStopAll_Click(object sender, EventArgs e)
    {
        UiLog("UI", "Apagado forzado del motor y sus procesos huérfanos.");
        _engine.Stop();
        txtUrl.Text = "Esperando Webhook...";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveConfig();
        _engine?.Dispose(); // IMPORTANTE: Mata hilos zombis y apaga Cloudflare
        base.OnFormClosing(e);
    }

    private void LoadConfig()
    {
        if (!File.Exists(_configFilePath)) return;

        try
        {
            var lines = File.ReadAllLines(_configFilePath);
            foreach (var line in lines)
            {
                var kvp = line.Split(new[] { '=' }, 2);
                if (kvp.Length != 2) continue;

                switch (kvp[0])
                {
                    case "WcUrl": txtWcUrl.Text = kvp[1]; break;
                    case "WcKeys": txtWcKeys.Text = kvp[1]; break;
                    case "WcId": txtWcWebhookId.Text = kvp[1]; break;
                    case "MpToken": txtMpToken.Text = kvp[1]; break;
                }
            }
        }
        catch (Exception ex)
        {
            UiLog("SYS", $"Error al cargar config: {ex.Message}");
        }
    }

    private void SaveConfig()
    {
        try
        {
            var lines = new List<string>
            {
                $"WcUrl={txtWcUrl.Text}",
                $"WcKeys={txtWcKeys.Text}",
                $"WcId={txtWcWebhookId.Text}",
                $"MpToken={txtMpToken.Text}"
            };
            File.WriteAllLines(_configFilePath, lines);
        }
        catch (Exception ex)
        {
            UiLog("SYS", $"Error guardando config: {ex.Message}");
        }
    }

    // --- WinForms Boilerplate Básico (Creado en RunTime) ---
    private void InitializeComponent()
    {
        this.Text = "ERP Client 4.8 (Mono-proceso sin SignalR)";
        this.Size = new Size(800, 600);
        this.BackColor = Color.FromArgb(30, 30, 30);
        this.ForeColor = Color.White;

        btnStartServer = new Button { Text = "⚡ API Local", Location = new Point(20, 20), Size = new Size(120, 35), BackColor = Color.SeaGreen, FlatStyle = FlatStyle.Flat };
        btnStartServer.Click += btnStartServer_Click;

        btnStartTunnel = new Button { Text = "☁️ API + Tunnel", Location = new Point(160, 20), Size = new Size(180, 35), BackColor = Color.Orange, FlatStyle = FlatStyle.Flat };
        btnStartTunnel.Click += btnStartTunnel_Click;

        btnStopAll = new Button { Text = "⬛ Detener Todo", Location = new Point(360, 20), Size = new Size(120, 35), BackColor = Color.Brown, FlatStyle = FlatStyle.Flat };
        btnStopAll.Click += btnStopAll_Click;

        lblStatus = new Label { Text = "URL Pública:", Location = new Point(20, 75), AutoSize = true };
        txtUrl = new TextBox { Text = "Esperando...", Location = new Point(100, 72), Width = 380, ReadOnly = true, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.Yellow };

        // --- Inserción Panel Configuración ---
        var lblConfig = new Label { Text = "Adapters Config:", Location = new Point(500, 5), AutoSize = true, ForeColor = Color.LightGray };

        txtWcUrl = new TextBox { Location = new Point(500, 25), Width = 260 };
        SendMessage(txtWcUrl.Handle, EM_SETCUEBANNER, 0, "WP Url (https://...)");

        txtWcKeys = new TextBox { Location = new Point(500, 50), Width = 200 };
        SendMessage(txtWcKeys.Handle, EM_SETCUEBANNER, 0, "ck_...:cs_...");

        txtWcWebhookId = new TextBox { Location = new Point(710, 50), Width = 50 };
        SendMessage(txtWcWebhookId.Handle, EM_SETCUEBANNER, 0, "ID");

        txtMpToken = new TextBox { Location = new Point(500, 75), Width = 260 };
        SendMessage(txtMpToken.Handle, EM_SETCUEBANNER, 0, "MercadoPago AccessToken (APP...)");
        // --- Fin Inserción ---

        rtbLog = new RichTextBox
        {
            Location = new Point(20, 110),
            Width = 740,
            Height = 420,
            BackColor = Color.Black,
            ForeColor = Color.LightGreen,
            ReadOnly = true,
            Font = new Font("Consolas", 10),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        this.Controls.AddRange(new Control[] {
            btnStartServer, btnStartTunnel, btnStopAll, lblStatus, txtUrl, rtbLog,
            lblConfig, txtWcUrl, txtWcKeys, txtWcWebhookId, txtMpToken
        });
    }

    private void UiLog(string category, string message)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => UiLog(category, message)));
            return;
        }

        rtbLog.AppendText($"[{DateTime.Now:HH:mm:ss}] [{category}] {message}\n");
        rtbLog.ScrollToCaret();
    }
}
