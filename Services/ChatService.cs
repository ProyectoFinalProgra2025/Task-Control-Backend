using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using TaskControlBackend.Data;
using TaskControlBackend.Models;
using TaskControlBackend.Models.Chat;
using TaskControlBackend.Models.Enums;
using TaskControlBackend.Services.Interfaces;
using TaskControlBackend.Hubs;

namespace TaskControlBackend.Services;

/// <summary>
/// Servicio de chat COMPLETAMENTE REHECHO - Arquitectura simplificada y funcional
/// </summary>
public class ChatService : IChatService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<ChatService> _logger;

    public ChatService(AppDbContext db, IHubContext<ChatHub> hubContext, ILogger<ChatService> logger)
    {
        _db = db;
        _hubContext = hubContext;
        _logger = logger;
    }

    // ==================== BÚSQUEDA DE USUARIOS ====================

    public async Task<List<Usuario>> SearchUsersAsync(Guid currentUserId, string searchTerm)
    {
        var currentUser = await _db.Usuarios.FindAsync(currentUserId);
        if (currentUser == null) return new List<Usuario>();

        IQueryable<Usuario> query = _db.Usuarios.Where(u => u.Id != currentUserId);

        // Filtros por rol
        if (currentUser.Rol == RolUsuario.AdminGeneral)
        {
            query = query.Where(u => u.Rol == RolUsuario.AdminEmpresa);
        }
        else
        {
            query = query.Where(u => u.EmpresaId == currentUser.EmpresaId);
        }

        // Búsqueda por término
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(u =>
                u.NombreCompleto.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term));
        }

        return await query
            .OrderBy(u => u.NombreCompleto)
            .Take(20)
            .ToListAsync();
    }

    // ==================== CONVERSACIONES ====================

    public async Task<List<Conversation>> GetUserConversationsAsync(Guid userId)
    {
        return await _db.Conversations
            .Include(c => c.Members.Where(m => m.IsActive))
                .ThenInclude(m => m.User)
            .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
                .ThenInclude(m => m.Sender)
            .Where(c => c.Members.Any(m => m.UserId == userId && m.IsActive))
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.LastActivityAt)
            .ToListAsync();
    }

    public async Task<Conversation?> GetConversationByIdAsync(Guid conversationId, Guid userId)
    {
        return await _db.Conversations
            .Include(c => c.Members.Where(m => m.IsActive))
                .ThenInclude(m => m.User)
            .Include(c => c.CreatedBy)
            .Where(c => c.Id == conversationId)
            .Where(c => c.Members.Any(m => m.UserId == userId && m.IsActive))
            .FirstOrDefaultAsync();
    }

    public async Task<Conversation> GetOrCreateDirectConversationAsync(Guid userId1, Guid userId2)
    {
        // Buscar conversación existente
        var existing = await _db.Conversations
            .Include(c => c.Members)
            .Where(c => c.Type == ConversationType.Direct)
            .Where(c => c.Members.Count == 2)
            .Where(c => c.Members.Any(m => m.UserId == userId1 && m.IsActive))
            .Where(c => c.Members.Any(m => m.UserId == userId2 && m.IsActive))
            .FirstOrDefaultAsync();

        if (existing != null) return existing;

        // Crear nueva conversación
        var conversation = new Conversation
        {
            Type = ConversationType.Direct,
            CreatedById = userId1,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        _db.Conversations.Add(conversation);

        _db.ConversationMembers.Add(new ConversationMember
        {
            ConversationId = conversation.Id,
            UserId = userId1,
            Role = ConversationMemberRole.Member,
            JoinedAt = DateTimeOffset.UtcNow,
            IsActive = true
        });

        _db.ConversationMembers.Add(new ConversationMember
        {
            ConversationId = conversation.Id,
            UserId = userId2,
            Role = ConversationMemberRole.Member,
            JoinedAt = DateTimeOffset.UtcNow,
            IsActive = true
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation("✅ Conversación directa creada entre {User1} y {User2}: {ConvId}",
            userId1, userId2, conversation.Id);

        return conversation;
    }

    public async Task<Conversation> CreateGroupConversationAsync(Guid creatorId, string groupName, List<Guid> memberIds, string? imageUrl = null)
    {
        var conversation = new Conversation
        {
            Type = ConversationType.Group,
            Name = groupName,
            ImageUrl = imageUrl,
            CreatedById = creatorId,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        _db.Conversations.Add(conversation);

        // Creador como Admin
        _db.ConversationMembers.Add(new ConversationMember
        {
            ConversationId = conversation.Id,
            UserId = creatorId,
            Role = ConversationMemberRole.Admin,
            JoinedAt = DateTimeOffset.UtcNow,
            IsActive = true
        });

        // Otros miembros
        foreach (var memberId in memberIds.Where(id => id != creatorId))
        {
            _db.ConversationMembers.Add(new ConversationMember
            {
                ConversationId = conversation.Id,
                UserId = memberId,
                Role = ConversationMemberRole.Member,
                JoinedAt = DateTimeOffset.UtcNow,
                IsActive = true
            });
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("✅ Conversación grupal creada: {ConvId} - {Name}", conversation.Id, groupName);

        return conversation;
    }

    public async Task<bool> UpdateConversationAsync(Guid conversationId, Guid userId, string? newName, string? newImageUrl)
    {
        var member = await _db.ConversationMembers
            .Include(m => m.Conversation)
            .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == userId && m.IsActive);

        if (member == null || member.Role != ConversationMemberRole.Admin)
            return false;

        if (newName != null) member.Conversation.Name = newName;
        if (newImageUrl != null) member.Conversation.ImageUrl = newImageUrl;

        member.Conversation.LastActivityAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> AddMembersToGroupAsync(Guid conversationId, Guid requesterId, List<Guid> newMemberIds)
    {
        var requester = await _db.ConversationMembers
            .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == requesterId && m.IsActive);

        if (requester == null || requester.Role != ConversationMemberRole.Admin)
            return false;

        foreach (var memberId in newMemberIds)
        {
            var existing = await _db.ConversationMembers
                .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == memberId);

            if (existing == null)
            {
                _db.ConversationMembers.Add(new ConversationMember
                {
                    ConversationId = conversationId,
                    UserId = memberId,
                    Role = ConversationMemberRole.Member,
                    JoinedAt = DateTimeOffset.UtcNow,
                    IsActive = true
                });
            }
            else if (!existing.IsActive)
            {
                existing.IsActive = true;
                existing.JoinedAt = DateTimeOffset.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveMemberFromGroupAsync(Guid conversationId, Guid requesterId, Guid memberIdToRemove)
    {
        var requester = await _db.ConversationMembers
            .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == requesterId && m.IsActive);

        if (requester == null) return false;

        // Solo admin puede remover a otros, cualquiera puede salirse
        if (requester.Role != ConversationMemberRole.Admin && requesterId != memberIdToRemove)
            return false;

        var memberToRemove = await _db.ConversationMembers
            .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == memberIdToRemove);

        if (memberToRemove == null) return false;

        memberToRemove.IsActive = false;
        await _db.SaveChangesAsync();

        return true;
    }

    // ==================== MENSAJES ====================

    public async Task<List<ChatMessage>> GetConversationMessagesAsync(Guid conversationId, Guid userId, int skip = 0, int take = 50)
    {
        var isMember = await _db.ConversationMembers.AnyAsync(m =>
            m.ConversationId == conversationId &&
            m.UserId == userId &&
            m.IsActive);

        if (!isMember) return new List<ChatMessage>();

        return await _db.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.ReplyToMessage)
                .ThenInclude(m => m!.Sender)
            .Include(m => m.DeliveryStatuses)
            .Include(m => m.ReadStatuses)
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
            .OrderByDescending(m => m.SentAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<ChatMessage> SendTextMessageAsync(Guid senderId, Guid conversationId, string content, Guid? replyToMessageId = null)
    {
        var isMember = await _db.ConversationMembers.AnyAsync(m =>
            m.ConversationId == conversationId &&
            m.UserId == senderId &&
            m.IsActive);

        if (!isMember)
            throw new UnauthorizedAccessException("No perteneces a esta conversación");

        var message = new ChatMessage
        {
            ConversationId = conversationId,
            SenderId = senderId,
            ContentType = MessageContentType.Text,
            Content = content,
            SentAt = DateTimeOffset.UtcNow,
            Status = MessageStatus.Sent,
            ReplyToMessageId = replyToMessageId
        };

        _db.ChatMessages.Add(message);

        // Actualizar LastActivityAt
        var conversation = await _db.Conversations.FindAsync(conversationId);
        if (conversation != null)
        {
            conversation.LastActivityAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();

        // Recargar mensaje con todos los datos
        var fullMessage = await _db.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.ReplyToMessage)
                .ThenInclude(r => r!.Sender)
            .FirstAsync(m => m.Id == message.Id);

        // NOTIFICACIÓN EN TIEMPO REAL
        await NotifyNewMessage(fullMessage);

        _logger.LogInformation("📨 Mensaje enviado: {MsgId} en conversación {ConvId} por {SenderId}",
            message.Id, conversationId, senderId);

        return fullMessage;
    }

    public async Task<ChatMessage> SendFileMessageAsync(
        Guid senderId,
        Guid conversationId,
        MessageContentType contentType,
        string? content,
        string fileUrl,
        string fileName,
        string fileMimeType,
        long fileSizeBytes,
        Guid? replyToMessageId = null)
    {
        // Validar que sea miembro de la conversación
        var isMember = await _db.ConversationMembers.AnyAsync(m =>
            m.ConversationId == conversationId &&
            m.UserId == senderId &&
            m.IsActive);

        if (!isMember)
            throw new UnauthorizedAccessException("No perteneces a esta conversación");

        var message = new ChatMessage
        {
            ConversationId = conversationId,
            SenderId = senderId,
            ContentType = contentType,
            Content = content ?? $"📎 {fileName}",
            FileUrl = fileUrl,
            FileName = fileName,
            FileMimeType = fileMimeType,
            FileSizeBytes = fileSizeBytes,
            SentAt = DateTimeOffset.UtcNow,
            Status = MessageStatus.Sent,
            ReplyToMessageId = replyToMessageId
        };

        _db.ChatMessages.Add(message);

        // Actualizar LastActivityAt
        var conversation = await _db.Conversations.FindAsync(conversationId);
        if (conversation != null)
        {
            conversation.LastActivityAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();

        // Recargar mensaje con todos los datos
        var fullMessage = await _db.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.ReplyToMessage)
                .ThenInclude(r => r!.Sender)
            .FirstAsync(m => m.Id == message.Id);

        // NOTIFICACIÓN EN TIEMPO REAL
        await NotifyNewMessage(fullMessage);

        _logger.LogInformation("📎 Archivo enviado: {FileName} en conversación {ConvId} por {SenderId}",
            fileName, conversationId, senderId);

        return fullMessage;
    }

    public async Task<bool> EditMessageAsync(Guid messageId, Guid userId, string newContent)
    {
        var message = await _db.ChatMessages.FindAsync(messageId);
        if (message == null || message.SenderId != userId) return false;

        message.Content = newContent;
        message.IsEdited = true;
        message.EditedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteMessageAsync(Guid messageId, Guid userId)
    {
        var message = await _db.ChatMessages.FindAsync(messageId);
        if (message == null || message.SenderId != userId) return false;

        message.IsDeleted = true;
        message.DeletedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    // ==================== CONFIRMACIONES DE LECTURA ====================

    public async Task<bool> MarkMessageAsDeliveredAsync(Guid messageId, Guid userId)
    {
        var message = await _db.ChatMessages.FindAsync(messageId);
        if (message == null) return false;

        // No marcar propios mensajes
        if (message.SenderId == userId) return true;

        // Verificar si ya existe
        var existing = await _db.MessageDeliveryStatuses
            .FirstOrDefaultAsync(ds => ds.MessageId == messageId && ds.DeliveredToUserId == userId);

        if (existing != null) return true;

        // Crear nuevo estado
        var deliveryStatus = new MessageDeliveryStatus
        {
            MessageId = messageId,
            DeliveredToUserId = userId,
            DeliveredAt = DateTimeOffset.UtcNow
        };

        _db.MessageDeliveryStatuses.Add(deliveryStatus);

        // Actualizar mensaje si es la primera entrega
        if (message.DeliveredAt == null)
        {
            message.DeliveredAt = DateTimeOffset.UtcNow;
            message.Status = MessageStatus.Delivered;
        }

        await _db.SaveChangesAsync();

        // NOTIFICAR AL SENDER
        await _hubContext.Clients.Group($"user_{message.SenderId}")
            .SendAsync("MessageDelivered", new
            {
                messageId,
                conversationId = message.ConversationId,
                deliveredToUserId = userId,
                deliveredAt = DateTimeOffset.UtcNow
            });

        _logger.LogDebug("✓ Mensaje {MsgId} marcado como entregado a {UserId}", messageId, userId);

        return true;
    }

    public async Task<bool> MarkMessageAsReadAsync(Guid messageId, Guid userId)
    {
        var message = await _db.ChatMessages.FindAsync(messageId);
        if (message == null) return false;

        // No marcar propios mensajes
        if (message.SenderId == userId) return true;

        // Verificar si ya está marcado como leído
        var existingRead = await _db.MessageReadStatuses
            .FirstOrDefaultAsync(rs => rs.MessageId == messageId && rs.ReadByUserId == userId);

        if (existingRead != null) return true;

        // Marcar como entregado también (si no lo está)
        await MarkMessageAsDeliveredAsync(messageId, userId);

        // Crear estado de lectura
        var readStatus = new MessageReadStatus
        {
            MessageId = messageId,
            ReadByUserId = userId,
            ReadAt = DateTimeOffset.UtcNow
        };

        _db.MessageReadStatuses.Add(readStatus);

        // Actualizar mensaje si es la primera lectura
        if (message.ReadAt == null)
        {
            message.ReadAt = DateTimeOffset.UtcNow;
            message.Status = MessageStatus.Read;
        }

        await _db.SaveChangesAsync();

        // NOTIFICAR AL SENDER
        await _hubContext.Clients.Group($"user_{message.SenderId}")
            .SendAsync("MessageRead", new
            {
                messageId,
                conversationId = message.ConversationId,
                readByUserId = userId,
                readAt = DateTimeOffset.UtcNow
            });

        _logger.LogDebug("✓✓ Mensaje {MsgId} marcado como leído por {UserId}", messageId, userId);

        return true;
    }

    public async Task<int> MarkAllMessagesAsReadAsync(Guid conversationId, Guid userId)
    {
        var member = await _db.ConversationMembers
            .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == userId && m.IsActive);

        if (member == null) return 0;

        var unreadMessages = await _db.ChatMessages
            .Where(m => m.ConversationId == conversationId)
            .Where(m => m.SenderId != userId)
            .Where(m => !m.ReadStatuses.Any(rs => rs.ReadByUserId == userId))
            .Where(m => !m.IsDeleted)
            .ToListAsync();

        foreach (var message in unreadMessages)
        {
            await MarkMessageAsReadAsync(message.Id, userId);
        }

        member.LastReadAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("✓✓ {Count} mensajes marcados como leídos en conversación {ConvId} por {UserId}",
            unreadMessages.Count, conversationId, userId);

        return unreadMessages.Count;
    }

    public async Task<int> GetUnreadMessageCountAsync(Guid conversationId, Guid userId)
    {
        var member = await _db.ConversationMembers
            .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == userId);

        if (member == null) return 0;

        var lastReadAt = member.LastReadAt ?? DateTimeOffset.MinValue;

        return await _db.ChatMessages
            .Where(m => m.ConversationId == conversationId)
            .Where(m => m.SenderId != userId)
            .Where(m => m.SentAt > lastReadAt)
            .Where(m => !m.IsDeleted)
            .CountAsync();
    }

    // ==================== HELPERS PRIVADOS ====================

    private async Task NotifyNewMessage(ChatMessage message)
    {
        // Obtener todos los miembros de la conversación
        var memberIds = await _db.ConversationMembers
            .Where(m => m.ConversationId == message.ConversationId && m.IsActive)
            .Select(m => m.UserId)
            .ToListAsync();

        var messageDto = new
        {
            id = message.Id,
            conversationId = message.ConversationId,
            senderId = message.SenderId,
            senderName = message.Sender?.NombreCompleto ?? "",
            contentType = message.ContentType.ToString(),
            content = message.Content,
            fileUrl = message.FileUrl,
            fileName = message.FileName,
            sentAt = message.SentAt,
            deliveredAt = message.DeliveredAt,
            readAt = message.ReadAt,
            status = message.Status.ToString(),
            isEdited = message.IsEdited,
            editedAt = message.EditedAt,
            replyToMessageId = message.ReplyToMessageId,
            replyToMessage = message.ReplyToMessage == null ? null : new
            {
                id = message.ReplyToMessage.Id,
                senderId = message.ReplyToMessage.SenderId,
                senderName = message.ReplyToMessage.Sender?.NombreCompleto ?? "",
                content = message.ReplyToMessage.Content
            }
        };

        // Enviar a TODOS los miembros usando sus grupos de usuario
        foreach (var memberId in memberIds)
        {
            await _hubContext.Clients.Group($"user_{memberId}")
                .SendAsync("ReceiveMessage", messageDto);
        }

        _logger.LogDebug("📤 Mensaje {MsgId} notificado a {Count} usuarios", message.Id, memberIds.Count);
    }
}
