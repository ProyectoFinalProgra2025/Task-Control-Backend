using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskControlBackend.Models.Chat;
using TaskControlBackend.Services.Interfaces;

namespace TaskControlBackend.Controllers;

/// <summary>
/// Controlador de chat COMPLETAMENTE REHECHO - APIs REST para chat
/// </summary>
[Authorize]
[Route("api/[controller]")]
public class ChatController : BaseController
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    // ==================== BÚSQUEDA DE USUARIOS ====================

    /// <summary>
    /// Buscar usuarios para iniciar chat
    /// GET /api/chat/users/search?term=juan
    /// </summary>
    [HttpGet("users/search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string term = "")
    {
        try
        {
            var userId = GetUserId();
            var users = await _chatService.SearchUsersAsync(userId, term);

            var userDtos = users.Select(u => new
            {
                id = u.Id,
                nombreCompleto = u.NombreCompleto,
                email = u.Email,
                rol = u.Rol.ToString(),
                empresaId = u.EmpresaId
            });

            return Ok(userDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error buscando usuarios");
            return Error("Error al buscar usuarios", 500);
        }
    }

    // ==================== CONVERSACIONES ====================

    /// <summary>
    /// Obtener todas las conversaciones del usuario
    /// GET /api/chat/conversations
    /// </summary>
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        try
        {
            var userId = GetUserId();
            var conversations = await _chatService.GetUserConversationsAsync(userId);

            var conversationDtos = conversations.Select(c => new
            {
                id = c.Id,
                type = c.Type.ToString(),
                name = c.Name,
                imageUrl = c.ImageUrl,
                createdById = c.CreatedById,
                createdAt = c.CreatedAt,
                lastActivityAt = c.LastActivityAt,
                isActive = c.IsActive,
                members = c.Members.Select(m => new
                {
                    userId = m.UserId,
                    userName = m.User?.NombreCompleto ?? "",
                    role = m.Role.ToString(),
                    joinedAt = m.JoinedAt,
                    isMuted = m.IsMuted,
                    lastReadAt = m.LastReadAt,
                    isActive = m.IsActive
                }).ToList(),
                lastMessage = c.Messages.FirstOrDefault() == null ? null : new
                {
                    id = c.Messages.First().Id,
                    senderId = c.Messages.First().SenderId,
                    senderName = c.Messages.First().Sender?.NombreCompleto ?? "",
                    content = c.Messages.First().Content,
                    contentType = c.Messages.First().ContentType.ToString(),
                    sentAt = c.Messages.First().SentAt,
                    status = c.Messages.First().Status.ToString()
                },
                unreadCount = _chatService.GetUnreadMessageCountAsync(c.Id, userId).Result
            });

            return Ok(conversationDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo conversaciones");
            return Error("Error al obtener conversaciones", 500);
        }
    }

    /// <summary>
    /// Obtener conversación por ID
    /// GET /api/chat/conversations/{id}
    /// </summary>
    [HttpGet("conversations/{id}")]
    public async Task<IActionResult> GetConversation(Guid id)
    {
        try
        {
            var userId = GetUserId();
            var conversation = await _chatService.GetConversationByIdAsync(id, userId);

            if (conversation == null)
                return NotFound(new { message = "Conversación no encontrada" });

            var conversationDto = new
            {
                id = conversation.Id,
                type = conversation.Type.ToString(),
                name = conversation.Name,
                imageUrl = conversation.ImageUrl,
                createdById = conversation.CreatedById,
                createdAt = conversation.CreatedAt,
                lastActivityAt = conversation.LastActivityAt,
                isActive = conversation.IsActive,
                members = conversation.Members.Select(m => new
                {
                    userId = m.UserId,
                    userName = m.User?.NombreCompleto ?? "",
                    role = m.Role.ToString(),
                    joinedAt = m.JoinedAt,
                    isMuted = m.IsMuted,
                    lastReadAt = m.LastReadAt,
                    isActive = m.IsActive
                }).ToList()
            };

            return Ok(conversationDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo conversación {ConvId}", id);
            return Error("Error al obtener conversación", 500);
        }
    }

    /// <summary>
    /// Crear o obtener conversación directa con un usuario
    /// POST /api/chat/conversations/direct
    /// Body: { "recipientUserId": "guid" }
    /// </summary>
    [HttpPost("conversations/direct")]
    public async Task<IActionResult> CreateDirectConversation([FromBody] CreateDirectConversationRequest request)
    {
        try
        {
            var userId = GetUserId();

            if (request.RecipientUserId == userId)
                return BadRequest(new { message = "No puedes crear un chat contigo mismo" });

            var conversation = await _chatService.GetOrCreateDirectConversationAsync(userId, request.RecipientUserId);

            return Ok(new { conversationId = conversation.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando conversación directa");
            return Error("Error al crear conversación", 500);
        }
    }

    /// <summary>
    /// Crear conversación grupal
    /// POST /api/chat/conversations/group
    /// Body: { "name": "string", "memberIds": ["guid1", "guid2"], "imageUrl": "string" }
    /// </summary>
    [HttpPost("conversations/group")]
    public async Task<IActionResult> CreateGroupConversation([FromBody] CreateGroupConversationRequest request)
    {
        try
        {
            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { message = "El nombre del grupo es requerido" });

            if (request.MemberIds == null || request.MemberIds.Count < 1)
                return BadRequest(new { message = "Debes agregar al menos un miembro" });

            var conversation = await _chatService.CreateGroupConversationAsync(
                userId,
                request.Name,
                request.MemberIds,
                request.ImageUrl
            );

            return Ok(new { conversationId = conversation.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando conversación grupal");
            return Error("Error al crear grupo", 500);
        }
    }

    /// <summary>
    /// Actualizar conversación (solo admin)
    /// PUT /api/chat/conversations/{id}
    /// Body: { "name": "string", "imageUrl": "string" }
    /// </summary>
    [HttpPut("conversations/{id}")]
    public async Task<IActionResult> UpdateConversation(Guid id, [FromBody] UpdateConversationRequest request)
    {
        try
        {
            var userId = GetUserId();

            var success = await _chatService.UpdateConversationAsync(
                id,
                userId,
                request.Name,
                request.ImageUrl
            );

            if (!success)
                return Forbid();

            return Ok(new { message = "Conversación actualizada" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando conversación {ConvId}", id);
            return Error("Error al actualizar conversación", 500);
        }
    }

    // ==================== MENSAJES ====================

    /// <summary>
    /// Obtener mensajes de una conversación
    /// GET /api/chat/conversations/{id}/messages?skip=0&take=50
    /// </summary>
    [HttpGet("conversations/{id}/messages")]
    public async Task<IActionResult> GetMessages(Guid id, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        try
        {
            var userId = GetUserId();
            var messages = await _chatService.GetConversationMessagesAsync(id, userId, skip, take);

            var messageDtos = messages.Select(m => new
            {
                id = m.Id,
                conversationId = m.ConversationId,
                senderId = m.SenderId,
                senderName = m.Sender?.NombreCompleto ?? "",
                contentType = m.ContentType.ToString(),
                content = m.Content,
                fileUrl = m.FileUrl,
                fileName = m.FileName,
                fileSizeBytes = m.FileSizeBytes,
                fileMimeType = m.FileMimeType,
                sentAt = m.SentAt,
                deliveredAt = m.DeliveredAt,
                readAt = m.ReadAt,
                status = m.Status.ToString(),
                isEdited = m.IsEdited,
                editedAt = m.EditedAt,
                isDeleted = m.IsDeleted,
                replyToMessageId = m.ReplyToMessageId,
                replyToMessage = m.ReplyToMessage == null ? null : new
                {
                    id = m.ReplyToMessage.Id,
                    senderId = m.ReplyToMessage.SenderId,
                    senderName = m.ReplyToMessage.Sender?.NombreCompleto ?? "",
                    content = m.ReplyToMessage.Content
                }
            });

            return Ok(messageDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo mensajes de conversación {ConvId}", id);
            return Error("Error al obtener mensajes", 500);
        }
    }

    /// <summary>
    /// Enviar archivo
    /// POST /api/chat/conversations/{id}/files
    /// Form: file, contentType?, replyToMessageId?
    /// </summary>
    [HttpPost("conversations/{id}/files")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50MB max
    public async Task<IActionResult> SendFile(Guid id, [FromForm] IFormFile file, [FromForm] string? replyToMessageId = null)
    {
        try
        {
            var userId = GetUserId();

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Se requiere un archivo válido" });

            // Validar tipo de archivo
            var contentType = GetContentTypeFromFile(file);
            var allowedTypes = new[] { "image", "document", "audio", "video" };
            if (!allowedTypes.Contains(contentType))
                return BadRequest(new { message = "Tipo de archivo no permitido" });

            // Validar tamaño (50MB max)
            if (file.Length > 50 * 1024 * 1024)
                return BadRequest(new { message = "El archivo excede el tamaño máximo permitido (50MB)" });

            // Por ahora no guardamos el archivo, solo la metadata
            // TODO: Implementar Azure Blob Storage o similar
            using var fileStream = file.OpenReadStream();

            var message = await _chatService.SendFileMessageAsync(
                userId,
                id,
                (MessageContentType)Enum.Parse(typeof(MessageContentType), contentType, true),
                file.FileName,
                fileStream,
                file.FileName,
                file.ContentType,
                string.IsNullOrEmpty(replyToMessageId) ? null : Guid.Parse(replyToMessageId));

            var messageDto = new
            {
                id = message.Id,
                conversationId = message.ConversationId,
                senderId = message.SenderId,
                senderName = message.Sender?.NombreCompleto ?? "",
                contentType = message.ContentType.ToString(),
                content = message.Content,
                fileName = message.FileName,
                fileSizeBytes = message.FileSizeBytes,
                fileMimeType = message.FileMimeType,
                sentAt = message.SentAt,
                deliveredAt = message.DeliveredAt,
                readAt = message.ReadAt,
                status = message.Status.ToString(),
                isEdited = message.IsEdited,
                replyToMessageId = message.ReplyToMessageId
            };

            return Ok(messageDto);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando archivo a conversación {ConvId}", id);
            return Error("Error al enviar archivo", 500);
        }
    }

    private string GetContentTypeFromFile(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var mimeType = file.ContentType.ToLowerInvariant();

        // Imágenes
        if (mimeType.StartsWith("image/") || extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp")
            return "Image";

        // Documentos
        if (mimeType.StartsWith("application/") || extension is ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt")
            return "Document";

        // Audio
        if (mimeType.StartsWith("audio/") || extension is ".mp3" or ".wav" or ".ogg" or ".m4a")
            return "Audio";

        // Video
        if (mimeType.StartsWith("video/") || extension is ".mp4" or ".avi" or ".mov" or ".wmv" or ".mkv")
            return "Video";

        return "Document"; // Default
    }

    /// <summary>
    /// Enviar mensaje de texto
    /// POST /api/chat/conversations/{id}/messages
    /// Body: { "content": "string", "replyToMessageId": "guid" }
    /// </summary>
    [HttpPost("conversations/{id}/messages")]
    public async Task<IActionResult> SendMessage(Guid id, [FromBody] SendMessageRequest request)
    {
        try
        {
            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { message = "El contenido del mensaje es requerido" });

            var message = await _chatService.SendTextMessageAsync(
                userId,
                id,
                request.Content,
                request.ReplyToMessageId
            );

            var messageDto = new
            {
                id = message.Id,
                conversationId = message.ConversationId,
                senderId = message.SenderId,
                senderName = message.Sender?.NombreCompleto ?? "",
                contentType = message.ContentType.ToString(),
                content = message.Content,
                sentAt = message.SentAt,
                deliveredAt = message.DeliveredAt,
                readAt = message.ReadAt,
                status = message.Status.ToString(),
                isEdited = message.IsEdited,
                replyToMessageId = message.ReplyToMessageId
            };

            return Ok(messageDto);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando mensaje a conversación {ConvId}", id);
            return Error("Error al enviar mensaje", 500);
        }
    }

    /// <summary>
    /// Editar mensaje
    /// PUT /api/chat/messages/{id}
    /// Body: { "content": "string" }
    /// </summary>
    [HttpPut("messages/{id}")]
    public async Task<IActionResult> EditMessage(Guid id, [FromBody] EditMessageRequest request)
    {
        try
        {
            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { message = "El contenido es requerido" });

            var success = await _chatService.EditMessageAsync(id, userId, request.Content);

            if (!success)
                return NotFound(new { message = "Mensaje no encontrado o no autorizado" });

            return Ok(new { message = "Mensaje editado" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editando mensaje {MsgId}", id);
            return Error("Error al editar mensaje", 500);
        }
    }

    /// <summary>
    /// Eliminar mensaje
    /// DELETE /api/chat/messages/{id}
    /// </summary>
    [HttpDelete("messages/{id}")]
    public async Task<IActionResult> DeleteMessage(Guid id)
    {
        try
        {
            var userId = GetUserId();

            var success = await _chatService.DeleteMessageAsync(id, userId);

            if (!success)
                return NotFound(new { message = "Mensaje no encontrado o no autorizado" });

            return Ok(new { message = "Mensaje eliminado" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando mensaje {MsgId}", id);
            return Error("Error al eliminar mensaje", 500);
        }
    }

    // ==================== CONFIRMACIONES DE LECTURA ====================

    /// <summary>
    /// Marcar mensaje como entregado
    /// POST /api/chat/messages/{id}/delivered
    /// </summary>
    [HttpPost("messages/{id}/delivered")]
    public async Task<IActionResult> MarkAsDelivered(Guid id)
    {
        try
        {
            var userId = GetUserId();
            var success = await _chatService.MarkMessageAsDeliveredAsync(id, userId);

            if (!success)
                return NotFound(new { message = "Mensaje no encontrado" });

            return Ok(new { message = "Marcado como entregado" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marcando mensaje {MsgId} como entregado", id);
            return Error("Error al marcar como entregado", 500);
        }
    }

    /// <summary>
    /// Marcar mensaje como leído
    /// POST /api/chat/messages/{id}/read
    /// </summary>
    [HttpPost("messages/{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        try
        {
            var userId = GetUserId();
            var success = await _chatService.MarkMessageAsReadAsync(id, userId);

            if (!success)
                return NotFound(new { message = "Mensaje no encontrado" });

            return Ok(new { message = "Marcado como leído" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marcando mensaje {MsgId} como leído", id);
            return Error("Error al marcar como leído", 500);
        }
    }

    /// <summary>
    /// Marcar todos los mensajes de una conversación como leídos
    /// POST /api/chat/conversations/{id}/read-all
    /// </summary>
    [HttpPost("conversations/{id}/read-all")]
    public async Task<IActionResult> MarkAllAsRead(Guid id)
    {
        try
        {
            var userId = GetUserId();
            var count = await _chatService.MarkAllMessagesAsReadAsync(id, userId);

            return Ok(new { message = $"{count} mensajes marcados como leídos", count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marcando todos los mensajes como leídos en conversación {ConvId}", id);
            return Error("Error al marcar mensajes como leídos", 500);
        }
    }

    /// <summary>
    /// Obtener contador de mensajes no leídos de una conversación
    /// GET /api/chat/conversations/{id}/unread-count
    /// </summary>
    [HttpGet("conversations/{id}/unread-count")]
    public async Task<IActionResult> GetUnreadCount(Guid id)
    {
        try
        {
            var userId = GetUserId();
            var count = await _chatService.GetUnreadMessageCountAsync(id, userId);

            return Ok(new { conversationId = id, unreadCount = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo contador de no leídos en conversación {ConvId}", id);
            return Error("Error al obtener contador", 500);
        }
    }
}

// ==================== DTOs DE REQUEST ====================

public class CreateDirectConversationRequest
{
    public Guid RecipientUserId { get; set; }
}

public class CreateGroupConversationRequest
{
    public string Name { get; set; } = string.Empty;
    public List<Guid> MemberIds { get; set; } = new();
    public string? ImageUrl { get; set; }
}

public class UpdateConversationRequest
{
    public string? Name { get; set; }
    public string? ImageUrl { get; set; }
}

public class SendMessageRequest
{
    public string Content { get; set; } = string.Empty;
    public Guid? ReplyToMessageId { get; set; }
}

public class EditMessageRequest
{
    public string Content { get; set; } = string.Empty;
}
