using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Webhooks.LegacyEngine.Engine;
using Webhooks.LegacyEngine.Managers;

namespace LegacyErpClient;

public partial class Form1 : Form
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern Int32 SendMessage(IntPtr hWnd, int msg, int wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);
    private const int EM_SETCUEBANNER = 0x1501;

    private WebhookHost _engine;

    // Botones principales
    private Button btnStartServer;
    private Button btnStartTunnel;
    private Button btnStopAll;
    private Button btnReset;
    private Label lblStatus;
    private TextBox txtUrl;

    // Configuración Cloudflare
    private TextBox txtTunnelName;
    private TextBox txtDomain;
    private TextBox txtApiToken;
    private TextBox txtAccountId;
    private TextBox txtZoneId;

    private RichTextBox rtbLog;

    private readonly string _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cloudflare.cfg");

    public Form1()
    {
        InitializeComponent();
        LoadConfig();

        // 1. Instanciamos el Motor
        _engine = new WebhookHost(port: 5000);

        // 2. Eventos de Webhook
        _engine.OnWebhookReceived += Engine_OnWebhookReceived;

        // 3. Eventos de Salud
        _engine.OnServerStatusChanged += (s, e) => UiLog("API", e);
        _engine.OnTunnelStatusChanged += (s, e) => UiLog("PROXY", e);
        _engine.OnSystemLog += (s, e) => UiLog(e.Level, e.Message);

        // 4. URL pública generada
        _engine.OnTunnelUrlGenerated += (s, url) =>
        {
            Invoke(new Action(() => txtUrl.Text = url));
        };
    }

    private void Engine_OnWebhookReceived(object sender, WebhookPayloadEventArgs e)
    {
        UiLog("WEBHOOK", $"RECIBIDO de [{e.Source}]\n{e.RawJson}");
    }

    private void btnStartServer_Click(object sender, EventArgs e)
    {
        UiLog("UI", "Arrancando servidor HTTP local...");
        _engine.Start(enableTunnel: false);
    }

    private void btnStartTunnel_Click(object sender, EventArgs e)
    {
        // Aplicar configuración de Cloudflare desde la UI
        var config = new CloudflareConfig
        {
            TunnelName = txtTunnelName.Text.Trim(),
            Domain = txtDomain.Text.Trim(),
            ApiToken = txtApiToken.Text.Trim(),
            AccountId = txtAccountId.Text.Trim(),
            ZoneId = txtZoneId.Text.Trim()
        };

        if (!config.IsValid)
        {
            UiLog("UI", "⚠️ Completá todos los campos de Cloudflare antes de iniciar el túnel.");
            MessageBox.Show(
                "Completá todos los campos de configuración de Cloudflare:\n" +
                "• Nombre del subdominio\n• Dominio\n• API Token\n• Account ID\n• Zone ID",
                "Configuración incompleta",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _engine.SetCloudflareConfig(config);
        SaveConfig();

        UiLog("UI", $"Arrancando motor + tunnel ({config.Hostname})...");
        _engine.Start(enableTunnel: true);

        SetCloudflareFieldsEnabled(false);
    }

    private void btnStopAll_Click(object sender, EventArgs e)
    {
        UiLog("UI", "Deteniendo motor y túnel...");
        _engine.Stop();
        txtUrl.Text = "Esperando Webhook...";
        SetCloudflareFieldsEnabled(true);
    }

    private void btnReset_Click(object sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "¿Estás seguro? Esto eliminará:\n" +
            "• tunnel_config.json (configuración local del túnel)\n" +
            "• cloudflared.exe (se re-descargará la próxima vez)\n\n" +
            "El túnel en Cloudflare NO se elimina y se reutilizará.",
            "Reset de Túnel",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result == DialogResult.Yes)
        {
            _engine.ResetTunnel();
            txtUrl.Text = "Esperando Webhook...";
            SetCloudflareFieldsEnabled(true);
            UiLog("UI", "🔄 Reset completado. Puede reiniciar el túnel cuando desee.");
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveConfig();
        _engine?.Dispose();
        base.OnFormClosing(e);
    }

    private void SetCloudflareFieldsEnabled(bool enabled)
    {
        txtTunnelName.Enabled = enabled;
        txtDomain.Enabled = enabled;
        txtApiToken.Enabled = enabled;
        txtAccountId.Enabled = enabled;
        txtZoneId.Enabled = enabled;
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
                    case "TunnelName": txtTunnelName.Text = kvp[1]; break;
                    case "Domain": txtDomain.Text = kvp[1]; break;
                    case "ApiToken": txtApiToken.Text = kvp[1]; break;
                    case "AccountId": txtAccountId.Text = kvp[1]; break;
                    case "ZoneId": txtZoneId.Text = kvp[1]; break;
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
                $"TunnelName={txtTunnelName.Text}",
                $"Domain={txtDomain.Text}",
                $"ApiToken={txtApiToken.Text}",
                $"AccountId={txtAccountId.Text}",
                $"ZoneId={txtZoneId.Text}"
            };
            File.WriteAllLines(_configFilePath, lines);
        }
        catch (Exception ex)
        {
            UiLog("SYS", $"Error guardando config: {ex.Message}");
        }
    }

    // --- WinForms Layout ---
    private void InitializeComponent()
    {
        this.Text = "ERP Client 4.8 — Webhooks Named Tunnel";
        this.Size = new Size(900, 650);
        this.BackColor = Color.FromArgb(30, 30, 30);
        this.ForeColor = Color.White;
        this.MinimumSize = new Size(900, 650);

        int y = 15;

        // === FILA 1: Botones principales ===
        btnStartServer = new Button { Text = "⚡ API Local", Location = new Point(20, y), Size = new Size(120, 35), BackColor = Color.SeaGreen, FlatStyle = FlatStyle.Flat };
        btnStartServer.Click += btnStartServer_Click;

        btnStartTunnel = new Button { Text = "☁️ API + Tunnel", Location = new Point(150, y), Size = new Size(160, 35), BackColor = Color.Orange, ForeColor = Color.Black, FlatStyle = FlatStyle.Flat };
        btnStartTunnel.Click += btnStartTunnel_Click;

        btnStopAll = new Button { Text = "⬛ Detener Todo", Location = new Point(320, y), Size = new Size(130, 35), BackColor = Color.Brown, FlatStyle = FlatStyle.Flat };
        btnStopAll.Click += btnStopAll_Click;

        btnReset = new Button { Text = "🔄 Reset Tunnel", Location = new Point(460, y), Size = new Size(130, 35), BackColor = Color.FromArgb(80, 40, 40), FlatStyle = FlatStyle.Flat };
        btnReset.Click += btnReset_Click;

        y += 50;

        // === FILA 2: URL pública ===
        lblStatus = new Label { Text = "URL Pública:", Location = new Point(20, y + 3), AutoSize = true };
        txtUrl = new TextBox
        {
            Text = "Esperando...",
            Location = new Point(105, y),
            Width = 485,
            ReadOnly = true,
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.Yellow
        };

        y += 40;

        // === SECCIÓN: Configuración Cloudflare ===
        var lblCfTitle = new Label { Text = "─── Cloudflare Tunnel ───", Location = new Point(20, y), AutoSize = true, ForeColor = Color.DodgerBlue, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        y += 22;

        var lblTunnelName = new Label { Text = "Subdominio:", Location = new Point(20, y + 3), AutoSize = true, ForeColor = Color.LightGray };
        txtTunnelName = new TextBox { Location = new Point(105, y), Width = 180, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
        SendMessage(txtTunnelName.Handle, EM_SETCUEBANNER, 0, "erp-cliente1");

        var lblDomain = new Label { Text = "Dominio:", Location = new Point(300, y + 3), AutoSize = true, ForeColor = Color.LightGray };
        txtDomain = new TextBox { Location = new Point(365, y), Width = 220, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
        SendMessage(txtDomain.Handle, EM_SETCUEBANNER, 0, "dotarsoluciones.com");

        y += 28;

        var lblApiToken = new Label { Text = "API Token:", Location = new Point(20, y + 3), AutoSize = true, ForeColor = Color.LightGray };
        txtApiToken = new TextBox { Location = new Point(105, y), Width = 480, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White, PasswordChar = '•' };
        SendMessage(txtApiToken.Handle, EM_SETCUEBANNER, 0, "Token de Cloudflare (Tunnel:Edit + DNS:Edit)");

        y += 28;

        var lblAccountId = new Label { Text = "Account ID:", Location = new Point(20, y + 3), AutoSize = true, ForeColor = Color.LightGray };
        txtAccountId = new TextBox { Location = new Point(105, y), Width = 220, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
        SendMessage(txtAccountId.Handle, EM_SETCUEBANNER, 0, "ID de cuenta Cloudflare");

        var lblZoneId = new Label { Text = "Zone ID:", Location = new Point(340, y + 3), AutoSize = true, ForeColor = Color.LightGray };
        txtZoneId = new TextBox { Location = new Point(400, y), Width = 185, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
        SendMessage(txtZoneId.Handle, EM_SETCUEBANNER, 0, "ID de zona DNS");

        y += 35;

        // === LOG ===
        rtbLog = new RichTextBox
        {
            Location = new Point(20, y),
            Width = 845,
            Height = 650 - y - 45,
            BackColor = Color.Black,
            ForeColor = Color.LightGreen,
            ReadOnly = true,
            Font = new Font("Consolas", 10),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        this.Controls.AddRange(new Control[] {
            btnStartServer, btnStartTunnel, btnStopAll, btnReset,
            lblStatus, txtUrl,
            lblCfTitle, lblTunnelName, txtTunnelName, lblDomain, txtDomain,
            lblApiToken, txtApiToken,
            lblAccountId, txtAccountId, lblZoneId, txtZoneId,
            rtbLog
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
