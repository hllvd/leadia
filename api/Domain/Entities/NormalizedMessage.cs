using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Normalized inbound message — output of the normalization pipeline.
/// Passed from the webhook endpoint to ConversationStateService.
/// </summary>
public record NormalizedMessage(
    string ConversationId,
    string BrokerId,
    string CustomerId,
    SenderType SenderType,
    string Text,
    DateTimeOffset Timestamp,
    string MessageHash);
