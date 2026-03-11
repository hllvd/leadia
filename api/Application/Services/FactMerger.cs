using Domain.Entities;

namespace Application.Services;

/// <summary>
/// Represents a proposed fact update from the LLM response.
/// </summary>
public record FactUpdate(string Name, string Value, double Confidence);

/// <summary>
/// Pure static function that merges LLM-extracted facts into the existing fact set.
/// Merge rule (from API.md §5 / LLM.md):
///   new_fact.confidence >= existing_fact.confidence → overwrite
///   otherwise                                       → keep existing
/// </summary>
public static class FactMerger
{
    /// <summary>
    /// Merges a set of incoming <paramref name="updates"/> into the <paramref name="existing"/> facts.
    /// Returns a new list — does not mutate the inputs.
    /// </summary>
    public static IReadOnlyList<ConversationFact> Merge(
        IEnumerable<ConversationFact> existing,
        IEnumerable<FactUpdate>       updates,
        string                        conversationId)
    {
        // Index existing facts by name for O(1) lookup
        var result = existing
            .ToDictionary(f => f.FactName, f => f, StringComparer.OrdinalIgnoreCase);

        var now = DateTimeOffset.UtcNow;

        foreach (var update in updates)
        {
            if (result.TryGetValue(update.Name, out var existing_fact))
            {
                // Only overwrite when incoming confidence is at least as high
                if (update.Confidence >= existing_fact.Confidence)
                {
                    result[update.Name] = existing_fact with
                    {
                        Value      = update.Value,
                        Confidence = update.Confidence,
                        UpdatedAt  = now
                    };
                }
                // else keep existing unchanged
            }
            else
            {
                // Brand new fact — always add
                result[update.Name] = new ConversationFact
                {
                    ConversationId = conversationId,
                    FactName       = update.Name,
                    Value          = update.Value,
                    Confidence     = update.Confidence,
                    UpdatedAt      = now
                };
            }
        }

        return [.. result.Values];
    }
}
