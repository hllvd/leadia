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

        var isExpired         = string.IsNullOrEmpty(state.LastActivityTimestamp)
                                ? false
                                : BufferPolicy.IsBufferExpired(DateTimeOffset.Parse(state.LastActivityTimestamp));

        var shouldTrigger     = BufferPolicy.ShouldTriggerSummary(buffer.Count, bufferChars, secondsSinceLast)
                             || (isExpired && buffer.Count > 0);

        // ── 4. Update state fields ────────────────────────────────────────────
        state.LastMessageHash       = msg.MessageHash;
        state.LastMessageTimestamp  = msg.Timestamp;
        state.LastActivityTimestamp = DateTimeOffset.UtcNow.ToString("O");

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

        // Overwrite rolling summary in memory (for subsequent logic)
        state.RollingSummary = llmResponse.Summary;
        // Direct DB update removed - handled via NATS persistence.

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
            Console.WriteLine($"[LOUD] ApplyLlmResultAsync: Merged {merged.Count} facts. Publishing to NATS...");
        }

        // ── 2. Publish persistence events (Queues) ───────────────────────────
        if (_debug) Console.WriteLine($"[LOUD] ApplyLlmResultAsync: Publishing to NATS persistence...");
        await persistencePublisher.PublishSummaryAsync(conversationId, llmResponse.Summary, state.LastMessageHash, ct);
        await persistencePublisher.PublishFactsAsync(conversationId, merged, ct);

        // ── 3. Handle Tasks & Signals ───────────────────────────────────────────────
        if (llmResponse.Signals != null)
        {
            if (_debug) Console.WriteLine($"[LOUD] ApplyLlmResultAsync: Processing Tasks/Signals for {conversationId}");
            await repository.UpsertSignalsAsync(conversationId, llmResponse.Signals, ct);
            await SyncTasksAsync(conversationId, llmResponse.Signals, llmResponse.Context, ct);
        }

        if (_debug) Console.WriteLine($"[LOUD] ApplyLlmResultAsync: COMPLETED for {conversationId}");
    }

    private async Task SyncTasksAsync(string conversationId, LlmSignals signals, LlmContext? context, CancellationToken ct)
    {
        var existingTasks = await repository.GetTasksAsync(conversationId, ct);
        var tasksDict = existingTasks.ToDictionary(t => t.Type, t => t);

        var now = DateTimeOffset.UtcNow;
        var defaultOwner = context?.LastAction?.Actor?.ToLowerInvariant() == "broker" ? "customer" : "broker";

        ConversationTask GetOrCreateTask(string type)
        {
            if (!tasksDict.TryGetValue(type, out var t))
            {
                t = new ConversationTask
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ConversationId = conversationId,
                    Type = type,
                    CreatedAt = now,
                    UpdatedAt = now
                };
            }
            return t;
        }

        bool UpsertIfNeeded(ConversationTask task, string newStatus, string? newOwner, string? desc, Dictionary<string, string>? metadata)
        {
            newOwner ??= defaultOwner;
            bool changed = task.Status != newStatus || task.Owner != newOwner || task.Description != desc;
            
            if (metadata != null)
            {
                var newMetaJson = JsonSerializer.Serialize(metadata);
                if (task.MetadataJson != newMetaJson)
                {
                    task.MetadataJson = newMetaJson;
                    changed = true;
                }
            }

            if (changed)
            {
                task.Status = newStatus;
                task.Owner = newOwner;
                if (desc != null) task.Description = desc;
                task.UpdatedAt = now;
                return true;
            }
            return false;
        }

        var toUpsert = new List<ConversationTask>();

        // 1. Question Task
        var qTask = GetOrCreateTask("question");
        string qStatus = qTask.Status;
        if (signals.HasNewQuestion) qStatus = "open";
        else if (!signals.HasUnansweredQuestion) qStatus = "completed";
        // If it was open and has_unanswered_question is true, it stays open.
        
        var qMeta = context?.LastAction?.Type == "question" && !string.IsNullOrEmpty(context.LastAction.Description)
            ? new Dictionary<string, string> { { "user_question", context.LastAction.Description } }
            : null;

        if (UpsertIfNeeded(qTask, qStatus, defaultOwner, "Answer customer's questions", qMeta))
            toUpsert.Add(qTask);

        // 2. Visit Task
        var vTask = GetOrCreateTask("visit");
        string vStatus = vTask.Status;
        if (signals.VisitSuggested || signals.HasPendingVisit) vStatus = "open";
        if (signals.VisitConfirmed) vStatus = "completed";

        var vMeta = context?.Visit != null ? new Dictionary<string, string>
        {
            { "proposed_date", context.Visit.ProposedDate ?? "" },
            { "proposed_time", context.Visit.ProposedTime ?? "" }
        } : null;

        if (UpsertIfNeeded(vTask, vStatus, defaultOwner, "Process property visit request", vMeta))
            toUpsert.Add(vTask);

        // 3. Documents Task
        var dTask = GetOrCreateTask("documents");
        string dStatus = dTask.Status;
        var docRequested = context?.Documents?.Requested == true;
        if (signals.HasPendingDocuments || docRequested) dStatus = "open";
        else if (!signals.HasPendingDocuments) dStatus = "completed";

        var dMeta = context?.Documents != null ? new Dictionary<string, string>
        {
            { "description", context.Documents.Description ?? "" }
        } : null;

        if (UpsertIfNeeded(dTask, dStatus, defaultOwner, "Handle pending documents", dMeta))
            toUpsert.Add(dTask);

        // 4. Call / Meeting Task
        var cTask = GetOrCreateTask("call");
        string cStatus = cTask.Status;
        if (signals.CallSuggested || signals.HasPendingCall) cStatus = "open";
        if (signals.CallConfirmed) cStatus = "completed";

        var cMeta = context?.Call != null ? new Dictionary<string, string>
        {
            { "proposed_date", context.Call.ProposedDate ?? "" },
            { "proposed_time", context.Call.ProposedTime ?? "" },
            { "type", context.Call.Type ?? "" }
        } : null;

        if (UpsertIfNeeded(cTask, cStatus, defaultOwner, "Schedule a call or meeting", cMeta))
            toUpsert.Add(cTask);

        // 5. Follow-up Task
        // pending if the LLM signals it, or if any other signal indicates the conversation is unresolved.
        // This ensures the follow-up task is reliably set without relying solely on LLM phrase detection.
        var fTask = GetOrCreateTask("followup");
        string fStatus = fTask.Status;
        bool needsFollowup = signals.NeedsFollowup
                          || signals.HasUnansweredQuestion
                          || signals.HasPendingVisit
                          || signals.HasPendingCall
                          || signals.HasPendingDocuments;

        if (needsFollowup) fStatus = "pending";
        else fStatus = "completed";

        if (UpsertIfNeeded(fTask, fStatus, "broker", "Follow up with customer", null))
            toUpsert.Add(fTask);

        foreach (var task in toUpsert)
        {
            if (_debug) Console.WriteLine($"[DEBUG] Upserting Task: {task.Type} | Status: {task.Status} | Owner: {task.Owner}");
            await repository.UpsertTaskAsync(task, ct);

            if (task.Status == "open" || task.Status == "pending")
            {
                await persistencePublisher.PublishNotificationAsync(conversationId, "task_state", new 
                {
                    task_id = task.Id,
                    task_type = task.Type,
                    status = task.Status,
                    owner = task.Owner,
                    description = task.Description
                }, ct);
            }
        }

        if (signals.CustomerUnresponsive)
        {
            await persistencePublisher.PublishNotificationAsync(conversationId, "unresponsive", new { }, ct);
        }
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
        var llmContext = LlmContextBuilder.Build(state.RollingSummary, facts, buffer, string.Empty, DateTimeOffset.UtcNow);
        
        if (_debug) Console.WriteLine($"[LOUD] TriggerAnalysisAsync: Built context ({llmContext.Length} chars) for {conversationId}");

        // Clear buffer
        state.BufferJson = "[]";
        state.BufferChars = 0;
        await repository.UpsertAsync(state, ct);

        return llmContext;
    }

    /// <summary>Returns all tasks for a conversation.</summary>
    public async Task<IReadOnlyList<ConversationTask>> GetTasksAsync(
        string conversationId, CancellationToken ct = default)
        => await repository.GetTasksAsync(conversationId, ct);

    /// <summary>Returns the latest signals for a conversation.</summary>
    public async Task<LlmSignals?> GetSignalsAsync(
        string conversationId, CancellationToken ct = default)
        => await repository.GetSignalsAsync(conversationId, ct);

    /// <summary>Returns all messages for a conversation.</summary>
    public async Task<IReadOnlyList<NormalizedMessage>> GetMessagesAsync(
        string conversationId, CancellationToken ct = default)
        => await repository.GetMessagesAsync(conversationId, ct);

}
