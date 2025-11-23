namespace TaskControlBackend.Models.Chat;

public class Chat
{
    public Guid Id { get; set; }
    public ChatType Type { get; set; }
    public string? Name { get; set; } // null for 1:1
    public Guid? CreatedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Usuario? CreatedBy { get; set; }
    public ICollection<ChatMember> Members { get; set; } = new List<ChatMember>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
