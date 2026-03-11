using System.Security.Cryptography;
using System.Text;
using Application.Services;

namespace Unit;

public class WebhookSignatureValidatorTests
{
    private const string Secret = "test-secret-key";

    private static string BuildSignature(string body, string secret)
    {
        var key   = Encoding.UTF8.GetBytes(secret);
        var data  = Encoding.UTF8.GetBytes(body);
        var hash  = HMACSHA256.HashData(key, data);
        return "sha256=" + Convert.ToHexStringLower(hash);
    }

    [Fact]
    public void Validate_WithCorrectSignature_ReturnsTrue()
    {
        var body      = "{'from':'123','message':'hello'}";
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var sig       = BuildSignature(body, Secret);

        Assert.True(WebhookSignatureValidator.Validate(bodyBytes, sig, Secret));
    }

    [Fact]
    public void Validate_WithWrongSecret_ReturnsFalse()
    {
        var body      = "hello";
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var sig       = BuildSignature(body, Secret);

        Assert.False(WebhookSignatureValidator.Validate(bodyBytes, sig, "wrong-secret"));
    }

    [Fact]
    public void Validate_WithTamperedBody_ReturnsFalse()
    {
        var sig           = BuildSignature("original-body", Secret);
        var tamperedBytes = Encoding.UTF8.GetBytes("tampered-body");

        Assert.False(WebhookSignatureValidator.Validate(tamperedBytes, sig, Secret));
    }

    [Fact]
    public void Validate_WithNullSignature_ReturnsFalse()
    {
        var bodyBytes = Encoding.UTF8.GetBytes("body");
        Assert.False(WebhookSignatureValidator.Validate(bodyBytes, null, Secret));
    }

    [Fact]
    public void Validate_WithEmptySignature_ReturnsFalse()
    {
        var bodyBytes = Encoding.UTF8.GetBytes("body");
        Assert.False(WebhookSignatureValidator.Validate(bodyBytes, string.Empty, Secret));
    }

    [Fact]
    public void Validate_WithMissingPrefix_ReturnsFalse()
    {
        var body      = "hello";
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var key       = Encoding.UTF8.GetBytes(Secret);
        var hashHex   = Convert.ToHexStringLower(HMACSHA256.HashData(key, bodyBytes));

        // No "sha256=" prefix
        Assert.False(WebhookSignatureValidator.Validate(bodyBytes, hashHex, Secret));
    }

    [Fact]
    public void Validate_WithEmptySecret_ReturnsFalse()
    {
        var bodyBytes = Encoding.UTF8.GetBytes("body");
        Assert.False(WebhookSignatureValidator.Validate(bodyBytes, "sha256=abc", string.Empty));
    }
}
