using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TaskControlBackend.Data;
using TaskControlBackend.Helpers;
using TaskControlBackend.Hubs;
using TaskControlBackend.Models.Enums;
using TaskControlBackend.Services;
using TaskControlBackend.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// DbContext
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(config.GetConnectionString("DefaultConnection")));

// Services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmpresaService, EmpresaService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<ITareaService, TareaService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddControllers();

// SignalR with custom UserIdProvider for JWT authentication
builder.Services.AddSignalR().AddJsonProtocol();
builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy
            .SetIsOriginAllowed(origin => 
                origin.StartsWith("http://localhost") || 
                origin.StartsWith("https://localhost") ||
                origin == "https://taskcontrol.work")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// JWT
var key = Encoding.UTF8.GetBytes(config["JwtSettings:SecretKey"]!);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.RequireHttpsMetadata = false;
        opt.SaveToken = true;
        opt.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = config["JwtSettings:Issuer"],
            ValidAudience = config["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ClockSkew = TimeSpan.Zero
        };
        // Allow SignalR to read access_token from query for WebSockets
        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"].FirstOrDefault();
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/apphub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo{
        Title = "TaskControl API", Version = "v1",
        Description = "Autenticación con JWT y refresh revocable"
    });
    // JWT en Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme{
        Description = "Ingrese 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement{
        {
            new OpenApiSecurityScheme{ Reference = new OpenApiReference{
                Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, new string[] { }
        }
    });
});

var app = builder.Build();

// Swagger
app.UseSwagger();
app.UseSwaggerUI();

// Redirigir raíz a Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

// 🔹 Middlewares (ORDEN IMPORTANTE)
app.UseHttpsRedirection();

app.UseRouting();

