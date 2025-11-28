namespace TaskControlBackend.Models.Chat;

public class Message
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public Guid SenderId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTimeOffset? ReadAt { get; set; }

    public Chat Chat { get; set; } = default!;
    public Usuario Sender { get; set; } = default!;
}
