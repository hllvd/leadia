using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Publishes normalized inbound messages to the 'messages' stream.
/// </summary>
public interface IMessagePublisher
{
    Task PublishAsync(NormalizedMessage message, CancellationToken ct = default);
}

/// <summary>
/// Publishes persistence events to the 'persistence_events' stream.
/// Used by the Message Worker to signal the Persistence Worker.
/// </summary>
public interface IPersistenceEventPublisher
{
    Task PublishMessageAsync(NormalizedMessage message, CancellationToken ct = default);
    Task PublishSummaryAsync(string conversationId, string summary, string lastHash, CancellationToken ct = default);
    Task PublishFactsAsync(string conversationId, IEnumerable<Domain.Entities.ConversationFact> facts, CancellationToken ct = default);
    Task PublishEventAsync(ConversationEvent @event, CancellationToken ct = default);
}
