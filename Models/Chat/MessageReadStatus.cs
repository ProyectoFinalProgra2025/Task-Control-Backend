namespace TaskControlBackend.Models.Chat;

/// <summary>
/// Tracking individual de LECTURA de mensajes (palomitas dobles ✓✓)
/// Registra cuándo un mensaje fue LEÍDO por cada destinatario
/// Soporta múltiples lectores en chats grupales
/// </summary>
public class MessageReadStatus
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID del mensaje
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// ID del usuario que leyó el mensaje
    /// </summary>
    public Guid ReadByUserId { get; set; }

    /// <summary>
    /// Fecha y hora en que el mensaje fue leído por este usuario
    /// </summary>
    public DateTimeOffset ReadAt { get; set; } = DateTimeOffset.UtcNow;

    // ==================== NAVIGATION PROPERTIES ====================

    /// <summary>
    /// Mensaje al que pertenece este estado de lectura
    /// </summary>
    public ChatMessage Message { get; set; } = default!;

    /// <summary>
    /// Usuario que leyó el mensaje
    /// </summary>
    public Usuario ReadByUser { get; set; } = default!;
}
