using Domain.Entities;

namespace Application.Services;

/// <summary>
/// Resolves the effective nudge configuration using a two-tier priority model:
///   1. RealStateBroker (highest priority — per-broker override)
///   2. RealStateAgency (default for all brokers in the agency)
/// </summary>
public static class NudgeConfigResolver
{
    private const int FallbackTimeoutMinutes = 10;
    private const int FallbackAfterMessages  = 3;
    private const bool FallbackEnabled       = false;

    public static bool IsEnabled(RealStateBroker? broker, RealStateAgency? agency)
        => broker?.NudgeEnabled
        ?? agency?.NudgeEnabled
        ?? FallbackEnabled;

    /// <summary>
    /// Returns the timeout in minutes before a nudge task is created,
    /// using broker settings if set, otherwise agency settings, otherwise fallback.
    /// </summary>
    public static int GetTimeoutMinutes(RealStateBroker? broker, RealStateAgency? agency)
        => broker?.NudgeTimeoutMinutes
        ?? agency?.NudgeTimeoutMinutes
        ?? FallbackTimeoutMinutes;

    /// <summary>
    /// Returns the number of unanswered broker messages before a nudge is triggered.
    /// </summary>
    public static int GetAfterMessages(RealStateBroker? broker, RealStateAgency? agency)
        => broker?.NudgeBrokerAfterMessages
        ?? agency?.NudgeBrokerAfterMessages
        ?? FallbackAfterMessages;
}
