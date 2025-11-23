using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TaskControlBackend.Data;
using TaskControlBackend.DTOs.Chat;
using TaskControlBackend.Hubs;
using TaskControlBackend.Models.Chat;
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
builder.Services.AddControllers();

// SignalR
builder.Services.AddSignalR().AddJsonProtocol();

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

// ==================== CHAT ENDPOINTS ====================

// Users: search
app.MapGet("/api/users/search", async (string? q, int take, AppDbContext db) =>
{
    var term = (q ?? string.Empty).Trim().ToLowerInvariant();
    take = Math.Clamp(take == 0 ? 20 : take, 1, 50);
    var users = await db.Usuarios
        .Where(u => term == string.Empty || u.Email.ToLower().Contains(term) || u.NombreCompleto.ToLower().Contains(term))
        .OrderBy(u => u.NombreCompleto)
        .Take(take)
        .Select(u => new { u.Id, u.NombreCompleto, u.Email })
        .ToListAsync();
    return Results.Ok(users);
}).RequireAuthorization().WithTags("Chat").WithOpenApi();

// Chats: list my chats with last message and members
app.MapGet("/api/chats", async (ClaimsPrincipal principal, AppDbContext db) =>
{
    var meId = ClaimsHelpers.GetUserId(principal);
    if (meId == null) return Results.Unauthorized();
    var meGuid = meId.Value;

    var raw = await db.Chats
        .Where(c => c.Members.Any(m => m.UserId == meGuid))
        .Select(c => new
        {
            c.Id,
            c.Type,
            c.Name,
            Members = c.Members.Select(m => new { m.UserId, m.User.NombreCompleto, m.User.Email }).ToList(),
            LastMessage = c.Messages
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new { m.Id, m.Body, m.CreatedAt, m.SenderId })
                .FirstOrDefault()
        })
        .ToListAsync();

    var ordered = raw.OrderByDescending(x => x.LastMessage?.CreatedAt ?? DateTimeOffset.MinValue).ToList();
    return Results.Ok(ordered);
}).RequireAuthorization().WithTags("Chat").WithOpenApi();

// Create or get 1:1 chat
app.MapPost("/api/chats/one-to-one", async (ClaimsPrincipal principal, CreateOneToOneRequest req, AppDbContext db) =>
{
    var meId = ClaimsHelpers.GetUserId(principal);
    if (meId == null) return Results.Unauthorized();
    var meGuid = meId.Value;
    if (req.UserId == Guid.Empty || req.UserId == meGuid) return Results.BadRequest(new { message = "Usuario inválido" });

    var otherExists = await db.Usuarios.AnyAsync(u => u.Id == req.UserId);
    if (!otherExists) return Results.NotFound(new { message = "Usuario no existe" });

    var existing = await db.Chats
        .Where(c => c.Type == ChatType.OneToOne
                    && c.Members.Any(m => m.UserId == meGuid)
                    && c.Members.Any(m => m.UserId == req.UserId))
        .Select(c => c.Id)
        .FirstOrDefaultAsync();

    if (existing != Guid.Empty)
        return Results.Ok(new { id = existing });

    var chat = new Chat
    {
        Id = Guid.NewGuid(),
        Type = ChatType.OneToOne,
        CreatedById = meGuid,
        CreatedAt = DateTimeOffset.UtcNow
    };
    db.Chats.Add(chat);
    db.ChatMembers.AddRange(
        new ChatMember { ChatId = chat.Id, UserId = meGuid, Role = ChatRole.Owner, JoinedAt = DateTimeOffset.UtcNow },
        new ChatMember { ChatId = chat.Id, UserId = req.UserId, Role = ChatRole.Member, JoinedAt = DateTimeOffset.UtcNow }
    );
    await db.SaveChangesAsync();
    return Results.Ok(new { id = chat.Id });
}).RequireAuthorization().WithTags("Chat").WithOpenApi();

// Create group chat
app.MapPost("/api/chats/group", async (ClaimsPrincipal principal, CreateGroupRequest req, AppDbContext db) =>
{
    var meId = ClaimsHelpers.GetUserId(principal);
    if (meId == null) return Results.Unauthorized();
    var meGuid = meId.Value;
    var name = (req.Name ?? string.Empty).Trim();
    if (name.Length < 3) return Results.BadRequest(new { message = "Nombre de grupo mínimo 3 caracteres" });

    var memberIds = (req.MemberIds ?? new List<Guid>()).Where(id => id != Guid.Empty && id != meGuid).Distinct().ToList();
    if (memberIds.Count == 0) return Results.BadRequest(new { message = "Agrega al menos un miembro" });

    // Validate users exist
    var count = await db.Usuarios.CountAsync(u => memberIds.Contains(u.Id));
    if (count != memberIds.Count) return Results.BadRequest(new { message = "Algún usuario no existe" });

    var chat = new Chat
    {
        Id = Guid.NewGuid(),
        Type = ChatType.Group,
        Name = name,
        CreatedById = meGuid,
        CreatedAt = DateTimeOffset.UtcNow
    };
    db.Chats.Add(chat);
    db.ChatMembers.Add(new ChatMember { ChatId = chat.Id, UserId = meGuid, Role = ChatRole.Owner, JoinedAt = DateTimeOffset.UtcNow });
    foreach (var uid in memberIds)
        db.ChatMembers.Add(new ChatMember { ChatId = chat.Id, UserId = uid, Role = ChatRole.Member, JoinedAt = DateTimeOffset.UtcNow });
    await db.SaveChangesAsync();
    return Results.Ok(new { id = chat.Id });
}).RequireAuthorization().WithTags("Chat").WithOpenApi();

