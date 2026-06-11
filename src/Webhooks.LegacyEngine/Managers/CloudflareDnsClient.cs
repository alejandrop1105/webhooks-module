using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Webhooks.LegacyEngine.Managers;

/// <summary>
/// Cliente HTTP minimalista para consultar y eliminar recursos en Cloudflare:
/// cantidad de DNS records, plan de la zone, eliminación de CNAME, eliminación de cfd_tunnel.
/// No toca file system ni procesos — pensado para que la UI lo instancie, lo use y lo descarte.
/// </summary>
public sealed class CloudflareDnsClient : IDisposable
{
    private const string ApiBase = "https://api.cloudflare.com/client/v4";
    private readonly HttpClient _http;
    private readonly CloudflareConfig _config;

    public CloudflareDnsClient(CloudflareConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiToken);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Consulta DNS records totales + plan de la zone y devuelve la cuota informativa.
    /// </summary>
    public async Task<CuotaInfo> ObtenerCuotaAsync()
    {
        int usados = await ObtenerCantidadDnsAsync();
        (string planId, string planNombre, int maximo) = await ObtenerPlanZonaAsync();
        return new CuotaInfo(usados, maximo, planNombre, planId);
    }

    /// <summary>
    /// Busca el CNAME por hostname exacto y lo elimina. Si hay varios records con el
    /// mismo nombre los elimina todos.
    /// </summary>
    public async Task<bool> EliminarRegistroDnsAsync(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname)) return false;

        var ids = await BuscarRecordIdsAsync(hostname);
        if (ids.Count == 0) return true; // ya no existe → success idempotente

        bool todosOk = true;
        foreach (string id in ids)
        {
            var resp = await _http.DeleteAsync($"{ApiBase}/zones/{_config.ZoneId}/dns_records/{id}");
            if (!resp.IsSuccessStatusCode) todosOk = false;
        }
        return todosOk;
    }

    /// <summary>
    /// Busca el cfd_tunnel por nombre, limpia sus conexiones activas y lo elimina.
    /// </summary>
    public async Task<bool> EliminarTunelAsync(string tunnelName)
    {
        if (string.IsNullOrWhiteSpace(tunnelName)) return false;

        string? tunnelId = await BuscarTunnelIdAsync(tunnelName);
        if (tunnelId == null) return true; // ya no existe → success idempotente

        // Cloudflare exige que el túnel no tenga conexiones activas. Hacemos DELETE
        // sobre /connections para forzar la desconexión antes del DELETE final.
        try
        {
            await _http.DeleteAsync($"{ApiBase}/accounts/{_config.AccountId}/cfd_tunnel/{tunnelId}/connections");
        }
        catch { /* si falla no abortamos: el DELETE del tunnel puede igual funcionar */ }

        var resp = await _http.DeleteAsync($"{ApiBase}/accounts/{_config.AccountId}/cfd_tunnel/{tunnelId}");
        return resp.IsSuccessStatusCode;
    }

    public void Dispose() => _http.Dispose();

    // ────────────────────────────────────────────────────────────────────
    // Helpers privados
    // ────────────────────────────────────────────────────────────────────

    private async Task<int> ObtenerCantidadDnsAsync()
    {
        var resp = await _http.GetAsync($"{ApiBase}/zones/{_config.ZoneId}/dns_records?per_page=1");
        var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
        if (!resp.IsSuccessStatusCode || json["success"]?.Value<bool>() != true) return -1;
        return json["result_info"]?["total_count"]?.Value<int>() ?? -1;
    }

    private async Task<(string id, string nombre, int max)> ObtenerPlanZonaAsync()
    {
        var resp = await _http.GetAsync($"{ApiBase}/zones/{_config.ZoneId}");
        var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
        if (!resp.IsSuccessStatusCode || json["success"]?.Value<bool>() != true)
            return ("desconocido", "?", -1);

        string id = json["result"]?["plan"]?["legacy_id"]?.ToString() ?? "desconocido";
        return id switch
        {
            "free" => ("free", "Free", 200),
            "pro" => ("pro", "Pro", 3500),
            "business" => ("business", "Business", 3500),
            "enterprise" => ("enterprise", "Enterprise", int.MaxValue),
            _ => (id, id, -1),
        };
    }

    private async Task<List<string>> BuscarRecordIdsAsync(string hostname)
    {
        var resp = await _http.GetAsync($"{ApiBase}/zones/{_config.ZoneId}/dns_records?name={Uri.EscapeDataString(hostname)}&type=CNAME");
        var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
        if (!resp.IsSuccessStatusCode || json["success"]?.Value<bool>() != true)
            return new List<string>();

        var arr = json["result"] as JArray;
        if (arr == null) return new List<string>();

        return arr.Select(r => r["id"]?.ToString() ?? string.Empty)
                  .Where(s => !string.IsNullOrEmpty(s))
                  .ToList();
    }

    private async Task<string?> BuscarTunnelIdAsync(string tunnelName)
    {
        var resp = await _http.GetAsync($"{ApiBase}/accounts/{_config.AccountId}/cfd_tunnel?name={Uri.EscapeDataString(tunnelName)}&is_deleted=false");
        var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
        if (!resp.IsSuccessStatusCode || json["success"]?.Value<bool>() != true)
            return null;

        var arr = json["result"] as JArray;
        if (arr == null || arr.Count == 0) return null;
        return arr[0]?["id"]?.ToString();
    }
}

/// <summary>
/// Snapshot de cuota actual de la zone de Cloudflare. <see cref="Maximo"/> es <see cref="int.MaxValue"/>
/// cuando el plan es Enterprise; -1 si no se pudo determinar.
/// </summary>
public sealed class CuotaInfo
{
    public int Usados { get; }
    public int Maximo { get; }
    public string PlanNombre { get; }
    public string PlanLegacyId { get; }

    public CuotaInfo(int usados, int maximo, string planNombre, string planLegacyId)
    {
        Usados = usados;
        Maximo = maximo;
        PlanNombre = planNombre;
        PlanLegacyId = planLegacyId;
    }
}
