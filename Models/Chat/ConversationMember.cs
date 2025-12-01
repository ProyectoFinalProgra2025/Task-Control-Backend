namespace TaskControlBackend.Models.Chat;

/// <summary>
/// Representa la relación many-to-many entre Conversation y Usuario
/// </summary>
public class ConversationMember
{
    /// <summary>
    /// ID de la conversación
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// ID del usuario miembro
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Rol del usuario en la conversación (Member o Admin)
    /// </summary>
    public ConversationMemberRole Role { get; set; } = ConversationMemberRole.Member;

    /// <summary>
    /// Fecha en que el usuario se unió a la conversación
    /// </summary>
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Indica si el usuario tiene notificaciones silenciadas para este chat
    /// </summary>
    public bool IsMuted { get; set; } = false;

    /// <summary>
    /// Última vez que el usuario leyó mensajes en este chat
    /// Útil para calcular mensajes no leídos eficientemente
    /// </summary>
    public DateTimeOffset? LastReadAt { get; set; }

    /// <summary>
    /// Indica si el usuario está activo en la conversación
    /// False = usuario removido del chat
    /// </summary>
    public bool IsActive { get; set; } = true;

    // ==================== NAVIGATION PROPERTIES ====================

    /// <summary>
    /// Conversación a la que pertenece
    /// </summary>
    public Conversation Conversation { get; set; } = default!;

    /// <summary>
    /// Usuario miembro
    /// </summary>
    public Usuario User { get; set; } = default!;
}
