using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Application.Interfaces;
using Application.DTOs;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services;


/// <summary>
/// Result returned by ProcessMessageAsync.
/// </summary>
public record ProcessResult(
    ConversationState UpdatedState);

/// <summary>
/// Orchestrates message processing using pure helper services.
/// Injectable (non-static) because it depends on the repository.
/// </summary>
public class ConversationStateService(
    IConversationStateRepository repository,
    IRealStateRepository realStateRepository,
    IPersistenceEventPublisher persistencePublisher,
    Microsoft.Extensions.Configuration.IConfiguration config)
{
    private readonly bool _debug = config["LOG_DEBUG"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
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
        var state = await repository.GetByIdAsync(msg.ConversationId, ct);
        
        if (state is null)
        {
            var broker = await realStateRepository.GetBrokerDataAsync(msg.BrokerId); // This returns List<BrokerData>, I need the assignment or broker entity
            // Wait, I should check how to get the RealStateBroker entity which has the Mode.
            // Actually, looking at RealStateService, AssignBrokerAsync returns RealStateBroker.
            // I need a way to get the RealStateBroker by brokerId.
            
            state = new ConversationState
            {
                ConversationId = msg.ConversationId,
                BrokerId       = msg.BrokerId,
                CustomerId     = msg.CustomerId,
            };

            var brokerAssignment = await realStateRepository.GetAssignmentsByBrokerIdAsync(msg.BrokerId, ct);
            if (brokerAssignment != null)
            {
                state.Mode = brokerAssignment.Mode;
            }
        }

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

        state.BufferJson  = JsonSerializer.Serialize(buffer);
        state.BufferChars = bufferChars;

        // ── 5. Persist internally (for worker memory/cache) ───────────────────
        await repository.UpsertAsync(state, ct);

        // ── 6. Publish for async storage ─────────────────────────────────────
        await persistencePublisher.PublishMessageAsync(msg, ct);

        // Notify if LLM check is needed immediately
        return new ProcessResult(state);
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
        // Map LLM keys to canonical FactKeys (case-insensitive)
        var canonicalKeys = Domain.Constants.FactKeys.All.ToDictionary(k => k.ToLowerInvariant(), k => k);

        var factUpdates = new List<FactUpdate>();
        if (llmResponse.Facts != null)
        {
            foreach (var kvp in llmResponse.Facts)
            {
                var valStr = kvp.Value.ValueKind == System.Text.Json.JsonValueKind.String 
                    ? kvp.Value.GetString() 
                    : kvp.Value.GetRawText();
                
                // Try to find the exact key, or normalize (e.g., 'property_type' -> 'Property Type')
                var normalizedInputKey = kvp.Key.Replace("_", " ").ToLowerInvariant();
                var actualKey = canonicalKeys.TryGetValue(normalizedInputKey, out var cKey) ? cKey : kvp.Key;

                // Safeguard: Skip facts that indicate "unknown" or "not specified"
                var lowerVal = valStr?.ToLowerInvariant() ?? "";
                if (lowerVal.Contains("não especificado") || 
                    lowerVal.Contains("não informado") || 
                    lowerVal.Contains("not specified") ||
                    lowerVal == "null" ||
                    string.IsNullOrWhiteSpace(valStr))
                {
                    continue;
                }

                factUpdates.Add(new FactUpdate(actualKey, valStr ?? "", 1.0));
            }
        }

        if (_debug)
        {
            Console.WriteLine($"[DEBUG] LLM identified {factUpdates.Count} facts.");
            foreach (var f in factUpdates)
            {
                Console.WriteLine($"[DEBUG] Fact: {f.Name} = {f.Value} (Conf: {f.Confidence})");
            }
        }

        var filtered = factUpdates.Where(f => f.Confidence >= 0.5).ToList();
        
        if (_debug) Console.WriteLine($"[DEBUG] {filtered.Count} facts passed confidence threshold (0.5).");

        var existing = await repository.GetFactsAsync(conversationId, ct);
        var merged   = FactMerger.Merge(existing, filtered, conversationId);
        
        if (_debug)
        {
            Console.WriteLine($"[DEBUG] Merged result: {merged.Count} facts total.");
            Console.WriteLine($"[LOUD] ApplyLlmResultAsync: Merged {merged.Count} facts. Saving to DB...");
        }
        await repository.UpsertFactsAsync(conversationId, merged, ct);

        // ── 2. Publish persistence events (Queues) ───────────────────────────
        if (_debug) Console.WriteLine($"[LOUD] ApplyLlmResultAsync: Publishing to NATS persistence...");
        await persistencePublisher.PublishSummaryAsync(conversationId, llmResponse.Summary, state.LastMessageHash, ct);
        await persistencePublisher.PublishFactsAsync(conversationId, merged, ct);

        // ── 3. Handle Events ────────────────────────────────────────────────
        if (llmResponse.Events != null && llmResponse.Events.Count > 0)
        {
            var eventTimestamp = DateTimeOffset.UtcNow.ToString("O");
            var convEvents = llmResponse.Events.Select(e => new ConversationEvent
            {
                ConversationId = conversationId,
                Type = e.Type,
                Actor = e.Actor,
                Description = e.Description,
                Timestamp = eventTimestamp
            }).ToList();

            if (_debug) Console.WriteLine($"[LOUD] ApplyLlmResultAsync: Processing {convEvents.Count} events for {conversationId}");
            await repository.UpsertEventsAsync(conversationId, convEvents, ct);
            
            foreach (var @event in convEvents)
            {
                await persistencePublisher.PublishEventAsync(@event, ct);
            }
        }

        if (_debug) Console.WriteLine($"[LOUD] ApplyLlmResultAsync: COMPLETED for {conversationId}");
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

    /// <summary>Returns full conversation state.</summary>
    public async Task<ConversationState?> GetStateAsync(
        string conversationId, CancellationToken ct = default)
        => await repository.GetByIdAsync(conversationId, ct);

    /// <summary>
    /// Forces an analysis of the current buffer, regardless of normal trigger rules.
    /// Clears the buffer and returns the context for LLM analysis.
    /// </summary>
    public async Task<string?> TriggerAnalysisAsync(string conversationId, CancellationToken ct = default)
    {
        var state = await repository.GetByIdAsync(conversationId, ct);
        if (state == null) return null;

        var buffer = JsonSerializer.Deserialize<List<string>>(state.BufferJson) ?? [];
        if (buffer.Count == 0) return null;

        var facts = await repository.GetFactsAsync(conversationId, ct);
        var llmContext = LlmContextBuilder.Build(state.RollingSummary, facts, buffer, string.Empty);
        
        if (_debug) Console.WriteLine($"[LOUD] TriggerAnalysisAsync: Built context ({llmContext.Length} chars) for {conversationId}");

        // Clear buffer
        state.BufferJson = "[]";
        state.BufferChars = 0;
        await repository.UpsertAsync(state, ct);

        return llmContext;
    }

    /// <summary>Returns all messages for a conversation.</summary>
    public async Task<IReadOnlyList<NormalizedMessage>> GetMessagesAsync(
        string conversationId, CancellationToken ct = default)
        => await repository.GetMessagesAsync(conversationId, ct);

    /// <summary>Returns all events for a conversation.</summary>
    public async Task<IReadOnlyList<ConversationEvent>> GetEventsAsync(
        string conversationId, CancellationToken ct = default)
        => await repository.GetEventsAsync(conversationId, ct);
}
