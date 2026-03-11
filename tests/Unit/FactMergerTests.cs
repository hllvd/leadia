using Application.Services;
using Domain.Entities;

namespace Unit;

public class FactMergerTests
{
    private const string ConvId = "test-conv";

    private static ConversationFact Fact(string name, string value, double confidence) =>
        new() { FactName = name, Value = value, Confidence = confidence, ConversationId = ConvId };

    private static FactUpdate Update(string name, string value, double confidence) =>
        new(name, value, confidence);

    [Fact]
    public void Merge_HigherConfidence_OverwritesExisting()
    {
        var existing = new[] { Fact("budget", "1000", 0.5) };
        var updates  = new[] { Update("budget", "2000", 0.8) };

        var result = FactMerger.Merge(existing, updates, ConvId);

        var budget = Assert.Single(result);
        Assert.Equal("2000", budget.Value);
        Assert.Equal(0.8, budget.Confidence);
    }

    [Fact]
    public void Merge_LowerConfidence_PreservesExisting()
    {
        var existing = new[] { Fact("budget", "1000", 0.9) };
        var updates  = new[] { Update("budget", "500", 0.4) };

        var result = FactMerger.Merge(existing, updates, ConvId);

        var budget = Assert.Single(result);
        Assert.Equal("1000", budget.Value);
        Assert.Equal(0.9, budget.Confidence);
    }

    [Fact]
    public void Merge_EqualConfidence_OverwritesExisting()
    {
        var existing = new[] { Fact("budget", "1000", 0.7) };
        var updates  = new[] { Update("budget", "1000", 0.7) };

        var result = FactMerger.Merge(existing, updates, ConvId);

        var budget = Assert.Single(result);
        Assert.Equal(0.7, budget.Confidence);
    }

    [Fact]
    public void Merge_BrandNewFact_IsAdded()
    {
        var existing = new List<ConversationFact>();
        var updates  = new[] { Update("intent", "buy", 0.95) };

        var result = FactMerger.Merge(existing, updates, ConvId);

        var intent = Assert.Single(result);
        Assert.Equal("intent", intent.FactName);
        Assert.Equal("buy", intent.Value);
        Assert.Equal(0.95, intent.Confidence);
        Assert.Equal(ConvId, intent.ConversationId);
    }

    [Fact]
    public void Merge_MultipleFacts_WorksCorrectly()
    {
        var existing = new[] { Fact("f1", "v1", 0.5), Fact("f2", "v2", 0.5) };
        var updates  = new[] { Update("f1", "new_v1", 0.9), Update("f3", "v3", 0.5) };

        var result = FactMerger.Merge(existing, updates, ConvId);

        Assert.Equal(3, result.Count);
        Assert.Equal("new_v1", result.First(f => f.FactName == "f1").Value);
        Assert.Equal("v2",     result.First(f => f.FactName == "f2").Value);
        Assert.Equal("v3",     result.First(f => f.FactName == "f3").Value);
    }

    [Fact]
    public void Merge_IsCaseInsensitiveForFactNames()
    {
        var existing = new[] { Fact("BUDGET", "1000", 0.5) };
        var updates  = new[] { Update("budget", "2000", 0.8) };

        var result = FactMerger.Merge(existing, updates, ConvId);

        var budget = Assert.Single(result);
        Assert.Equal("2000", budget.Value);
    }
}
