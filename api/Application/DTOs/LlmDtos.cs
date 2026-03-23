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
    [property: JsonPropertyName("signals")] LlmSignals? Signals,
    [property: JsonPropertyName("context")] LlmContext? Context);

public record LlmSignals(
    [property: JsonPropertyName("has_unanswered_question")] bool HasUnansweredQuestion = false,
    [property: JsonPropertyName("has_new_question")] bool HasNewQuestion = false,
    [property: JsonPropertyName("needs_followup")] bool NeedsFollowup = false,
    [property: JsonPropertyName("has_pending_visit")] bool HasPendingVisit = false,
    [property: JsonPropertyName("visit_suggested")] bool VisitSuggested = false,
    [property: JsonPropertyName("visit_confirmed")] bool VisitConfirmed = false,
    [property: JsonPropertyName("has_pending_documents")] bool HasPendingDocuments = false,
    [property: JsonPropertyName("customer_engaged")] bool CustomerEngaged = false,
    [property: JsonPropertyName("customer_unresponsive")] bool CustomerUnresponsive = false);

public record LlmContext(
    [property: JsonPropertyName("last_action")] LlmLastAction? LastAction,
    [property: JsonPropertyName("visit")] LlmVisitContext? Visit,
    [property: JsonPropertyName("documents")] LlmDocumentsContext? Documents);

public record LlmLastAction(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("actor")] string? Actor,
    [property: JsonPropertyName("description")] string? Description);

public record LlmVisitContext(
    [property: JsonPropertyName("proposed_date")] string? ProposedDate,
    [property: JsonPropertyName("proposed_time")] string? ProposedTime);

public record LlmDocumentsContext(
    [property: JsonPropertyName("requested")] bool Requested = false,
    [property: JsonPropertyName("description")] string? Description = null);
