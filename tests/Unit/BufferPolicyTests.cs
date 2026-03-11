using Application.Services;

namespace Unit;

public class BufferPolicyTests : IDisposable
{
    // Save and restore defaults so tests don't bleed into each other
    private readonly int    _origTriggerMessages  = BufferPolicy.SummaryTriggerMessages;
    private readonly int    _origTriggerChars     = BufferPolicy.SummaryTriggerChars;
    private readonly double _origTimeoutSeconds   = BufferPolicy.TimeoutSeconds;
    private readonly double _origExpirationSeconds = BufferPolicy.ExpirationSeconds;

    public void Dispose()
    {
        BufferPolicy.SummaryTriggerMessages  = _origTriggerMessages;
        BufferPolicy.SummaryTriggerChars     = _origTriggerChars;
        BufferPolicy.TimeoutSeconds          = _origTimeoutSeconds;
        BufferPolicy.ExpirationSeconds       = _origExpirationSeconds;
    }

    // ── ShouldTriggerSummary ─────────────────────────────────────────────────

    [Fact]
    public void ShouldTriggerSummary_WhenMessageCountThresholdMet()
    {
        BufferPolicy.SummaryTriggerMessages = 5;
        Assert.True(BufferPolicy.ShouldTriggerSummary(bufferCount: 5, bufferChars: 0, secondsSinceLastMessage: 0));
    }

    [Fact]
    public void ShouldTriggerSummary_WhenCharCountThresholdMet()
    {
        BufferPolicy.SummaryTriggerChars = 400;
        Assert.True(BufferPolicy.ShouldTriggerSummary(bufferCount: 1, bufferChars: 400, secondsSinceLastMessage: 0));
    }

    [Fact]
    public void ShouldTriggerSummary_WhenTimeoutMet()
    {
        BufferPolicy.TimeoutSeconds = 30;
        Assert.True(BufferPolicy.ShouldTriggerSummary(bufferCount: 1, bufferChars: 10, secondsSinceLastMessage: 31));
    }

    [Fact]
    public void ShouldNotTriggerSummary_WhenBelowAllThresholds()
    {
        BufferPolicy.SummaryTriggerMessages = 5;
        BufferPolicy.SummaryTriggerChars    = 400;
        BufferPolicy.TimeoutSeconds         = 30;
        Assert.False(BufferPolicy.ShouldTriggerSummary(bufferCount: 2, bufferChars: 50, secondsSinceLastMessage: 5));
    }

    [Fact]
    public void ShouldTriggerSummary_ExactlyAtMessageThreshold()
    {
        BufferPolicy.SummaryTriggerMessages = 3;
        // Count = 3 should trigger (≥)
        Assert.True(BufferPolicy.ShouldTriggerSummary(bufferCount: 3, bufferChars: 0, secondsSinceLastMessage: 0));
        // Count = 2 should not
        Assert.False(BufferPolicy.ShouldTriggerSummary(bufferCount: 2, bufferChars: 0, secondsSinceLastMessage: 0));
    }

    // ── IsBufferExpired ──────────────────────────────────────────────────────

    [Fact]
    public void IsBufferExpired_ReturnsTrue_WhenOlderThanExpiration()
    {
        BufferPolicy.ExpirationSeconds = 300;
        var oldTimestamp = DateTimeOffset.UtcNow.AddSeconds(-301);
        Assert.True(BufferPolicy.IsBufferExpired(oldTimestamp));
    }

    [Fact]
    public void IsBufferExpired_ReturnsFalse_WhenWithinExpiration()
    {
        BufferPolicy.ExpirationSeconds = 300;
        var recentTimestamp = DateTimeOffset.UtcNow.AddSeconds(-100);
        Assert.False(BufferPolicy.IsBufferExpired(recentTimestamp));
    }

    // ── IsBufferFull ─────────────────────────────────────────────────────────

    [Fact]
    public void IsBufferFull_ReturnsTrueWhenMessageLimitReached()
    {
        BufferPolicy.MaxMessages = 6;
        Assert.True(BufferPolicy.IsBufferFull(bufferCount: 6, bufferChars: 0));
    }

    [Fact]
    public void IsBufferFull_ReturnsTrueWhenCharLimitReached()
    {
        BufferPolicy.MaxChars = 500;
        Assert.True(BufferPolicy.IsBufferFull(bufferCount: 1, bufferChars: 500));
    }

    [Fact]
    public void IsBufferFull_ReturnsFalseWhenBelowBothLimits()
    {
        BufferPolicy.MaxMessages = 6;
        BufferPolicy.MaxChars    = 500;
        Assert.False(BufferPolicy.IsBufferFull(bufferCount: 3, bufferChars: 200));
    }
}
