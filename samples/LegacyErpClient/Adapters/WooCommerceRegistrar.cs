using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LegacyErpClient.Adapters
{
    /// <summary>
    /// Adaptador que se comunica con la API v3 de WooCommerce para empujar la nueva URL del Túnel
    /// </summary>
    public class WooCommerceRegistrar : IWebhookRegistrar
    {
        private readonly string _storeUrl;
        private readonly string _consumerKey;
        private readonly string _consumerSecret;
        private readonly string _webhookId;

        public WooCommerceRegistrar(string storeUrl, string consumerKey, string consumerSecret, string webhookId)
        {
            _storeUrl = storeUrl.TrimEnd('/');
            _consumerKey = consumerKey;
            _consumerSecret = consumerSecret;
            _webhookId = webhookId;
        }

        public async Task<bool> RegisterUrlAsync(string newTunnelUrl)
        {
            if (string.IsNullOrWhiteSpace(_storeUrl) || string.IsNullOrWhiteSpace(_webhookId))
                return false;

            try
            {
                using (var hc = new HttpClient())
                {
                    // Algunos hostings WordPress bloquean si no hay User-Agent o usan TLS viejo
                    System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
                    hc.DefaultRequestHeaders.Add("User-Agent", "LegacyErpClient/1.0");

                    // Basic Auth exigido por WooCommerce API
                    var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_consumerKey}:{_consumerSecret}"));
                    hc.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);

                    // Payload PUT (Solo actualizamos la URL de entrega)
                    string fullDeliveryUrl = $"{newTunnelUrl.TrimEnd('/')}/api/webhooks/woocommerce";
                    string jsonPayload = $"{{\"delivery_url\": \"{fullDeliveryUrl}\"}}";
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    // Llamada a la API de WP.
                    // Muchos Hostings compartidos (Apache/CGI) destruyen la cabecera "Authorization: Basic".
                    // Pasamos las llaves por QueryString como método infalible 100% soportado por WooCommerce.
                    string apiUrl = $"{_storeUrl}/wp-json/wc/v3/webhooks/{_webhookId}?consumer_key={_consumerKey}&consumer_secret={_consumerSecret}";

                    var response = await hc.PutAsync(apiUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                    else
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        Serilog.Log.Error($"[WooCommerce API] Error actualizando webhook {_webhookId}: {response.StatusCode} - {error}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[WooCommerce AI] Excepción fatal al registrar la URL en {_storeUrl}");
                return false;
            }
        }
    }
}
