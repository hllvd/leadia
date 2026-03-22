using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Interface for storing message batches in S3.
/// </summary>
public interface IMessageStorage
{
    /// <summary>
    /// Stores a batch of messages for a conversation as a numbered part.
    /// File naming: {conversationId}+part-{part}.json
    /// </summary>
    Task StoreMessagesAsync(string conversationId, int part, IEnumerable<NormalizedMessage> messages, CancellationToken ct = default);
}
