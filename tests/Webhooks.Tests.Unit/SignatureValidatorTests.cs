using Webhooks.Core.Services;

namespace Webhooks.Tests.Unit;

/// <summary>
/// Tests para el validador de firmas HMAC.
/// </summary>
public class SignatureValidatorTests
{
    private readonly SignatureValidator _validator = new();

    [Fact]
    public void Validate_WithValidSignature_ReturnsTrue()
    {
        // Arrange
        var payload = "{\"test\": true}";
        var secretKey = "mi_clave_secreta";

        // Generar firma válida
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(secretKey);
        var payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);
        var hash = System.Security.Cryptography.HMACSHA256.HashData(keyBytes, payloadBytes);
        var validSignature = Convert.ToBase64String(hash);

        // Act
        var result = _validator.Validate(payload, validSignature, secretKey, "HMAC-SHA256");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Validate_WithInvalidSignature_ReturnsFalse()
    {
        // Arrange
        var payload = "{\"test\": true}";
        var secretKey = "mi_clave_secreta";
        var invalidSignature = "firma_invalida";

        // Act
        var result = _validator.Validate(payload, invalidSignature, secretKey, "HMAC-SHA256");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Validate_WithNullPayload_ReturnsFalse()
    {
        // Act
        var result = _validator.Validate(null!, "signature", "secret", "HMAC-SHA256");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Validate_WithNullSignature_ReturnsFalse()
    {
        // Act
        var result = _validator.Validate("payload", null!, "secret", "HMAC-SHA256");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Validate_WithPrefixedSignature_ReturnsTrue()
    {
        // Arrange - algunos servicios envían prefijos como "sha256="
        var payload = "{\"test\": true}";
        var secretKey = "mi_clave_secreta";

        var keyBytes = System.Text.Encoding.UTF8.GetBytes(secretKey);
        var payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);
        var hash = System.Security.Cryptography.HMACSHA256.HashData(keyBytes, payloadBytes);
        var prefixedSignature = "sha256=" + Convert.ToBase64String(hash);

        // Act
        var result = _validator.Validate(payload, prefixedSignature, secretKey, "HMAC-SHA256");

        // Assert
        Assert.True(result);
    }
}
