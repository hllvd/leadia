using Application.Interfaces;
using Domain.Entities;

namespace Infrastructure.Repositories;

/// <summary>
/// In-memory implementation of IConversationStateRepository.
/// Used for local prototype — no DynamoDB or Redis required.
/// Registered as singleton so state survives across requests.
/// </summary>
public class InMemoryConversationStateRepository : IConversationStateRepository
{
    private readonly Dictionary<string, ConversationState> _states = [];
    private readonly Dictionary<string, List<ConversationFact>> _facts = [];

    public Task<ConversationState?> GetByIdAsync(string conversationId, CancellationToken ct = default)
        => Task.FromResult(_states.GetValueOrDefault(conversationId));

    public Task UpsertAsync(ConversationState state, CancellationToken ct = default)
    {
        _states[state.ConversationId] = state;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ConversationFact>> GetFactsAsync(string conversationId, CancellationToken ct = default)
    {
        var facts = _facts.GetValueOrDefault(conversationId) ?? [];
        return Task.FromResult<IReadOnlyList<ConversationFact>>(facts);
    }

    public Task UpsertFactsAsync(string conversationId, IEnumerable<ConversationFact> facts, CancellationToken ct = default)
    {
        _facts[conversationId] = [.. facts];
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<NormalizedMessage>> GetMessagesAsync(string conversationId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<NormalizedMessage>>([]);
}
