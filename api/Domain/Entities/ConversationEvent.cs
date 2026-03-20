namespace Domain.Entities;

/// <summary>
/// Represents an atomic event extracted from a conversation (e.g., "broker_asked_question").
/// </summary>
public class ConversationEvent
{
    public string ConversationId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty; // ISO-8601
}
