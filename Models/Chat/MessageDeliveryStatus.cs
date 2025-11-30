namespace TaskControlBackend.Models.Chat;

/// <summary>
/// Tracking individual de ENTREGA de mensajes (palomita simple ✓)
/// Registra cuándo un mensaje fue ENTREGADO a cada destinatario
/// </summary>
public class MessageDeliveryStatus
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID del mensaje
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// ID del usuario que recibió el mensaje
    /// </summary>
    public Guid DeliveredToUserId { get; set; }

    /// <summary>
    /// Fecha y hora en que el mensaje fue entregado a este usuario
    /// </summary>
    public DateTimeOffset DeliveredAt { get; set; } = DateTimeOffset.UtcNow;

    // ==================== NAVIGATION PROPERTIES ====================

    /// <summary>
    /// Mensaje al que pertenece este estado de entrega
    /// </summary>
    public ChatMessage Message { get; set; } = default!;

    /// <summary>
    /// Usuario que recibió el mensaje
    /// </summary>
    public Usuario DeliveredToUser { get; set; } = default!;
}
