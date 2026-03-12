namespace Domain.Entities;

public class RealStateAgency
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Navigation
    public ICollection<RealStateBroker> BrokerAssignments { get; set; } = [];
}
