using Application.Interfaces;
using Application.Services;
using Domain.Entities;

namespace Infrastructure.Services;

/// <summary>
/// No-op implementation of message and persistence publishers.
/// Used for local prototype — discards all events silently.
/// </summary>
public class NoOpPublisher : IMessagePublisher, IPersistenceEventPublisher
{
    public Task PublishAsync(NormalizedMessage message, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task PublishMessageAsync(NormalizedMessage message, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task PublishSummaryAsync(string conversationId, string summary, string lastHash, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task PublishFactsAsync(string conversationId, IEnumerable<ConversationFact> facts, CancellationToken ct = default)
        => Task.CompletedTask;
}
