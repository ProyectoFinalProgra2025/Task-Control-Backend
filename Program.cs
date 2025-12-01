using System.Security.Claims;
using System.Text;
using System.Linq;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TaskControlBackend.Data;
using TaskControlBackend.Helpers;
using TaskControlBackend.Hubs;
using TaskControlBackend.Models.Enums;
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
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddControllers();
builder.Services.AddSingleton<BlobService>();


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
                // Soportar ambos hubs: TareaHub y ChatHub
                if (!string.IsNullOrEmpty(accessToken) && 
                    (path.StartsWithSegments("/tareahub") || path.StartsWithSegments("/chathub")))
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
    // Soporte para IFormFile en Swagger
    c.SupportNonNullableReferenceTypes();
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

// Map SignalR Hubs
app.MapHub<TareaHub>("/tareahub");  // Para tareas, métricas, actualizaciones generales
app.MapHub<ChatHub>("/chathub");    // Para chat (separado)

// ==================== CHAT ENDPOINTS ====================
// NOTA: Los endpoints de chat están implementados en ChatController.cs
// Esto elimina la duplicación y mejora el mantenimiento del código

app.Run();
