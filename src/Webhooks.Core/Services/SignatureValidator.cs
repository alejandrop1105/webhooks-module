using System.Security.Cryptography;
using System.Text;
using Webhooks.Core.Interfaces;

namespace Webhooks.Core.Services;

/// <summary>
/// Servicio para validar firmas HMAC de webhooks.
/// </summary>
public class SignatureValidator : ISignatureValidator
{
    /// <inheritdoc />
    public bool Validate(string payload, string signature, string secretKey, string algorithm)
    {
        if (string.IsNullOrEmpty(payload) || string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secretKey))
            return false;

        try
        {
            var computedSignature = ComputeSignature(payload, secretKey, algorithm);

            // Comparación segura contra timing attacks
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedSignature),
                Encoding.UTF8.GetBytes(NormalizeSignature(signature)));
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeSignature(string payload, string secretKey, string algorithm)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        byte[] hashBytes = algorithm.ToUpperInvariant() switch
        {
            "HMAC-SHA256" or "SHA256" => HMACSHA256.HashData(keyBytes, payloadBytes),
            "HMAC-SHA1" or "SHA1" => HMACSHA1.HashData(keyBytes, payloadBytes),
            "HMAC-SHA512" or "SHA512" => HMACSHA512.HashData(keyBytes, payloadBytes),
            _ => HMACSHA256.HashData(keyBytes, payloadBytes)
        };

        return Convert.ToBase64String(hashBytes);
    }

    private static string NormalizeSignature(string signature)
    {
        // Algunos servicios envían prefijos como "sha256=", "sha1=", etc.
        // Solo normalizar si tiene un prefijo de algoritmo conocido
        var knownPrefixes = new[] { "sha256=", "sha1=", "sha512=", "hmac=" };

        foreach (var prefix in knownPrefixes)
        {
            if (signature.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return signature[prefix.Length..];
            }
        }

        return signature;
    }
}
