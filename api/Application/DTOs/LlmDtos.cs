using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.DTOs;

/// <summary>
/// The structured response from the LLM during conversation analysis.
/// Matches the schema in LLM.md §6.
/// </summary>
public record LlmResponse(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("facts")] Dictionary<string, JsonElement>? Facts,
    [property: JsonPropertyName("events")] List<LlmEvent>? Events);

public record LlmEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("actor")] string Actor,
    [property: JsonPropertyName("description")] string Description);
