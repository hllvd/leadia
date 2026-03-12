namespace Domain.Entities;

public class RealStateBroker
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RealStateAgencyId { get; set; } = string.Empty;
    public string BrokerId { get; set; } = string.Empty;

    // Navigation
    public RealStateAgency? RealStateAgency { get; set; }
    public User? Broker { get; set; }
}
