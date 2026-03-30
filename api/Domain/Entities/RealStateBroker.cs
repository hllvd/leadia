using Domain.Enums;

namespace Domain.Entities;

public class RealStateBroker
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RealStateAgencyId { get; set; } = string.Empty;
    public string BrokerId { get; set; } = string.Empty;
    public ConversationMode Mode { get; set; } = ConversationMode.OnlyListening;

    // nullable — null = use agency default
    public bool? NudgeEnabled { get; set; } = null;
    public int? NudgeTimeoutMinutes { get; set; } = null;
    public int? NudgeBrokerAfterMessages { get; set; } = null;

    // Navigation
    public RealStateAgency? RealStateAgency { get; set; }
    public User? Broker { get; set; }
}
