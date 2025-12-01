using TaskControlBackend.Models.Chat;

namespace TaskControlBackend.DTOs.Chat;

// ==================== REQUEST DTOs ====================

/// <summary>
/// Request para buscar usuarios con los que chatear
/// </summary>
public class SearchUsersRequest
{
    public string SearchTerm { get; set; } = string.Empty;
}

/// <summary>
/// Request para crear una conversación directa (1:1)
/// </summary>
public class CreateDirectConversationRequest
{
    public Guid OtherUserId { get; set; }
}

/// <summary>
/// Request para crear una conversación grupal
/// </summary>
public class CreateGroupConversationRequest
{
    public string GroupName { get; set; } = string.Empty;
    public List<Guid> MemberIds { get; set; } = new();
    public string? ImageUrl { get; set; }
}

/// <summary>
/// Request para actualizar información de un grupo
/// </summary>
public class UpdateConversationRequest
{
    public string? Name { get; set; }
    public string? ImageUrl { get; set; }
}

/// <summary>
/// Request para agregar miembros a un grupo
/// </summary>
public class AddMembersRequest
{
    public List<Guid> MemberIds { get; set; } = new();
}

/// <summary>
/// Request para obtener mensajes de una conversación con paginación
/// </summary>
public class GetMessagesRequest
{
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 50;
}

/// <summary>
/// Request para enviar un mensaje de texto
/// </summary>
public class SendTextMessageRequest
{
    public string Content { get; set; } = string.Empty;
    public Guid? ReplyToMessageId { get; set; }
}

/// <summary>
/// Request para editar un mensaje
/// </summary>
public class EditMessageRequest
{
    public string NewContent { get; set; } = string.Empty;
}

// ==================== RESPONSE DTOs ====================

/// <summary>
/// DTO simplificado de Usuario para búsqueda de chat
/// </summary>
public class UserSearchResultDTO
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string? Departamento { get; set; }
    public Guid? EmpresaId { get; set; }
    public string? EmpresaNombre { get; set; }
}

/// <summary>
/// DTO de Conversación con información resumida
/// </summary>
public class ConversationDTO
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty; // "Direct" o "Group"
    public string? Name { get; set; }
    public string? ImageUrl { get; set; }
    public Guid CreatedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }

    public List<ConversationMemberDTO> Members { get; set; } = new();
    public ChatMessageDTO? LastMessage { get; set; }
    public int UnreadCount { get; set; }
}

/// <summary>
/// DTO de miembro de conversación
/// </summary>
public class ConversationMemberDTO
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // "Member" o "Admin"
    public DateTimeOffset JoinedAt { get; set; }
    public bool IsMuted { get; set; }
    public DateTimeOffset? LastReadAt { get; set; }
}

/// <summary>
/// DTO completo de mensaje de chat
/// </summary>
public class ChatMessageDTO
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty; // "Text", "Image", "Document", "Audio", "Video"
    public string Content { get; set; } = string.Empty;

    // File metadata (si aplica)
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? FileMimeType { get; set; }

    public DateTimeOffset SentAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public string Status { get; set; } = string.Empty; // "Sent", "Delivered", "Read"

    public bool IsEdited { get; set; }
    public DateTimeOffset? EditedAt { get; set; }

    public Guid? ReplyToMessageId { get; set; }
    public ChatMessageDTO? ReplyToMessage { get; set; }

    // Read receipts (para mostrar quién leyó en chats grupales)
    public List<MessageReadReceiptDTO> ReadReceipts { get; set; } = new();
    public List<MessageDeliveryReceiptDTO> DeliveryReceipts { get; set; } = new();
}

/// <summary>
/// DTO de confirmación de lectura individual
/// </summary>
public class MessageReadReceiptDTO
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTimeOffset ReadAt { get; set; }
}

/// <summary>
/// DTO de confirmación de entrega individual
/// </summary>
public class MessageDeliveryReceiptDTO
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTimeOffset DeliveredAt { get; set; }
}

/// <summary>
/// Response genérica para operaciones exitosas
/// </summary>
public class SuccessResponse
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Response con contador de mensajes marcados
/// </summary>
public class MarkMessagesResponse
{
    public bool Success { get; set; } = true;
    public int MessagesMarked { get; set; }
    public string Message { get; set; } = string.Empty;
}
