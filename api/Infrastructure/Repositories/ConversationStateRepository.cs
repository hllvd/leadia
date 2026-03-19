using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
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

    public Task<IReadOnlyList<NormalizedMessage>> GetMessagesAsync(string conversationId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<NormalizedMessage>>([]);
}
