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
}
