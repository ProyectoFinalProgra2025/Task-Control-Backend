namespace TaskControlBackend.Models.Chat;

public class ChatMember
{
    public Guid ChatId { get; set; }
    public int UserId { get; set; }
    public ChatRole Role { get; set; }
    public DateTimeOffset JoinedAt { get; set; }

    public Chat Chat { get; set; } = default!;
    public Usuario User { get; set; } = default!;
}
