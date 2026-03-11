using System.Security.Cryptography;
using System.Text;

namespace Application.Services;

/// <summary>
/// Pure static functions for validating WhatsApp webhook HMAC-SHA256 signatures.
/// </summary>
public static class WebhookSignatureValidator
{
    /// <summary>
    /// Validates that the provided signature matches the HMAC-SHA256 of the raw request body
    /// signed with the given secret.
    ///
    /// Expected signature format: "sha256=&lt;hex&gt;"
    ///
    /// Uses a constant-time comparison to prevent timing attacks.
    /// </summary>
    /// <param name="rawBody">Raw UTF-8 encoded request body bytes.</param>
    /// <param name="signature">Value of the X-Hub-Signature-256 header.</param>
    /// <param name="secret">Webhook HMAC secret from configuration.</param>
    /// <returns>True if the signature is valid; false otherwise.</returns>
    public static bool Validate(ReadOnlySpan<byte> rawBody, string? signature, string secret)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret))
            return false;

        if (!signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            return false;

        var providedHex  = signature["sha256=".Length..];
        var secretBytes  = Encoding.UTF8.GetBytes(secret);
        var computedHash = HMACSHA256.HashData(secretBytes, rawBody);
        var computedHex  = Convert.ToHexStringLower(computedHash);

        // Constant-time comparison
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(providedHex.ToLowerInvariant()));
    }
}
