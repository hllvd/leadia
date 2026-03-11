using System.Text.Json.Serialization;

namespace Application.DTOs;

/// <summary>
/// A fact update proposed by the LLM.
/// </summary>
public record LlmFactUpdate(
    [property: JsonPropertyName("value")] object? Value,
    [property: JsonPropertyName("confidence")] double Confidence);

/// <summary>
/// The structured response from the LLM during conversation analysis.
/// Matches the schema in LLM.md §6.
/// </summary>
public record LlmResponse(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("facts")] Dictionary<string, LlmFactUpdate> Facts);
