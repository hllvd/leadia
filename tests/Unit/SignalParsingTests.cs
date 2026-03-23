using System.Text.Json;
using Application.DTOs;

namespace Unit;

public class SignalParsingTests
{
    [Fact]
    public void EmptyJson_DefaultsAllSignalsToFalse()
    {
        var json = "{}";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        
        var response = JsonSerializer.Deserialize<LlmResponse>(json, options);
        var signals = response?.Signals ?? new LlmSignals();

        Assert.False(signals.HasUnansweredQuestion);
        Assert.False(signals.HasNewQuestion);
        Assert.False(signals.NeedsFollowup);
        Assert.False(signals.HasPendingVisit);
        Assert.False(signals.VisitSuggested);
        Assert.False(signals.VisitConfirmed);
        Assert.False(signals.HasPendingDocuments);
        Assert.False(signals.CustomerEngaged);
        Assert.False(signals.CustomerUnresponsive);
    }

    [Fact]
    public void PartialJson_ParsesCorrectly_AndDefaultsMissingToFalse()
    {
        var json = """
        {
            "signals": {
                "has_new_question": true,
                "customer_engaged": true
            }
        }
        """;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        
        var response = JsonSerializer.Deserialize<LlmResponse>(json, options);
        var signals = response?.Signals ?? new LlmSignals();

        Assert.True(signals.HasNewQuestion);
        Assert.True(signals.CustomerEngaged);
        Assert.False(signals.NeedsFollowup);
        Assert.False(signals.VisitConfirmed);
    }

    [Fact]
    public void Context_ParsesSafely()
    {
        var json = """
        {
            "context": {
                "last_action": { "type": "message", "actor": "broker" },
                "visit": { "proposed_date": "amanhã" }
            }
        }
        """;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        
        var response = JsonSerializer.Deserialize<LlmResponse>(json, options);
        var context = response?.Context ?? new LlmContext(null, null, null);

        Assert.NotNull(context.LastAction);
        Assert.Equal("broker", context.LastAction?.Actor);
        
        Assert.NotNull(context.Visit);
        Assert.Equal("amanhã", context.Visit?.ProposedDate);
        Assert.Null(context.Visit?.ProposedTime);
        
        Assert.Null(context.Documents);
    }
}
