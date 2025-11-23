using Microsoft.EntityFrameworkCore;
using TaskControlBackend.Models;
using TaskControlBackend.Models.Chat;

namespace TaskControlBackend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options): base(options) {}
    
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Capacidad> Capacidades => Set<Capacidad>();
    public DbSet<UsuarioCapacidad> UsuarioCapacidades => Set<UsuarioCapacidad>();
    
    public DbSet<Tarea> Tareas { get; set; } = null!;
    public DbSet<TareaCapacidadRequerida> TareasCapacidadesRequeridas { get; set; } = null!;
    
    // Chat entities
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<ChatMember> ChatMembers => Set<ChatMember>();
    public DbSet<Message> Messages => Set<Message>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Soft delete global
        modelBuilder.Entity<Empresa>().HasQueryFilter(e => e.IsActive);
        modelBuilder.Entity<Usuario>().HasQueryFilter(u => u.IsActive);
        
        // Chat entity configurations
        modelBuilder.Entity<Chat>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Type).IsRequired();
            b.Property(x => x.Name).HasMaxLength(128);
            b.Property(x => x.CreatedAt).IsRequired();
            b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById);
        });

        modelBuilder.Entity<ChatMember>(b =>
        {
            b.HasKey(x => new { x.ChatId, x.UserId });
            b.Property(x => x.Role).HasDefaultValue(ChatRole.Member);
            b.Property(x => x.JoinedAt).IsRequired();
            b.HasOne(x => x.Chat).WithMany(c => c.Members).HasForeignKey(x => x.ChatId);
            b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<Message>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
            b.HasIndex(x => new { x.ChatId, x.CreatedAt });
            b.HasOne(x => x.Chat).WithMany(c => c.Messages).HasForeignKey(x => x.ChatId);
            b.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId);
        });
    }
    
}