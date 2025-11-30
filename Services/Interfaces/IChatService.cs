using TaskControlBackend.Models;
using TaskControlBackend.Models.Chat;

namespace TaskControlBackend.Services.Interfaces;

/// <summary>
/// Interfaz del servicio de chat con soporte completo para:
/// - Chats 1:1 y grupales
/// - Mensajes con archivos adjuntos
/// - Read receipts (confirmaciones de lectura individuales)
/// - Delivery receipts (confirmaciones de entrega individuales)
/// </summary>
public interface IChatService
{
    // ==================== USER SEARCH ====================

    /// <summary>
    /// Busca usuarios con los que el usuario actual puede chatear
    /// Aplica reglas de rol:
    /// - AdminGeneral: solo puede buscar AdminEmpresa
    /// - AdminEmpresa/Usuario/ManagerDepartamento: puede buscar dentro de su empresa
    /// </summary>
    Task<List<Usuario>> SearchUsersAsync(Guid currentUserId, string searchTerm);

    // ==================== CONVERSATIONS ====================

    /// <summary>
    /// Obtiene todas las conversaciones del usuario actual
    /// Ordenadas por última actividad (LastActivityAt DESC)
    /// Incluye información de último mensaje y contador de no leídos
    /// </summary>
    Task<List<Conversation>> GetUserConversationsAsync(Guid userId);

    /// <summary>
    /// Obtiene una conversación específica por ID
    /// Verifica que el usuario sea miembro de la conversación
    /// </summary>
    Task<Conversation?> GetConversationByIdAsync(Guid conversationId, Guid userId);

    /// <summary>
    /// Crea o retorna una conversación directa (1:1) existente
    /// Si ya existe una conversación entre estos 2 usuarios, la retorna
    /// Si no existe, crea una nueva
    /// </summary>
    Task<Conversation> GetOrCreateDirectConversationAsync(Guid userId1, Guid userId2);

    /// <summary>
    /// Crea una nueva conversación grupal
    /// El creador automáticamente se convierte en Admin del grupo
    /// </summary>
    Task<Conversation> CreateGroupConversationAsync(Guid creatorId, string groupName, List<Guid> memberIds, string? imageUrl = null);

    /// <summary>
    /// Actualiza información de una conversación grupal (nombre, imagen)
    /// Solo Admins del grupo pueden actualizar
    /// </summary>
    Task<bool> UpdateConversationAsync(Guid conversationId, Guid userId, string? newName, string? newImageUrl);

    /// <summary>
    /// Agrega miembros a una conversación grupal
    /// Solo Admins del grupo pueden agregar miembros
    /// </summary>
    Task<bool> AddMembersToGroupAsync(Guid conversationId, Guid requesterId, List<Guid> newMemberIds);

    /// <summary>
    /// Remueve un miembro de una conversación grupal
    /// Admins pueden remover cualquiera, usuarios solo pueden removerse a sí mismos
    /// </summary>
    Task<bool> RemoveMemberFromGroupAsync(Guid conversationId, Guid requesterId, Guid memberIdToRemove);

    // ==================== MESSAGES ====================

    /// <summary>
    /// Obtiene mensajes de una conversación con paginación
    /// Retorna mensajes ordenados por SentAt DESC (más reciente primero)
    /// Excluye mensajes eliminados (IsDeleted = true)
    /// </summary>
    Task<List<ChatMessage>> GetConversationMessagesAsync(Guid conversationId, Guid userId, int skip = 0, int take = 50);

    /// <summary>
    /// Envía un mensaje de texto simple
    /// Crea automáticamente MessageDeliveryStatus para todos los miembros excepto el sender
    /// Actualiza Conversation.LastActivityAt
    /// </summary>
    Task<ChatMessage> SendTextMessageAsync(Guid senderId, Guid conversationId, string content, Guid? replyToMessageId = null);

    /// <summary>
    /// Envía un mensaje con archivo adjunto (imagen, documento, audio, video)
    ///
    /// ARQUITECTURA COMPLETA - IMPLEMENTACIÓN MARCADA COMO TODO:
    /// - contentType: MessageContentType (Image, Document, Audio, Video)
    /// - content: Caption o descripción del archivo (opcional)
    /// - fileData: Stream del archivo (TODO: implementar upload a Blob Storage)
    /// - fileName: Nombre original del archivo
    /// - fileMimeType: MIME type (ej: "image/png", "application/pdf")
    ///
    /// TODO IMPLEMENTAR:
    /// 1. Validar tipo y tamaño de archivo
    /// 2. Subir archivo a Azure Blob Storage
    /// 3. Generar URL del archivo en Blob Storage
    /// 4. Crear ChatMessage con FileUrl, FileName, FileSizeBytes, FileMimeType
    /// 5. Crear MessageDeliveryStatus para todos los miembros
    /// </summary>
    Task<ChatMessage> SendFileMessageAsync(
        Guid senderId,
        Guid conversationId,
        MessageContentType contentType,
        string? content,
        Stream fileData,
        string fileName,
        string fileMimeType,
        Guid? replyToMessageId = null);

    /// <summary>
    /// Edita el contenido de un mensaje existente
    /// Solo el sender puede editar su mensaje
    /// Marca IsEdited = true y actualiza EditedAt
    /// </summary>
    Task<bool> EditMessageAsync(Guid messageId, Guid userId, string newContent);

    /// <summary>
    /// Elimina un mensaje (soft delete)
    /// Solo el sender puede eliminar su mensaje
    /// Marca IsDeleted = true y actualiza DeletedAt
    /// </summary>
    Task<bool> DeleteMessageAsync(Guid messageId, Guid userId);

    // ==================== DELIVERY & READ RECEIPTS ====================

    /// <summary>
    /// Marca un mensaje como ENTREGADO para un usuario específico (palomita simple ✓)
    /// Crea MessageDeliveryStatus si no existe
    /// Si es la primera entrega, actualiza ChatMessage.DeliveredAt y Status = Delivered
    /// </summary>
    Task<bool> MarkMessageAsDeliveredAsync(Guid messageId, Guid userId);

    /// <summary>
    /// Marca un mensaje como LEÍDO para un usuario específico (palomita doble ✓✓)
    /// Crea MessageReadStatus si no existe
    /// Si es la primera lectura, actualiza ChatMessage.ReadAt y Status = Read
    /// Automáticamente marca como entregado también
    /// </summary>
    Task<bool> MarkMessageAsReadAsync(Guid messageId, Guid userId);

    /// <summary>
    /// Marca TODOS los mensajes de una conversación como leídos para un usuario
    /// Eficiente para cuando el usuario abre un chat
    /// Actualiza ConversationMember.LastReadAt
    /// </summary>
    Task<int> MarkAllMessagesAsReadAsync(Guid conversationId, Guid userId);

    /// <summary>
    /// Cuenta mensajes no leídos en una conversación para un usuario
    /// Usa ConversationMember.LastReadAt para eficiencia
    /// </summary>
    Task<int> GetUnreadMessageCountAsync(Guid conversationId, Guid userId);
}