// Add member to existing group
app.MapPost("/api/chats/{chatId:guid}/members", async (ClaimsPrincipal principal, Guid chatId, AddGroupMemberRequest req, AppDbContext db) =>
{
    var meId = ClaimsHelpers.GetUserId(principal);
    if (meId == null) return Results.Unauthorized();
    var meGuid = meId.Value;
    if (req.UserId == Guid.Empty) return Results.BadRequest(new { message = "Usuario inválido" });

    var chat = await db.Chats.FirstOrDefaultAsync(c => c.Id == chatId);
    if (chat is null) return Results.NotFound(new { message = "Chat no existe" });
    if (chat.Type != ChatType.Group) return Results.BadRequest(new { message = "Solo los grupos permiten agregar miembros" });

    var isMember = await db.ChatMembers.AnyAsync(m => m.ChatId == chatId && m.UserId == meGuid);
    if (!isMember) return Results.Forbid();

    var userExists = await db.Usuarios.AnyAsync(u => u.Id == req.UserId);
    if (!userExists) return Results.BadRequest(new { message = "Usuario no existe" });

    var alreadyMember = await db.ChatMembers.AnyAsync(m => m.ChatId == chatId && m.UserId == req.UserId);
    if (alreadyMember) return Results.Conflict(new { message = "Usuario ya es miembro de este chat" });

    db.ChatMembers.Add(new ChatMember
    {
        ChatId = chatId,
        UserId = req.UserId,
        Role = ChatRole.Member,
        JoinedAt = DateTimeOffset.UtcNow
    });
    await db.SaveChangesAsync();
    return Results.Ok(new { chatId, userId = req.UserId });
}).RequireAuthorization().WithTags("Chat").WithOpenApi();

// Get messages
app.MapGet("/api/chats/{chatId:guid}/messages", async (ClaimsPrincipal principal, Guid chatId, int skip, int take, AppDbContext db) =>
{
    var meId = ClaimsHelpers.GetUserId(principal);
    if (meId == null) return Results.Unauthorized();
    var meGuid = meId.Value;
    var isMember = await db.ChatMembers.AnyAsync(m => m.ChatId == chatId && m.UserId == meGuid);
    if (!isMember) return Results.Forbid();

    skip = Math.Max(skip, 0);
    take = Math.Clamp(take == 0 ? 50 : take, 1, 100);

    var msgs = await db.Messages
        .Where(m => m.ChatId == chatId)
        .OrderBy(m => m.CreatedAt)
        .Skip(skip)
        .Take(take)
        .Select(m => new { m.Id, m.Body, m.CreatedAt, m.SenderId })
        .ToListAsync();
    return Results.Ok(msgs);
}).RequireAuthorization().WithTags("Chat").WithOpenApi();

// Send message
app.MapPost("/api/chats/{chatId:guid}/messages", async (ClaimsPrincipal principal, Guid chatId, SendMessageRequest req, AppDbContext db, IHubContext<ChatAppHub> hub) =>
{
    var meId = ClaimsHelpers.GetUserId(principal);
    if (meId == null) return Results.Unauthorized();
    var meGuid = meId.Value;
    var isMember = await db.ChatMembers.AnyAsync(m => m.ChatId == chatId && m.UserId == meGuid);
    if (!isMember) return Results.Forbid();

    var body = (req.Text ?? string.Empty).Trim();
    if (body.Length == 0) return Results.BadRequest(new { message = "Mensaje vacío" });

    var msg = new Message
    {
        Id = Guid.NewGuid(),
        ChatId = chatId,
        SenderId = meGuid,
        Body = body,
        CreatedAt = DateTimeOffset.UtcNow
    };
    db.Messages.Add(msg);
    await db.SaveChangesAsync();

    var payload = new { id = msg.Id, body = msg.Body, createdAt = msg.CreatedAt, senderId = msg.SenderId, chatId };
    await hub.Clients.Group(chatId.ToString()).SendAsync("chat:message", payload);
    return Results.Ok(payload);
}).RequireAuthorization().WithTags("Chat").WithOpenApi();

// SignalR Hub for real-time chat
app.MapHub<ChatAppHub>("/apphub").RequireAuthorization();

app.Run();
