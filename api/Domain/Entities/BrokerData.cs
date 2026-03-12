using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

public class BrokerData
{
    [Required]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string BrokerId { get; set; } = string.Empty;

    [Required]
    public string DataName { get; set; } = string.Empty;

    [Required]
    public string DataKey { get; set; } = string.Empty;

    public string DataValue { get; set; } = string.Empty;

    public bool IsPreferred { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public User? Broker { get; set; }
}
