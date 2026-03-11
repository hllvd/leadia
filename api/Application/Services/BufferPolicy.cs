namespace Application.Services;

/// <summary>
/// Pure static functions that encode the buffer and summary trigger policy from API.md §5.
/// All values are configurable via the static properties (defaults match the spec).
/// </summary>
public static class BufferPolicy
{
    // ── Configurable thresholds (override in tests or app startup) ──────────
    public static int    MaxMessages             { get; set; } = 6;
    public static int    MaxChars                { get; set; } = 500;
    public static int    SummaryTriggerMessages  { get; set; } = 5;
    public static int    SummaryTriggerChars     { get; set; } = 400;
    public static double TimeoutSeconds          { get; set; } = 30.0;
    public static double ExpirationSeconds       { get; set; } = 300.0;   // 5 minutes

    /// <summary>
    /// Returns true when at least one rolling-summary trigger condition is met:
    ///   • buffer message count ≥ SummaryTriggerMessages
    ///   • buffer character count ≥ SummaryTriggerChars
    ///   • seconds since last message > TimeoutSeconds
    /// </summary>
    public static bool ShouldTriggerSummary(int bufferCount, int bufferChars, double secondsSinceLastMessage)
        => bufferCount  >= SummaryTriggerMessages
        || bufferChars  >= SummaryTriggerChars
        || secondsSinceLastMessage > TimeoutSeconds;

    /// <summary>
    /// Returns true when the buffer has been alive for longer than ExpirationSeconds
    /// without being flushed (force-flush scenario).
    /// </summary>
    public static bool IsBufferExpired(DateTimeOffset lastActivityTimestamp)
        => (DateTimeOffset.UtcNow - lastActivityTimestamp).TotalSeconds > ExpirationSeconds;

    /// <summary>
    /// Returns true when the buffer is full (any hard limit reached).
    /// </summary>
    public static bool IsBufferFull(int bufferCount, int bufferChars)
        => bufferCount >= MaxMessages || bufferChars >= MaxChars;
}
