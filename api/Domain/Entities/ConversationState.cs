using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Persisted conversation state — rolling summary, buffer, and metadata.
/// The buffer is stored as a JSON-serialised list of strings in a single column.
/// </summary>
public class ConversationState
{
    public string ConversationId { get; set; } = string.Empty;  // PK: "<broker>-<customer>"
    public string BrokerId       { get; set; } = string.Empty;
    public string CustomerId     { get; set; } = string.Empty;
    public ConversationMode Mode { get; set; } = ConversationMode.OnlyListening;

    public string RollingSummary        { get; set; } = string.Empty;
    public string BufferJson            { get; set; } = "[]";   // JSON array of strings
    public int    BufferChars           { get; set; }
    public string LastMessageHash       { get; set; } = string.Empty;
    public DateTimeOffset LastMessageTimestamp  { get; set; } = DateTimeOffset.MinValue;
    public DateTimeOffset LastActivityTimestamp { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt     { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public ICollection<ConversationFact> Facts { get; set; } = [];
}
