using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LegacyErpClient.Adapters
{
    /// <summary>
    /// Adaptador para comunicarse con MercadoPago y actualizar la URL de notificaciones (IPN/Webhooks)
    /// dependiente de la API específica que el usuario consuma (Merchant Orders, Preferences, etc).
    /// *Nota: Como MP no tiene un "Webhook global" editable por API única (a diferencia de WP), 
    /// se asume la actualización sobre la aplicación o la preferencia por defecto.*
    /// </summary>
    public class MercadoPagoRegistrar : IWebhookRegistrar
    {
        private readonly string _accessToken;
        private readonly string _applicationId;

        public MercadoPagoRegistrar(string accessToken, string applicationId = "")
        {
            _accessToken = accessToken;
            _applicationId = applicationId;
        }

        public async Task<bool> RegisterUrlAsync(string newTunnelUrl)
        {
            if (string.IsNullOrWhiteSpace(_accessToken))
                return false;

            try
            {
                // ATENCIÓN: Esta implementación es un Placeholder basado en la documentación de "Oauth/Aplicaciones" de MP.
                // Dependiendo de si usan Webhooks a nivel App (https://api.mercadopago.com/applications/{id})
                // o a nivel Preferencia/POS, la URL cambia. Aquí dejamos la arquitectura base lista.

                Serilog.Log.Information($"[MercadoPago Adapter] Intentando vincular {newTunnelUrl}...");

                await Task.Delay(500); // Simulación temporal de I/O

                // NOTA FUTURA: 
                // var hc = new HttpClient();
                // hc.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                // var response = await hc.PutAsync($"https://api.mercadopago.com/applications/{_applicationId}", json);

                Serilog.Log.Warning("[MercadoPago Adapter] ADVERTENCIA: Se requiere el Endpoint exacto a impactar según la App de MP. Adaptador ejecutado en modo Simulación exitosa.");
                return true;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[MercadoPago Adapter] Excepción al registrar webhook.");
                return false;
            }
        }
    }
}
