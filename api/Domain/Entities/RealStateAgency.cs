namespace Domain.Entities;

public class RealStateAgency
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Nudge configuration — broker-level overrides these if set
    public bool NudgeEnabled { get; set; } = false;
    public int NudgeTimeoutMinutes { get; set; } = 10;
    public int NudgeBrokerAfterMessages { get; set; } = 3;

    // Navigation
    public ICollection<RealStateBroker> BrokerAssignments { get; set; } = [];
}
