using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
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

                    // Payload PUT (Solo actualizamos la URL de entrega)
                    string fullDeliveryUrl = $"{newTunnelUrl.TrimEnd('/')}/api/webhooks/woocommerce";
                    string jsonPayload = $"{{\"delivery_url\": \"{fullDeliveryUrl}\"}}";
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    string baseUrl = $"{_storeUrl}/wp-json/wc/v3/webhooks/{_webhookId}";
                    string apiUrl = string.Empty;

                    bool isHttps = _storeUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

                    if (isHttps)
                    {
                        // Para HTTPS: Pasamos las llaves por QueryString (Infalible contra bloqueos de Apache)
                        apiUrl = $"{baseUrl}?consumer_key={_consumerKey}&consumer_secret={_consumerSecret}";

                        // También enviamos por Cabecera nativa HTTP por compatibilidad máxima
                        var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_consumerKey}:{_consumerSecret}"));
                        hc.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);
                    }
                    else
                    {
                        // Para HTTP: WooCommerce prohíbe Basic Auth estrictamente por seguridad.
                        // TRUCO: Hay que emular y firmar la petición con OAuth 1.0a HMAC-SHA256 manualmente.
                        Serilog.Log.Information("[WooCommerce API] Conexión HTTP detectada. Generando firma OAuth 1.0a sobre la marcha...");

                        string nonce = Guid.NewGuid().ToString("N");
                        string timestamp = ((long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString();

                        var parameters = new SortedDictionary<string, string>
                        {
                            { "oauth_consumer_key", _consumerKey },
                            { "oauth_nonce", nonce },
                            { "oauth_signature_method", "HMAC-SHA256" },
                            { "oauth_timestamp", timestamp }
                        };

                        // 1. Unimos los parámetros codificados
                        string parameterString = string.Join("&", parameters.Select(
                            p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

                        // 2. Creamos el Base String (Verbo HTTP + URL + Parámetros)
                        string baseString = $"PUT&{Uri.EscapeDataString(baseUrl)}&{Uri.EscapeDataString(parameterString)}";

                        // 3. Clave Secreta = ConsumerSecret + "&"
                        string secretKey = $"{Uri.EscapeDataString(_consumerSecret)}&";

                        // 4. Firmar con HMAC-SHA256 y Base64
                        using (var hasher = new HMACSHA256(Encoding.ASCII.GetBytes(secretKey)))
                        {
                            var hash = hasher.ComputeHash(Encoding.ASCII.GetBytes(baseString));
                            string signature = Convert.ToBase64String(hash);

                            // 5. Ensamblar la URL final mágica
                            apiUrl = $"{baseUrl}?{parameterString}&oauth_signature={Uri.EscapeDataString(signature)}";
                        }
                    }

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
