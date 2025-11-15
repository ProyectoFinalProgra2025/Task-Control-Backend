using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TaskControlBackend.Data;
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
//builder.Services.AddScoped<IEmpresaService, EmpresaService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddControllers();

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

// Middlewares
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();