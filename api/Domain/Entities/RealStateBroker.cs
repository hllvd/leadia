using Domain.Enums;

namespace Domain.Entities;

public class RealStateBroker
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RealStateAgencyId { get; set; } = string.Empty;
    public string BrokerId { get; set; } = string.Empty;
    public ConversationMode Mode { get; set; } = ConversationMode.OnlyListening;

    // Navigation
    public RealStateAgency? RealStateAgency { get; set; }
    public User? Broker { get; set; }
}
