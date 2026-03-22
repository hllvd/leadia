using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Repository interface for ConversationState persistence.
/// </summary>
public interface IConversationStateRepository
{
    /// <summary>Gets conversation state by ID, or null if it doesn't exist.</summary>
    Task<ConversationState?> GetByIdAsync(string conversationId, CancellationToken ct = default);

    /// <summary>Inserts or updates a ConversationState record.</summary>
    Task UpsertAsync(ConversationState state, CancellationToken ct = default);

    /// <summary>Gets all facts for a conversation.</summary>
    Task<IReadOnlyList<ConversationFact>> GetFactsAsync(string conversationId, CancellationToken ct = default);

    /// <summary>Replaces all facts for a conversation with the provided list.</summary>
    Task UpsertFactsAsync(string conversationId, IEnumerable<ConversationFact> facts, CancellationToken ct = default);

    /// <summary>Gets all messages for a conversation.</summary>
    Task<IReadOnlyList<NormalizedMessage>> GetMessagesAsync(string conversationId, CancellationToken ct = default);

    /// <summary>Gets all events for a conversation, sorted by timestamp.</summary>
    Task<IReadOnlyList<ConversationEvent>> GetEventsAsync(string conversationId, CancellationToken ct = default);

    /// <summary>Gets the latest N events for a conversation.</summary>
    Task<IReadOnlyList<ConversationEvent>> GetLatestEventsAsync(string conversationId, int limit, CancellationToken ct = default);

    /// <summary>Gets a paged timeline of events and messages.</summary>
    Task<PagedTimelineResult> GetTimelineAsync(string conversationId, int limit = 50, string? exclusiveStartKey = null, bool forward = true, CancellationToken ct = default);

    /// <summary>Appends events to a conversation (append-only).</summary>
    Task UpsertEventsAsync(string conversationId, IEnumerable<ConversationEvent> events, CancellationToken ct = default);
}

public record PagedTimelineResult(
    IReadOnlyList<TimelineItem> Items,
    string? LastEvaluatedKey);

public record TimelineItem(
    string Type, // "message" or "{event_type}"
    object Data, // NormalizedMessage or ConversationEvent
    DateTimeOffset Timestamp);
