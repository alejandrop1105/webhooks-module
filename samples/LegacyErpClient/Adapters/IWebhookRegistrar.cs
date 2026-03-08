using System.Threading.Tasks;

namespace LegacyErpClient.Adapters
{
    /// <summary>
    /// Contrato estándar para cualquier plataforma externa (WooCommerce, MercadoPago, etc)
    /// que necesite ser notificada automáticamente cuando Cloudflare genera un nuevo Túnel.
    /// </summary>
    public interface IWebhookRegistrar
    {
        /// <summary>
        /// Sube la nueva URL generada a la plataforma remota.
        /// </summary>
        /// <param name="newTunnelUrl">Ejemplo: https://pepito.trycloudflare.com/api/webhooks/woocommerce</param>
        /// <returns>Verdadero si la plataforma aceptó la actualización.</returns>
        Task<bool> RegisterUrlAsync(string newTunnelUrl);
    }
}