// 👉 CORS DEBE IR ANTES DE AUTH
app.UseCors("DevCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map SignalR ChatHub
app.MapHub<ChatHub>("/chathub");

// ==================== CHAT ENDPOINTS ====================

app.MapGet("/api/chat/users/search", async (
    ClaimsPrincipal principal,
    IChatService chatService,
    string? q) =>
{
    var userId = ClaimsHelpers.GetUserIdOrThrow(principal);
    var searchTerm = q ?? string.Empty;

    var users = await chatService.SearchUsersAsync(userId, searchTerm);

    var usersDTO = users.Select(u => new
    {
        id = u.Id,
        nombre = u.NombreCompleto,
        email = u.Email,
        rol = u.Rol.ToString(),
        departamento = u.Departamento.ToString(),
        empresaId = u.EmpresaId,
        empresaNombre = u.Empresa?.Nombre
    }).ToList();

    return Results.Ok(new { success = true, data = usersDTO });
})
.RequireAuthorization()
.WithTags("Chat")
.WithDescription("Busca usuarios con los que el usuario actual puede chatear");

app.MapGet("/api/chat/conversations", async (
    ClaimsPrincipal principal,
    IChatService chatService) =>
{
    var userId = ClaimsHelpers.GetUserIdOrThrow(principal);

    var conversations = await chatService.GetUserConversationsAsync(userId);

    var conversationsDTO = conversations.Select(c => new
    {
        id = c.Id,
        type = c.Type.ToString(),
        name = c.Name,
        imageUrl = c.ImageUrl,
        createdById = c.CreatedById,
        createdAt = c.CreatedAt,
        lastActivityAt = c.LastActivityAt,
        members = c.Members.Select(m => new
        {
            userId = m.UserId,
            userName = m.User.NombreCompleto,
            role = m.Role.ToString(),
            joinedAt = m.JoinedAt,
            isMuted = m.IsMuted,
            lastReadAt = m.LastReadAt
        }).ToList(),
        lastMessage = c.Messages.OrderByDescending(m => m.SentAt).FirstOrDefault() is { } lastMsg
            ? new
            {
                id = lastMsg.Id,
                senderId = lastMsg.SenderId,
                senderName = lastMsg.Sender.NombreCompleto,
                contentType = lastMsg.ContentType.ToString(),
                content = lastMsg.Content,
                sentAt = lastMsg.SentAt
            }
            : null,
        unreadCount = 0 // TODO: Implement efficient unread count
    }).ToList();

    return Results.Ok(new { success = true, data = conversationsDTO });
})
.RequireAuthorization()
.WithTags("Chat")
.WithDescription("Obtiene todas las conversaciones del usuario actual");

app.MapGet("/api/chat/conversations/{conversationId}", async (
    Guid conversationId,
    ClaimsPrincipal principal,
    IChatService chatService) =>
{
    var userId = ClaimsHelpers.GetUserIdOrThrow(principal);

    var conversation = await chatService.GetConversationByIdAsync(conversationId, userId);

    if (conversation == null)
        return Results.NotFound(new { success = false, message = "Conversación no encontrada" });

    var conversationDTO = new
    {
        id = conversation.Id,
        type = conversation.Type.ToString(),
        name = conversation.Name,
        imageUrl = conversation.ImageUrl,
        createdById = conversation.CreatedById,
        createdAt = conversation.CreatedAt,
        lastActivityAt = conversation.LastActivityAt,
        members = conversation.Members.Select(m => new
        {
            userId = m.UserId,
            userName = m.User.NombreCompleto,
            role = m.Role.ToString(),
            joinedAt = m.JoinedAt,
            isMuted = m.IsMuted,
            lastReadAt = m.LastReadAt
        }).ToList()
    };

    return Results.Ok(new { success = true, data = conversationDTO });
})
.RequireAuthorization()
.WithTags("Chat")
.WithDescription("Obtiene una conversación específica por ID");

app.MapPost("/api/chat/conversations/direct", async (
    ClaimsPrincipal principal,
    IChatService chatService,
    TaskControlBackend.DTOs.Chat.CreateDirectConversationRequest request) =>
{
    var userId = ClaimsHelpers.GetUserIdOrThrow(principal);

    var conversation = await chatService.GetOrCreateDirectConversationAsync(userId, request.OtherUserId);

    var conversationDTO = new
    {
        id = conversation.Id,
        type = conversation.Type.ToString(),
        createdById = conversation.CreatedById,
        createdAt = conversation.CreatedAt
    };

    return Results.Ok(new { success = true, data = conversationDTO, message = "Conversación directa creada o recuperada" });
})
.RequireAuthorization()
.WithTags("Chat")
.WithDescription("Crea o retorna una conversación directa existente");

app.MapPost("/api/chat/conversations/group", async (
    ClaimsPrincipal principal,
    IChatService chatService,
    TaskControlBackend.DTOs.Chat.CreateGroupConversationRequest request) =>
{
    var userId = ClaimsHelpers.GetUserIdOrThrow(principal);

    var conversation = await chatService.CreateGroupConversationAsync(
        userId,
        request.GroupName,
        request.MemberIds,
        request.ImageUrl);

    var conversationDTO = new
    {
        id = conversation.Id,
        type = conversation.Type.ToString(),
        name = conversation.Name,
        imageUrl = conversation.ImageUrl,
        createdById = conversation.CreatedById,
        createdAt = conversation.CreatedAt
    };

    return Results.Ok(new { success = true, data = conversationDTO, message = "Conversación grupal creada" });
})
.RequireAuthorization()
.WithTags("Chat")
.WithDescription("Crea una nueva conversación grupal");

app.MapPut("/api/chat/conversations/{conversationId}", async (
    Guid conversationId,
    ClaimsPrincipal principal,
    IChatService chatService,
    TaskControlBackend.DTOs.Chat.UpdateConversationRequest request) =>
{
    var userId = ClaimsHelpers.GetUserIdOrThrow(principal);

    var updated = await chatService.UpdateConversationAsync(conversationId, userId, request.Name, request.ImageUrl);

    if (!updated)
        return Results.BadRequest(new { success = false, message = "No tienes permisos para actualizar esta conversación" });

    return Results.Ok(new { success = true, message = "Conversación actualizada" });
})
.RequireAuthorization()
.WithTags("Chat")
.WithDescription("Actualiza nombre o imagen de una conversación grupal");

app.MapPost("/api/chat/conversations/{conversationId}/members", async (
    Guid conversationId,
    ClaimsPrincipal principal,
    IChatService chatService,
    TaskControlBackend.DTOs.Chat.AddMembersRequest request) =>
{
    var userId = ClaimsHelpers.GetUserIdOrThrow(principal);

    var added = await chatService.AddMembersToGroupAsync(conversationId, userId, request.MemberIds);

    if (!added)
        return Results.BadRequest(new { success = false, message = "No tienes permisos para agregar miembros" });

    return Results.Ok(new { success = true, message = "Miembros agregados" });
})
.RequireAuthorization()
.WithTags("Chat")
.WithDescription("Agrega miembros a una conversación grupal");

app.MapDelete("/api/chat/conversations/{conversationId}/members/{memberId}", async (
    Guid conversationId,
    Guid memberId,
    ClaimsPrincipal principal,
    IChatService chatService) =>
{
    var userId = ClaimsHelpers.GetUserIdOrThrow(principal);

    var removed = await chatService.RemoveMemberFromGroupAsync(conversationId, userId, memberId);

    if (!removed)
        return Results.BadRequest(new { success = false, message = "No tienes permisos para remover este miembro" });

    return Results.Ok(new { success = true, message = "Miembro removido" });
})
.RequireAuthorization()
.WithTags("Chat")
.WithDescription("Remueve un miembro de una conversación grupal");

app.MapGet("/api/chat/conversations/{conversationId}/messages", async (
    Guid conversationId,
    ClaimsPrincipal principal,
    IChatService chatService,
    int skip = 0,
    int take = 50) =>
{
    var userId = ClaimsHelpers.GetUserIdOrThrow(principal);

    var messages = await chatService.GetConversationMessagesAsync(conversationId, userId, skip, take);

    var messagesDTO = messages.Select(m => new
    {
        id = m.Id,
        conversationId = m.ConversationId,
        senderId = m.SenderId,
        senderName = m.Sender.NombreCompleto,
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
        replyToMessageId = m.ReplyToMessageId,
        replyToMessage = m.ReplyToMessage != null
            ? new
            {
                id = m.ReplyToMessage.Id,
                senderId = m.ReplyToMessage.SenderId,
                senderName = m.ReplyToMessage.Sender.NombreCompleto,
                contentType = m.ReplyToMessage.ContentType.ToString(),
                content = m.ReplyToMessage.Content
            }
            : null,
        readReceipts = m.ReadStatuses.Select(rs => new
        {
            userId = rs.ReadByUserId,
            userName = rs.ReadByUser.NombreCompleto,
            readAt = rs.ReadAt
        }).ToList(),
        deliveryReceipts = m.DeliveryStatuses.Select(ds => new
        {
            userId = ds.DeliveredToUserId,
            userName = ds.DeliveredToUser.NombreCompleto,
            deliveredAt = ds.DeliveredAt
        }).ToList()
    }).ToList();

    return Results.Ok(new { success = true, data = messagesDTO });
})
.RequireAuthorization()
.WithTags("Chat")
.WithDescription("Obtiene mensajes de una conversación con paginación");

app.MapPost("/api/chat/conversations/{conversationId}/messages", async (
    Guid conversationId,
    ClaimsPrincipal principal,
    IChatService chatService,
    TaskControlBackend.DTOs.Chat.SendTextMessageRequest request) =>
{
    var userId = ClaimsHelpers.GetUserIdOrThrow(principal);

    var message = await chatService.SendTextMessageAsync(
        userId,
        conversationId,
        request.Content,
        request.ReplyToMessageId);

    var messageDTO = new
    {
        id = message.Id,
        conversationId = message.ConversationId,
        senderId = message.SenderId,
        contentType = message.ContentType.ToString(),
        content = message.Content,
        sentAt = message.SentAt,
        status = message.Status.ToString(),
        replyToMessageId = message.ReplyToMessageId
    };

    return Results.Ok(new { success = true, data = messageDTO, message = "Mensaje enviado" });
})
.RequireAuthorization()
.WithTags("Chat")
.WithDescription("Envía un mensaje de texto a una conversación");

app.MapPut("/api/chat/messages/{messageId}", async (
    Guid messageId,
    ClaimsPrincipal principal,
    IChatService chatService,
    TaskControlBackend.DTOs.Chat.EditMessageRequest request) =>
{
    var userId = ClaimsHelpers.GetUserIdOrThrow(principal);

    var edited = await chatService.EditMessageAsync(messageId, userId, request.NewContent);

    if (!edited)
        return Results.BadRequest(new { success = false, message = "No puedes editar este mensaje" });

    return Results.Ok(new { success = true, message = "Mensaje editado" });
})
.RequireAuthorization()
.WithTags("Chat")
.WithDescription("Edita un mensaje existente");

app.MapDelete("/api/chat/messages/{messageId}", async (
    Guid messageId,
    ClaimsPrincipal principal,
    IChatService chatService) =>
{
    var userId = ClaimsHelpers.GetUserIdOrThrow(principal);

    var deleted = await chatService.DeleteMessageAsync(messageId, userId);

    if (!deleted)
        return Results.BadRequest(new { success = false, message = "No puedes eliminar este mensaje" });

    return Results.Ok(new { success = true, message = "Mensaje eliminado" });
})
.RequireAuthorization()
.WithTags("Chat")
.WithDescription("Elimina un mensaje (soft delete)");

app.MapPut("/api/chat/messages/{messageId}/delivered", async (
    Guid messageId,
    ClaimsPrincipal principal,
    IChatService chatService) =>
{
    var userId = ClaimsHelpers.GetUserIdOrThrow(principal);

    var marked = await chatService.MarkMessageAsDeliveredAsync(messageId, userId);

    if (!marked)
        return Results.BadRequest(new { success = false, message = "No se pudo marcar como entregado" });

    return Results.Ok(new { success = true, message = "Mensaje marcado como entregado" });
})
.RequireAuthorization()
.WithTags("Chat")
.WithDescription("Marca un mensaje como entregado (palomita simple ✓)");

app.MapPut("/api/chat/messages/{messageId}/read", async (
    Guid messageId,
    ClaimsPrincipal principal,
    IChatService chatService) =>
{
    var userId = ClaimsHelpers.GetUserIdOrThrow(principal);

    var marked = await chatService.MarkMessageAsReadAsync(messageId, userId);

    if (!marked)
        return Results.BadRequest(new { success = false, message = "No se pudo marcar como leído" });

    return Results.Ok(new { success = true, message = "Mensaje marcado como leído" });
})
.RequireAuthorization()
.WithTags("Chat")
.WithDescription("Marca un mensaje como leído (palomita doble ✓✓)");

app.MapPut("/api/chat/conversations/{conversationId}/mark-all-read", async (
    Guid conversationId,
    ClaimsPrincipal principal,
    IChatService chatService) =>
{
    var userId = ClaimsHelpers.GetUserIdOrThrow(principal);

    var count = await chatService.MarkAllMessagesAsReadAsync(conversationId, userId);

    return Results.Ok(new { success = true, messagesMarked = count, message = $"{count} mensajes marcados como leídos" });
})
.RequireAuthorization()
.WithTags("Chat")
.WithDescription("Marca todos los mensajes de una conversación como leídos");

app.MapGet("/api/chat/conversations/{conversationId}/unread-count", async (
    Guid conversationId,
    ClaimsPrincipal principal,
    IChatService chatService) =>
{
    var userId = ClaimsHelpers.GetUserIdOrThrow(principal);

    var count = await chatService.GetUnreadMessageCountAsync(conversationId, userId);

    return Results.Ok(new { success = true, unreadCount = count });
})
.RequireAuthorization()
.WithTags("Chat")
.WithDescription("Obtiene el número de mensajes no leídos en una conversación");

app.Run();
