using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Application.Services;

/// <summary>
/// Pure static functions for normalizing WhatsApp messages.
/// All methods are stateless and deterministic — ideal for unit testing.
/// </summary>
public static partial class MessageNormalizer
{
    /// <summary>
    /// Builds a deterministic conversation ID from the broker and customer phone numbers.
    /// <example>BuildConversationId("4798913312", "47839948") => "4798913312-47839948"</example>
    /// </summary>
    public static string BuildConversationId(string brokerNumber, string customerNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(brokerNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerNumber);
        return $"{brokerNumber.Trim()}-{customerNumber.Trim()}";
    }

    /// <summary>
    /// Extracts the conversation ID from a NATS subject (e.g., "persist.message.123-456").
    /// Returns the last part of the subject.
    /// </summary>
    public static string ExtractConversationId(string subject)
    {
        if (string.IsNullOrEmpty(subject)) return string.Empty;
        var lastDotIndex = subject.LastIndexOf('.');
        return lastDotIndex != -1 ? subject[(lastDotIndex + 1)..] : subject;
    }

    /// <summary>
    /// Normalizes message text:
    ///   1. Trim leading/trailing whitespace
    ///   2. Collapse multiple consecutive spaces into one
    ///   3. Normalize newlines to \n
    /// The input is required to be valid UTF-8 (guaranteed by .NET strings).
    /// </summary>
    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var trimmed      = text.Trim();
        var singleSpaces = CollapseSpacesRegex().Replace(trimmed, " ");
        var normalized   = NormalizeNewlinesRegex().Replace(singleSpaces, "\n");
        return normalized;
    }

    /// <summary>
    /// Computes the deduplication hash for a message.
    /// Formula: SHA-256(timestamp + brokerId + customerId + text)
    /// Returns a lowercase hex string.
    /// </summary>
    public static string ComputeHash(string timestamp, string brokerId, string customerId, string text)
    {
        var raw   = $"{timestamp}{brokerId}{customerId}{text}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexStringLower(bytes);
    }

    [GeneratedRegex(@" {2,}")]
    private static partial Regex CollapseSpacesRegex();

    [GeneratedRegex(@"\r\n|\r")]
    private static partial Regex NormalizeNewlinesRegex();
}
