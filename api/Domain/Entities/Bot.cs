namespace Domain.Entities;

public class Bot
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BotNumber { get; set; } = string.Empty;
    public string BotName { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Soul { get; set; } = string.Empty;
    public bool IsAgent { get; set; } = false;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public ICollection<User> Users { get; set; } = [];
}
