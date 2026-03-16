using System.Text.Json;
using Application.Interfaces;
using Application.DTOs;
using Domain.Entities;

namespace Application.Services;

/// <summary>
/// Normalized inbound message — output of the normalization pipeline.
/// Passed from the webhook endpoint to ConversationStateService.
/// </summary>
public record NormalizedMessage(
    string ConversationId,
    string BrokerId,
    string CustomerId,
    string SenderType,       // "broker" | "customer"
    string Text,
    DateTimeOffset Timestamp,
    string MessageHash);

/// <summary>
/// Result returned by ProcessMessageAsync.
/// </summary>
public record ProcessResult(
    ConversationState UpdatedState,
    bool SummaryTriggered,
    bool FactsUpdated,
    string? LlmContext);

/// <summary>
/// Orchestrates message processing using pure helper services.
/// Injectable (non-static) because it depends on the repository.
/// </summary>
public class ConversationStateService(
    IConversationStateRepository repository,
    IPersistenceEventPublisher persistencePublisher)
{
    /// <summary>
    /// Processes a normalized inbound message:
    ///   1. Load or initialise ConversationState
    ///   2. Deduplicate via message hash
    ///   3. Append to buffer
    ///   4. Check buffer trigger thresholds
    ///   5. If triggered: build LLM context (caller is responsible for the actual LLM call)
    ///   6. Persist updated state
    /// Returns null if the message is a duplicate.
    /// </summary>
    public async Task<ProcessResult?> ProcessMessageAsync(
        NormalizedMessage msg,
        CancellationToken ct = default)
    {
        // ── 1. Load or initialise state ─────────────────────────────────────
        var state = await repository.GetByIdAsync(msg.ConversationId, ct)
                    ?? new ConversationState
                    {
                        ConversationId = msg.ConversationId,
                        BrokerId       = msg.BrokerId,
                        CustomerId     = msg.CustomerId,
                    };

        // ── 2. Deduplication ─────────────────────────────────────────────────
        if (state.LastMessageHash == msg.MessageHash)
            return null;    // caller should return HTTP 409

        // ── 3. Update buffer ─────────────────────────────────────────────────
        var buffer = JsonSerializer.Deserialize<List<string>>(state.BufferJson) ?? [];
        buffer.Add(msg.Text);

        var bufferChars       = state.BufferChars + msg.Text.Length;
        var secondsSinceLast  = state.LastMessageTimestamp == DateTimeOffset.MinValue 
                                ? 0 
                                : (msg.Timestamp - state.LastMessageTimestamp).TotalSeconds;

        var isExpired         = state.LastActivityTimestamp == DateTimeOffset.MinValue
                                ? false
                                : BufferPolicy.IsBufferExpired(state.LastActivityTimestamp);

        var shouldTrigger     = BufferPolicy.ShouldTriggerSummary(buffer.Count, bufferChars, secondsSinceLast)
                             || (isExpired && buffer.Count > 0);

        // ── 4. Update state fields ────────────────────────────────────────────
        state.LastMessageHash       = msg.MessageHash;
        state.LastMessageTimestamp  = msg.Timestamp;
        state.LastActivityTimestamp = DateTimeOffset.UtcNow;

        string? llmContext = null;
        bool summaryTriggered = false;

        if (shouldTrigger)
        {
            var facts = await repository.GetFactsAsync(msg.ConversationId, ct);
            llmContext     = LlmContextBuilder.Build(state.RollingSummary, facts, buffer, msg.Text);
            summaryTriggered = true;

            // Clear buffer after triggering
            buffer      = [];
            bufferChars = 0;
        }

        state.BufferJson  = JsonSerializer.Serialize(buffer);
        state.BufferChars = bufferChars;

        // ── 5. Persist internally (for worker memory/cache) ───────────────────
        await repository.UpsertAsync(state, ct);

        // ── 6. Publish for async storage ─────────────────────────────────────
        await persistencePublisher.PublishMessageAsync(msg, ct);

        // Notify if LLM check is needed immediately
        return new ProcessResult(state, summaryTriggered, false, llmContext);
    }

    /// <summary>
    /// Applies LLM fact and summary results back to the conversation state.
    /// Call this after receiving the LLM response.
    /// </summary>
    public async Task ApplyLlmResultAsync(
        string       conversationId,
        LlmResponse  llmResponse,
        CancellationToken ct = default)
    {
        var state = await repository.GetByIdAsync(conversationId, ct);
        if (state is null) return;

        // Overwrite rolling summary
        state.RollingSummary = llmResponse.Summary;
        await repository.UpsertAsync(state, ct);

        // Convert LlmResponse.Facts to FactUpdates + Filter by confidence >= 0.5
        var factUpdates = llmResponse.Facts
            .Select(kvp => new FactUpdate(kvp.Key, kvp.Value.Value?.ToString() ?? string.Empty, kvp.Value.Confidence))
            .Where(f => f.Confidence >= 0.5) // Graceful threshold from LLM.md §9
            .ToList();

        var existing = await repository.GetFactsAsync(conversationId, ct);
        var merged   = FactMerger.Merge(existing, factUpdates, conversationId);
        await repository.UpsertFactsAsync(conversationId, merged, ct);

        // ── 2. Publish persistence events ────────────────────────────────────
        await persistencePublisher.PublishSummaryAsync(conversationId, llmResponse.Summary, state.LastMessageHash, ct);
        await persistencePublisher.PublishFactsAsync(conversationId, merged, ct);
    }

    /// <summary>Returns current facts for a conversation.</summary>
    public async Task<IReadOnlyList<ConversationFact>> GetFactsAsync(
        string conversationId, CancellationToken ct = default)
        => await repository.GetFactsAsync(conversationId, ct);

    /// <summary>Returns current rolling summary for a conversation.</summary>
    public async Task<string> GetSummaryAsync(
        string conversationId, CancellationToken ct = default)
    {
        var state = await repository.GetByIdAsync(conversationId, ct);
        return state?.RollingSummary ?? string.Empty;
    }
}
