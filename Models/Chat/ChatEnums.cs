namespace TaskControlBackend.Models.Chat;

/// <summary>
/// Tipo de conversación
/// </summary>
public enum ConversationType
{
    /// <summary>
    /// Chat directo entre 2 usuarios (1:1)
    /// </summary>
    Direct = 1,

    /// <summary>
    /// Chat grupal con múltiples usuarios
    /// </summary>
    Group = 2
}

/// <summary>
/// Rol del usuario dentro de una conversación
/// </summary>
public enum ConversationMemberRole
{
    /// <summary>
    /// Miembro regular del chat
    /// </summary>
    Member = 1,

    /// <summary>
    /// Administrador del chat (puede agregar/remover miembros)
    /// Solo aplicable a chats grupales
    /// </summary>
    Admin = 2
}

/// <summary>
/// Estado de entrega y lectura de un mensaje
/// </summary>
public enum MessageStatus
{
    /// <summary>
    /// Mensaje enviado pero no entregado a ningún destinatario
    /// </summary>
    Sent = 1,

    /// <summary>
    /// Mensaje entregado a al menos un destinatario
    /// </summary>
    Delivered = 2,

    /// <summary>
    /// Mensaje leído por al menos un destinatario
    /// </summary>
    Read = 3
}

/// <summary>
/// Tipo de contenido del mensaje
/// </summary>
public enum MessageContentType
{
    /// <summary>
    /// Mensaje de texto plano
    /// </summary>
    Text = 1,

    /// <summary>
    /// Imagen (PNG, JPG, GIF, etc.)
    /// </summary>
    Image = 2,

    /// <summary>
    /// Documento (PDF, DOCX, XLSX, etc.)
    /// </summary>
    Document = 3,

    /// <summary>
    /// Audio (MP3, WAV, etc.)
    /// </summary>
    Audio = 4,

    /// <summary>
    /// Video (MP4, AVI, etc.)
    /// </summary>
    Video = 5
}
