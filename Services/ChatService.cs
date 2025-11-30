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
/// Servicio de chat con soporte completo para mensajes, archivos y confirmaciones de lectura
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

    // ==================== USER SEARCH ====================

    public async Task<List<Usuario>> SearchUsersAsync(Guid currentUserId, string searchTerm)
    {
        var currentUser = await _db.Usuarios
            .FirstOrDefaultAsync(u => u.Id == currentUserId);

        if (currentUser == null)
            return new List<Usuario>();

        // Aplicar reglas de búsqueda según rol
        IQueryable<Usuario> query = _db.Usuarios.Where(u => u.Id != currentUserId);

        if (currentUser.Rol == RolUsuario.AdminGeneral)
        {
            // AdminGeneral solo puede chatear con AdminEmpresa
            query = query.Where(u => u.Rol == RolUsuario.AdminEmpresa);
        }
        else
        {
            // Otros roles solo pueden buscar dentro de su empresa
            query = query.Where(u => u.EmpresaId == currentUser.EmpresaId);
        }

        // Aplicar filtro de búsqueda
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchLower = searchTerm.ToLower();
            query = query.Where(u =>
                u.NombreCompleto.ToLower().Contains(searchLower) ||
                u.Email.ToLower().Contains(searchLower));
        }

        return await query
            .OrderBy(u => u.NombreCompleto)
            .Take(20)
            .ToListAsync();
    }

    // ==================== CONVERSATIONS ====================

    public async Task<List<Conversation>> GetUserConversationsAsync(Guid userId)
    {
        return await _db.Conversations
            .Include(c => c.Members.Where(m => m.IsActive))
                .ThenInclude(m => m.User)
            .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1)) // Último mensaje
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
        // Buscar conversación directa existente entre estos 2 usuarios
        var existingConversation = await _db.Conversations
            .Include(c => c.Members)
            .Where(c => c.Type == ConversationType.Direct)
            .Where(c => c.Members.Count == 2)
            .Where(c => c.Members.Any(m => m.UserId == userId1 && m.IsActive))
            .Where(c => c.Members.Any(m => m.UserId == userId2 && m.IsActive))
            .FirstOrDefaultAsync();

        if (existingConversation != null)
            return existingConversation;

        // Crear nueva conversación directa
        var conversation = new Conversation
        {
            Type = ConversationType.Direct,
            CreatedById = userId1,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        _db.Conversations.Add(conversation);

        // Agregar ambos usuarios como miembros
        var member1 = new ConversationMember
        {
            ConversationId = conversation.Id,
            UserId = userId1,
            Role = ConversationMemberRole.Member,
            JoinedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        var member2 = new ConversationMember
        {
            ConversationId = conversation.Id,
            UserId = userId2,
            Role = ConversationMemberRole.Member,
            JoinedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        _db.ConversationMembers.AddRange(member1, member2);
        await _db.SaveChangesAsync();

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

        // Agregar creador como Admin
        var creatorMember = new ConversationMember
        {
            ConversationId = conversation.Id,
            UserId = creatorId,
            Role = ConversationMemberRole.Admin,
            JoinedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        _db.ConversationMembers.Add(creatorMember);

        // Agregar otros miembros
        foreach (var memberId in memberIds.Where(id => id != creatorId))
        {
            var member = new ConversationMember
            {
                ConversationId = conversation.Id,
                UserId = memberId,
                Role = ConversationMemberRole.Member,
                JoinedAt = DateTimeOffset.UtcNow,
                IsActive = true
            };

            _db.ConversationMembers.Add(member);
        }

        await _db.SaveChangesAsync();

        return conversation;
    }

    public async Task<bool> UpdateConversationAsync(Guid conversationId, Guid userId, string? newName, string? newImageUrl)
    {
        var member = await _db.ConversationMembers
            .Include(m => m.Conversation)
            .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == userId && m.IsActive);

        if (member == null || member.Role != ConversationMemberRole.Admin)
            return false;

        if (newName != null)
            member.Conversation.Name = newName;

        if (newImageUrl != null)
            member.Conversation.ImageUrl = newImageUrl;

        member.Conversation.LastActivityAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        // Notificar a todos los miembros
        var memberUserIds = await _db.ConversationMembers
            .Where(m => m.ConversationId == conversationId && m.IsActive)
            .Select(m => m.UserId.ToString())
            .ToListAsync();

        await _hubContext.Clients.Users(memberUserIds).SendAsync("chat:conversation_updated", new
        {
            conversationId,
            name = newName,
            imageUrl = newImageUrl,
            updatedAt = DateTimeOffset.UtcNow
        });

        return true;
    }

    public async Task<bool> AddMembersToGroupAsync(Guid conversationId, Guid requesterId, List<Guid> newMemberIds)
    {
        var requesterMember = await _db.ConversationMembers
            .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == requesterId && m.IsActive);

        if (requesterMember == null || requesterMember.Role != ConversationMemberRole.Admin)
            return false;

        foreach (var newMemberId in newMemberIds)
        {
            var existingMember = await _db.ConversationMembers
                .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == newMemberId);

            if (existingMember == null)
            {
                var newMember = new ConversationMember
                {
                    ConversationId = conversationId,
                    UserId = newMemberId,
                    Role = ConversationMemberRole.Member,
                    JoinedAt = DateTimeOffset.UtcNow,
                    IsActive = true
                };

                _db.ConversationMembers.Add(newMember);
            }
            else if (!existingMember.IsActive)
            {
                existingMember.IsActive = true;
                existingMember.JoinedAt = DateTimeOffset.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveMemberFromGroupAsync(Guid conversationId, Guid requesterId, Guid memberIdToRemove)
    {
        var requesterMember = await _db.ConversationMembers
            .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == requesterId && m.IsActive);

        if (requesterMember == null)
            return false;

        // Admin puede remover a cualquiera, usuario solo puede removerse a sí mismo
        if (requesterMember.Role != ConversationMemberRole.Admin && requesterId != memberIdToRemove)
            return false;

        var memberToRemove = await _db.ConversationMembers
            .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == memberIdToRemove);

        if (memberToRemove == null)
            return false;

        memberToRemove.IsActive = false;

        await _db.SaveChangesAsync();
        return true;
    }

    // ==================== MESSAGES ====================

    public async Task<List<ChatMessage>> GetConversationMessagesAsync(Guid conversationId, Guid userId, int skip = 0, int take = 50)
    {
        // Verificar que el usuario es miembro de la conversación
        var isMember = await IsUserActiveMemberAsync(conversationId, userId);

        if (!isMember)
            return new List<ChatMessage>();

        return await _db.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.ReplyToMessage)
                .ThenInclude(m => m!.Sender)
            .Include(m => m.DeliveryStatuses)
                .ThenInclude(ds => ds.DeliveredToUser)
            .Include(m => m.ReadStatuses)
                .ThenInclude(rs => rs.ReadByUser)
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
            .OrderByDescending(m => m.SentAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<ChatMessage> SendTextMessageAsync(Guid senderId, Guid conversationId, string content, Guid? replyToMessageId = null)
    {
        if (!await IsUserActiveMemberAsync(conversationId, senderId))
            throw new UnauthorizedAccessException("No perteneces a esta conversaci�n");

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

        // Actualizar LastActivityAt de la conversación
        var conversation = await _db.Conversations.FindAsync(conversationId);
        if (conversation != null)
        {
            conversation.LastActivityAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();

        // NOTIFICACIÓN EN TIEMPO REAL: Notificar a TODOS los usuarios (no solo al grupo)
        // Esto permite que reciban notificaciones incluso si están viendo otro chat
        var allMemberUserIds = await _db.ConversationMembers
            .Where(m => m.ConversationId == conversationId && m.IsActive)
            .Select(m => m.UserId.ToString())
            .ToListAsync();

        // Cargar información completa del mensaje para el evento
        var messageWithSender = await _db.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.ReplyToMessage)
            .FirstAsync(m => m.Id == message.Id);

        await _hubContext.Clients.Users(allMemberUserIds).SendAsync("chat:message", new
        {
            messageId = message.Id,
            conversationId = message.ConversationId,
            senderId = message.SenderId,
            senderName = messageWithSender.Sender.NombreCompleto,
            contentType = message.ContentType.ToString(),
            content = message.Content,
            sentAt = message.SentAt,
            replyToMessageId = message.ReplyToMessageId
        });

        return message;
    }

    public Task<ChatMessage> SendFileMessageAsync(
        Guid senderId,
        Guid conversationId,
        MessageContentType contentType,
        string? content,
        Stream fileData,
        string fileName,
        string fileMimeType,
        Guid? replyToMessageId = null)
    {
        // TODO: IMPLEMENTAR UPLOAD A BLOB STORAGE
        // 1. Validar tipo de archivo y tamaño
        // 2. Generar nombre único para el archivo
        // 3. Subir a Azure Blob Storage
        // 4. Obtener URL del archivo subido

        // POR AHORA: Arquitectura lista, pero upload no implementado
        throw new NotImplementedException(
            "File upload to Blob Storage not implemented yet. " +
            "Architecture is ready - implement Azure Blob Storage upload logic here.");

        // CÓDIGO ESPERADO (después de implementar Blob Storage):
        /*
        string fileUrl = await _blobStorageService.UploadFileAsync(fileData, fileName, fileMimeType);
        long fileSizeBytes = fileData.Length;

        var message = new ChatMessage
        {
            ConversationId = conversationId,
            SenderId = senderId,
            ContentType = contentType,
            Content = content ?? string.Empty,
            FileUrl = fileUrl,
            FileName = fileName,
            FileSizeBytes = fileSizeBytes,
            FileMimeType = fileMimeType,
            SentAt = DateTimeOffset.UtcNow,
            Status = MessageStatus.Sent,
            ReplyToMessageId = replyToMessageId
        };

        _db.ChatMessages.Add(message);

        // ... resto del código igual que SendTextMessageAsync
        */
    }

    public async Task<bool> EditMessageAsync(Guid messageId, Guid userId, string newContent)
    {
        var message = await _db.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == userId);

        if (message == null)
            return false;

        if (message.SenderId == userId)
            return false;

        if (!await IsUserActiveMemberAsync(message.ConversationId, userId))
            return false;

        message.Content = newContent;
        message.IsEdited = true;
        message.EditedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteMessageAsync(Guid messageId, Guid userId)
    {
        var message = await _db.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == userId);

        if (message == null)
            return false;

        if (message.SenderId == userId)
            return false;

        if (!await IsUserActiveMemberAsync(message.ConversationId, userId))
            return false;

        message.IsDeleted = true;
        message.DeletedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    // ==================== DELIVERY & READ RECEIPTS ====================

    public async Task<bool> MarkMessageAsDeliveredAsync(Guid messageId, Guid userId)
    {
        var message = await _db.ChatMessages.FindAsync(messageId);
        if (message == null)
            return false;

        // Verificar si ya existe MessageDeliveryStatus
        var existingStatus = await _db.MessageDeliveryStatuses
            .FirstOrDefaultAsync(ds => ds.MessageId == messageId && ds.DeliveredToUserId == userId);

        if (existingStatus != null)
            return true; // Ya marcado como entregado

        // Crear nuevo MessageDeliveryStatus
        var deliveryStatus = new MessageDeliveryStatus
        {
            MessageId = messageId,
            DeliveredToUserId = userId,
            DeliveredAt = DateTimeOffset.UtcNow
        };

        _db.MessageDeliveryStatuses.Add(deliveryStatus);

        // Si es la primera entrega, actualizar el mensaje
        if (message.DeliveredAt == null)
        {
            message.DeliveredAt = DateTimeOffset.UtcNow;
            message.Status = MessageStatus.Delivered;
        }

        await _db.SaveChangesAsync();

        // Notificar al sender
        await _hubContext.Clients.User(message.SenderId.ToString()).SendAsync("chat:message_delivered", new
        {
            messageId,
            deliveredToUserId = userId,
            deliveredAt = DateTimeOffset.UtcNow
        });

        return true;
    }

    public async Task<bool> MarkMessageAsReadAsync(Guid messageId, Guid userId)
    {
        var message = await _db.ChatMessages.FindAsync(messageId);
        if (message == null)
            return false;

        // Verificar si ya existe MessageReadStatus
        var existingReadStatus = await _db.MessageReadStatuses
            .FirstOrDefaultAsync(rs => rs.MessageId == messageId && rs.ReadByUserId == userId);

        if (existingReadStatus != null)
            return true; // Ya marcado como leído

        // Marcar como entregado también (si no lo está)
        await MarkMessageAsDeliveredAsync(messageId, userId);

        // Crear nuevo MessageReadStatus
        var readStatus = new MessageReadStatus
        {
            MessageId = messageId,
            ReadByUserId = userId,
            ReadAt = DateTimeOffset.UtcNow
        };

        _db.MessageReadStatuses.Add(readStatus);

        // Si es la primera lectura, actualizar el mensaje
        if (message.ReadAt == null)
        {
            message.ReadAt = DateTimeOffset.UtcNow;
            message.Status = MessageStatus.Read;
        }

        await _db.SaveChangesAsync();

        // Notificar al sender
        await _hubContext.Clients.User(message.SenderId.ToString()).SendAsync("chat:message_read", new
        {
            messageId,
            readByUserId = userId,
            readAt = DateTimeOffset.UtcNow
        });

        return true;
    }

    public async Task<int> MarkAllMessagesAsReadAsync(Guid conversationId, Guid userId)
    {
        var member = await _db.ConversationMembers
            .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == userId && m.IsActive);

        if (member == null)
            return 0;

        // Obtener todos los mensajes no leídos por este usuario
        var unreadMessages = await _db.ChatMessages
            .Where(m => m.ConversationId == conversationId)
            .Where(m => m.SenderId != userId) // No marcar propios mensajes
            .Where(m => !m.ReadStatuses.Any(rs => rs.ReadByUserId == userId))
            .Where(m => !m.IsDeleted)
            .ToListAsync();

        foreach (var message in unreadMessages)
        {
            await MarkMessageAsReadAsync(message.Id, userId);
        }

        member.LastReadAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return unreadMessages.Count;
    }

    public async Task<int> GetUnreadMessageCountAsync(Guid conversationId, Guid userId)
    {
        var member = await _db.ConversationMembers
            .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == userId);

        if (member == null)
            return 0;

        var lastReadAt = member.LastReadAt ?? DateTimeOffset.MinValue;

        return await _db.ChatMessages
            .Where(m => m.ConversationId == conversationId)
            .Where(m => m.SenderId != userId)
            .Where(m => m.SentAt > lastReadAt)
            .Where(m => !m.IsDeleted)
            .CountAsync();
    }

    private Task<bool> IsUserActiveMemberAsync(Guid conversationId, Guid userId)
    {
        return _db.ConversationMembers.AnyAsync(m =>
            m.ConversationId == conversationId &&
            m.UserId == userId &&
            m.IsActive);
    }
}
