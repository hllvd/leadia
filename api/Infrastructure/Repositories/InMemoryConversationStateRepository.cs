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

    public Task<IReadOnlyList<ConversationEvent>> GetEventsAsync(string conversationId, CancellationToken ct = default)
    {
        var evts = _events.GetValueOrDefault(conversationId) ?? [];
        return Task.FromResult<IReadOnlyList<ConversationEvent>>(evts.OrderBy(e => e.Timestamp).ToList());
    }

    public Task<IReadOnlyList<ConversationEvent>> GetLatestEventsAsync(string conversationId, int limit, CancellationToken ct = default)
    {
        var evts = _events.GetValueOrDefault(conversationId) ?? [];
        return Task.FromResult<IReadOnlyList<ConversationEvent>>(evts.OrderByDescending(e => e.Timestamp).Take(limit).ToList());
    }

    public Task<PagedTimelineResult> GetTimelineAsync(string conversationId, int limit = 50, string? exclusiveStartKey = null, bool forward = true, CancellationToken ct = default)
    {
        // Simple in-memory mock timeline (only events for now)
        var evts = _events.GetValueOrDefault(conversationId) ?? [];
        var items = evts.Select(e => new TimelineItem(e.Type, e, DateTimeOffset.Parse(e.Timestamp)))
                        .OrderBy(x => x.Timestamp)
                        .Take(limit)
                        .ToList();
        return Task.FromResult(new PagedTimelineResult(items, null));
    }

    public Task UpsertEventsAsync(string conversationId, IEnumerable<ConversationEvent> events, CancellationToken ct = default)
    {
        if (!_events.TryGetValue(conversationId, out var existing))
        {
            existing = new List<ConversationEvent>();
            _events[conversationId] = existing;
        }
        ((List<ConversationEvent>)existing).AddRange(events);
        return Task.CompletedTask;
    }

    private readonly Dictionary<string, List<ConversationEvent>> _events = [];
}
