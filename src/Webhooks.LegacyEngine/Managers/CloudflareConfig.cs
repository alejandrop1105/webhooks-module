namespace Webhooks.LegacyEngine.Managers;

/// <summary>
/// Configuración necesaria para provisionar y ejecutar un Named Tunnel
/// de Cloudflare vía API REST. Estos valores se configuran desde la UI
/// del ERP antes del primer uso.
/// </summary>
public class CloudflareConfig
{
    /// <summary>
    /// Nombre del túnel/subdominio. Ej: "erp-cliente1"
    /// Se usará como nombre del túnel en Cloudflare y como prefijo del hostname.
    /// </summary>
    public string TunnelName { get; set; }

    /// <summary>
    /// Dominio base gestionado en Cloudflare. Ej: "dotarsoluciones.com"
    /// El hostname final será: {TunnelName}.{Domain}
    /// </summary>
    public string Domain { get; set; }

    /// <summary>
    /// API Token de Cloudflare con permisos Tunnel:Edit + DNS:Edit.
    /// Se obtiene desde el Dashboard de Cloudflare (una sola vez).
    /// </summary>
    public string ApiToken { get; set; }

    /// <summary>
    /// Account ID de Cloudflare. Visible en el Dashboard de la cuenta.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Zone ID de la zona DNS del dominio en Cloudflare.
    /// Visible en el Dashboard del dominio (Overview → API → Zone ID).
    /// </summary>
    public string ZoneId { get; set; }

    /// <summary>
    /// Dominio limpio sin protocolo ni barras. Ej: "dotarsoluciones.com"
    /// </summary>
    public string CleanDomain
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Domain)) return string.Empty;
            string clean = Domain.Trim();
            // Eliminar protocolos (https://, http://)
            if (clean.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(8);
            else if (clean.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(7);
            // Eliminar barras finales
            return clean.TrimEnd('/');
        }
    }

    /// <summary>
    /// Hostname completo calculado. Ej: "erp-cliente1.dotarsoluciones.com"
    /// </summary>
    public string Hostname => $"{TunnelName}.{CleanDomain}";

    /// <summary>
    /// Valida que todos los campos obligatorios estén completos.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(TunnelName) &&
        !string.IsNullOrWhiteSpace(Domain) &&
        !string.IsNullOrWhiteSpace(ApiToken) &&
        !string.IsNullOrWhiteSpace(AccountId) &&
        !string.IsNullOrWhiteSpace(ZoneId);
}
