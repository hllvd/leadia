using Application.Services;
using Domain.Entities;

namespace Unit;

public class LlmContextBuilderTests
{
    private static ConversationFact Fact(string name, string value) =>
        new() { FactName = name, Value = value, Confidence = 0.9, ConversationId = "test" };

    [Fact]
    public void Build_IncludesSummarySection()
    {
        var result = LlmContextBuilder.Build(
            rollingSummary:  "User wants an apartment.",
            facts:           [],
            buffer:          [],
            incomingMessage: "Hello",
            currentDateTime: DateTimeOffset.UtcNow);

        Assert.Contains("SUMMARY", result);
        Assert.Contains("User wants an apartment.", result);
    }

    [Fact]
    public void Build_IncludesFactsSection()
    {
        var facts = new[] { Fact("budget", "600000"), Fact("location", "downtown") };

        var result = LlmContextBuilder.Build("", facts, [], "msg", DateTimeOffset.UtcNow);

        Assert.Contains("FACTS", result);
        Assert.Contains("budget: 600000", result);
        Assert.Contains("location: downtown", result);
    }

    [Fact]
    public void Build_IncludesRecentMessagesSection()
    {
        var buffer = new[] { "Does it have parking?", "And a pool?" };

        var result = LlmContextBuilder.Build("", [], buffer, "new msg", DateTimeOffset.UtcNow);

        Assert.Contains("RECENT MESSAGES", result);
        Assert.Contains("Does it have parking?", result);
        Assert.Contains("And a pool?", result);
    }

    [Fact]
    public void Build_IncludesNewMessageSection()
    {
        var result = LlmContextBuilder.Build("", [], [], "Can you send photos?", DateTimeOffset.UtcNow);

        Assert.Contains("NEW MESSAGE", result);
        Assert.Contains("Can you send photos?", result);
    }

    [Fact]
    public void Build_WithEmptyState_ProducesMinimalValidContext()
    {
        var result = LlmContextBuilder.Build("", [], [], "Hi!", DateTimeOffset.UtcNow);

        Assert.Contains("SUMMARY", result);
        Assert.Contains("FACTS", result);
        Assert.Contains("RECENT MESSAGES", result);
        Assert.Contains("NEW MESSAGE", result);
        Assert.Contains("Hi!", result);
    }

    [Fact]
    public void Build_SectionsAppearInCorrectOrder()
    {
        var result = LlmContextBuilder.Build("summary", [Fact("k", "v")], ["buffer"], "incoming", DateTimeOffset.UtcNow);

        var summaryIdx  = result.IndexOf("SUMMARY",        StringComparison.Ordinal);
        var factsIdx    = result.IndexOf("FACTS",          StringComparison.Ordinal);
        var recentIdx   = result.IndexOf("RECENT MESSAGES",StringComparison.Ordinal);
        var newMsgIdx   = result.IndexOf("NEW MESSAGE",    StringComparison.Ordinal);

        Assert.True(summaryIdx < factsIdx);
        Assert.True(factsIdx   < recentIdx);
        Assert.True(recentIdx  < newMsgIdx);
    }
    [Fact]
    public void Build_IncludesCurrentDateTimeSection()
    {
        var fixedDate = new DateTimeOffset(2026, 3, 25, 16, 0, 0, TimeSpan.FromHours(-3));
        var result = LlmContextBuilder.Build("", [], [], "msg", fixedDate);

        Assert.Contains("CURRENT DATETIME", result);
        Assert.Contains("2026-03-25T16:00:00-03:00", result);
    }
}
