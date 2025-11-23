namespace TaskControlBackend.Models.Chat;

public class Message
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public int SenderId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public Chat Chat { get; set; } = default!;
    public Usuario Sender { get; set; } = default!;
}
