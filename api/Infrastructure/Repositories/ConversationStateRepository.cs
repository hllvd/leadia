using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class ConversationStateRepository(AppDbContext db) : IConversationStateRepository
{
    public async Task<ConversationState?> GetByIdAsync(string conversationId, CancellationToken ct = default)
        => await db.ConversationStates
                   .FirstOrDefaultAsync(s => s.ConversationId == conversationId, ct);

    public async Task UpsertAsync(ConversationState state, CancellationToken ct = default)
    {
        var existing = await db.ConversationStates
                               .FindAsync([state.ConversationId], ct);
        if (existing is null)
            db.ConversationStates.Add(state);
        else
        {
            existing.RollingSummary        = state.RollingSummary;
            existing.BufferJson            = state.BufferJson;
            existing.BufferChars           = state.BufferChars;
            existing.LastMessageHash       = state.LastMessageHash;
            existing.LastMessageTimestamp  = state.LastMessageTimestamp;
            existing.LastActivityTimestamp = state.LastActivityTimestamp;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ConversationFact>> GetFactsAsync(string conversationId, CancellationToken ct = default)
        => await db.ConversationFacts
                   .Where(f => f.ConversationId == conversationId)
                   .ToListAsync(ct);

    public async Task UpsertFactsAsync(string conversationId, IEnumerable<ConversationFact> facts, CancellationToken ct = default)
    {
        var existing = await db.ConversationFacts
                               .Where(f => f.ConversationId == conversationId)
                               .ToListAsync(ct);

        var existingByName = existing.ToDictionary(f => f.FactName, StringComparer.OrdinalIgnoreCase);

        foreach (var fact in facts)
        {
            if (existingByName.TryGetValue(fact.FactName, out var row))
            {
                row.Value      = fact.Value;
                row.Confidence = fact.Confidence;
                row.UpdatedAt  = fact.UpdatedAt;
            }
            else
            {
                db.ConversationFacts.Add(new ConversationFact
                {
                    ConversationId = conversationId,
                    FactName       = fact.FactName,
                    Value          = fact.Value,
                    Confidence     = fact.Confidence,
                    UpdatedAt      = fact.UpdatedAt,
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NormalizedMessage>> GetMessagesAsync(string conversationId, CancellationToken ct = default)
        => await Task.FromResult(new List<NormalizedMessage>());

    public async Task<IReadOnlyList<ConversationTask>> GetTasksAsync(string conversationId, CancellationToken ct = default)
        => await db.ConversationTasks
                   .Where(t => t.ConversationId == conversationId)
                   .ToListAsync(ct);

    public async Task UpsertTaskAsync(ConversationTask task, CancellationToken ct = default)
    {
        var existing = await db.ConversationTasks
                               .FirstOrDefaultAsync(t => t.ConversationId == task.ConversationId && t.Type == task.Type, ct);
        if (existing is null)
            db.ConversationTasks.Add(task);
        else
        {
            existing.Status       = task.Status;
            existing.Owner        = task.Owner;
            existing.Description  = task.Description;
            existing.MetadataJson = task.MetadataJson;
            existing.UpdatedAt    = task.UpdatedAt;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task UpsertSignalsAsync(string conversationId, Application.DTOs.LlmSignals signals, CancellationToken ct = default)
    {
        var state = await db.ConversationStates.FindAsync([conversationId], ct);
        if (state != null)
        {
            state.SignalsJson = JsonSerializer.Serialize(signals);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<Application.DTOs.LlmSignals?> GetSignalsAsync(string conversationId, CancellationToken ct = default)
    {
        var state = await db.ConversationStates.FindAsync([conversationId], ct);
        if (state == null || string.IsNullOrEmpty(state.SignalsJson)) return null;
        return JsonSerializer.Deserialize<Application.DTOs.LlmSignals>(state.SignalsJson);
    }
}

