using Domain.Enums;

namespace Domain.Entities;

public class Message
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string BotId { get; set; } = string.Empty;
    public SenderType Sender { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public User? User { get; set; }
    public Bot? Bot { get; set; }
}
