namespace TaskControlBackend.Models.Chat;

/// <summary>
/// Representa un mensaje dentro de una conversación
/// </summary>
public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID de la conversación a la que pertenece el mensaje
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// ID del usuario que envió el mensaje
    /// </summary>
    public Guid SenderId { get; set; }

    /// <summary>
    /// Tipo de contenido del mensaje
    /// </summary>
    public MessageContentType ContentType { get; set; } = MessageContentType.Text;

    /// <summary>
    /// Contenido del mensaje
    /// - Para Text: el texto del mensaje
    /// - Para Image/Document/Audio/Video: descripción o caption (opcional)
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// URL del archivo en Blob Storage (para mensajes con archivos adjuntos)
    /// NULL para mensajes de solo texto
    /// </summary>
    public string? FileUrl { get; set; }

    /// <summary>
    /// Nombre original del archivo (si aplica)
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Tamaño del archivo en bytes (si aplica)
    /// </summary>
    public long? FileSizeBytes { get; set; }

    /// <summary>
    /// MIME type del archivo (ej: "image/png", "application/pdf")
    /// </summary>
    public string? FileMimeType { get; set; }

    /// <summary>
    /// Fecha y hora de envío del mensaje
    /// </summary>
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Fecha y hora de la primera entrega (cuando al menos 1 destinatario lo recibió)
    /// NULL = aún no entregado
    /// </summary>
    public DateTimeOffset? DeliveredAt { get; set; }

    /// <summary>
    /// Fecha y hora de la primera lectura (cuando al menos 1 destinatario lo leyó)
    /// NULL = aún no leído
    /// </summary>
    public DateTimeOffset? ReadAt { get; set; }

    /// <summary>
    /// Estado actual del mensaje (Sent/Delivered/Read)
    /// Se actualiza automáticamente basado en DeliveredAt y ReadAt
    /// </summary>
    public MessageStatus Status { get; set; } = MessageStatus.Sent;

    /// <summary>
    /// Indica si el mensaje fue editado
    /// </summary>
    public bool IsEdited { get; set; } = false;

    /// <summary>
    /// Fecha de la última edición (si fue editado)
    /// </summary>
    public DateTimeOffset? EditedAt { get; set; }

    /// <summary>
    /// Indica si el mensaje fue eliminado
    /// Los mensajes eliminados permanecen en la BD pero se marcan como tal
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Fecha de eliminación (si fue eliminado)
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// ID del mensaje al que responde (threading/replies)
    /// NULL si no es una respuesta
    /// </summary>
    public Guid? ReplyToMessageId { get; set; }

    // ==================== NAVIGATION PROPERTIES ====================

    /// <summary>
    /// Conversación a la que pertenece el mensaje
    /// </summary>
    public Conversation Conversation { get; set; } = default!;

    /// <summary>
    /// Usuario que envió el mensaje
    /// </summary>
    public Usuario Sender { get; set; } = default!;

    /// <summary>
    /// Mensaje al que responde (si aplica)
    /// </summary>
    public ChatMessage? ReplyToMessage { get; set; }

    /// <summary>
    /// Respuestas a este mensaje
    /// </summary>
    public ICollection<ChatMessage> Replies { get; set; } = new List<ChatMessage>();

    /// <summary>
    /// Confirmaciones de entrega individual por usuario
    /// </summary>
    public ICollection<MessageDeliveryStatus> DeliveryStatuses { get; set; } = new List<MessageDeliveryStatus>();

    /// <summary>
    /// Confirmaciones de lectura individual por usuario
    /// </summary>
    public ICollection<MessageReadStatus> ReadStatuses { get; set; } = new List<MessageReadStatus>();
}
