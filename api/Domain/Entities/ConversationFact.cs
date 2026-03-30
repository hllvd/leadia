namespace Domain.Entities;

/// <summary>
/// A single structured fact extracted from a conversation by the LLM.
/// </summary>
public class ConversationFact
{
    public int    Id             { get; set; }
    public string ConversationId { get; set; } = string.Empty;
    public string FactName       { get; set; } = string.Empty;
    public string Value          { get; set; } = string.Empty;   // stored as string; parse at read time
    public double Confidence     { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
