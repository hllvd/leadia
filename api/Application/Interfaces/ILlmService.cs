using Application.DTOs;

namespace Application.Interfaces;

/// <summary>
/// Abstraction for LLM-based conversation analysis (Fact extraction + Summarization).
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Analyzes the provided context (SUMMARY + FACTS + RECENT + NEW MESSAGE)
    /// and returns the structured update.
    /// Returns null if the LLM call fails or returns invalid JSON.
    /// </summary>
    Task<LlmResponse?> AnalyzeAsync(string context, CancellationToken ct = default);

    /// <summary>
    /// Generates a free-text conversational reply from the broker bot.
    /// Returns null on failure.
    /// </summary>
    Task<string?> ChatAsync(string systemPrompt, string userMessage, CancellationToken ct = default);
}
