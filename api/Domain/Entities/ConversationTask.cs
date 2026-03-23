using System.Text.Json;
using System.Text.Json.Serialization;

namespace Domain.Entities;

/// <summary>
/// Represents an actionable task derived from conversation signals.
/// Serves as the source of truth for conversational state tracking.
/// </summary>
public class ConversationTask
{
    public string Id { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of the task. Valid values: "question", "visit", "documents", "followup"
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Status of the task. Valid values: "open", "pending", "completed", "cancelled"
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Owner of the task. Valid values: "broker", "customer"
    /// </summary>
    public string Owner { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// JSON serialized representation of metadata (e.g., proposed_date, proposed_time).
    /// </summary>
    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Helper to access metadata programmatically.
    /// </summary>
    [JsonIgnore]
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public Dictionary<string, string> Metadata
    {
        get => JsonSerializer.Deserialize<Dictionary<string, string>>(MetadataJson) ?? [];
        set => MetadataJson = JsonSerializer.Serialize(value);
    }
}
