namespace TaskControlBackend.Models.Chat;

/// <summary>
/// Representa una conversación (chat 1:1 o grupal)
/// </summary>
public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Tipo de conversación (Direct o Group)
    /// </summary>
    public ConversationType Type { get; set; }

    /// <summary>
    /// Nombre de la conversación (solo para chats grupales)
    /// NULL para chats directos (1:1)
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// URL de la imagen del chat grupal (opcional)
    /// Almacenado en Blob Storage
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Usuario que creó la conversación
    /// </summary>
    public Guid CreatedById { get; set; }

    /// <summary>
    /// Fecha de creación
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Última actualización (útil para ordenar chats por actividad)
    /// </summary>
    public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Indica si la conversación está activa
    /// False = archivada o eliminada
    /// </summary>
    public bool IsActive { get; set; } = true;

    // ==================== NAVIGATION PROPERTIES ====================

    /// <summary>
    /// Usuario creador de la conversación
    /// </summary>
    public Usuario CreatedBy { get; set; } = default!;

    /// <summary>
    /// Miembros de la conversación
    /// </summary>
    public ICollection<ConversationMember> Members { get; set; } = new List<ConversationMember>();

    /// <summary>
    /// Mensajes de la conversación
    /// </summary>
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
