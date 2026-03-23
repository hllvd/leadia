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
        
    public Task<IReadOnlyList<ConversationTask>> GetTasksAsync(string conversationId, CancellationToken ct = default)
    {
        var tasks = _tasks.GetValueOrDefault(conversationId) ?? [];
        return Task.FromResult<IReadOnlyList<ConversationTask>>(tasks.Values.ToList());
    }

    public Task UpsertTaskAsync(ConversationTask task, CancellationToken ct = default)
    {
        if (!_tasks.TryGetValue(task.ConversationId, out var dict))
        {
            dict = new Dictionary<string, ConversationTask>();
            _tasks[task.ConversationId] = dict;
        }
        dict[task.Type] = task;
        return Task.CompletedTask;
    }

    public Task<Application.DTOs.LlmSignals?> GetSignalsAsync(string conversationId, CancellationToken ct = default)
        => Task.FromResult(_signals.GetValueOrDefault(conversationId));

    public Task UpsertSignalsAsync(string conversationId, Application.DTOs.LlmSignals signals, CancellationToken ct = default)
    {
        _signals[conversationId] = signals;
        return Task.CompletedTask;
    }

    private readonly Dictionary<string, Dictionary<string, ConversationTask>> _tasks = [];
    private readonly Dictionary<string, Application.DTOs.LlmSignals> _signals = [];
}
