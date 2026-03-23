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

    /// <summary>Gets all tasks for a conversation.</summary>
    Task<IReadOnlyList<ConversationTask>> GetTasksAsync(string conversationId, CancellationToken ct = default);

    /// <summary>Inserts or updates a task for a conversation.</summary>
    Task UpsertTaskAsync(ConversationTask task, CancellationToken ct = default);

    /// <summary>Saves the raw latest signals object for a conversation.</summary>
    Task UpsertSignalsAsync(string conversationId, Application.DTOs.LlmSignals signals, CancellationToken ct = default);

    /// <summary>Gets the latest signals for a conversation (if any).</summary>
    Task<Application.DTOs.LlmSignals?> GetSignalsAsync(string conversationId, CancellationToken ct = default);
}
