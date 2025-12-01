using Microsoft.EntityFrameworkCore;
using TaskControlBackend.Models;
using TaskControlBackend.Models.Chat;

namespace TaskControlBackend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options): base(options) {}

    // ==================== CORE ENTITIES ====================
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Capacidad> Capacidades => Set<Capacidad>();
    public DbSet<UsuarioCapacidad> UsuarioCapacidades => Set<UsuarioCapacidad>();

    public DbSet<Tarea> Tareas { get; set; } = null!;
    public DbSet<TareaCapacidadRequerida> TareasCapacidadesRequeridas { get; set; } = null!;
    public DbSet<TareaAsignacionHistorial> TareasAsignacionesHistorial { get; set; } = null!;
    
    // ==================== NUEVAS ENTIDADES PARA ARCHIVOS ====================
    public DbSet<TareaDocumentoAdjunto> TareasDocumentosAdjuntos { get; set; } = null!;
    public DbSet<TareaEvidencia> TareasEvidencias { get; set; } = null!;

    // ==================== CHAT ENTITIES ====================
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMember> ConversationMembers => Set<ConversationMember>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<MessageDeliveryStatus> MessageDeliveryStatuses => Set<MessageDeliveryStatus>();
    public DbSet<MessageReadStatus> MessageReadStatuses => Set<MessageReadStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // ==================== SOFT DELETE FILTERS ====================
        modelBuilder.Entity<Empresa>().HasQueryFilter(e => e.IsActive);
        modelBuilder.Entity<Usuario>().HasQueryFilter(u => u.IsActive);

        // ==================== TAREA DOCUMENTOS Y EVIDENCIAS ====================
        
        // TareaDocumentoAdjunto relationships
        modelBuilder.Entity<TareaDocumentoAdjunto>()
            .HasOne(d => d.Tarea)
            .WithMany(t => t.DocumentosAdjuntos)
            .HasForeignKey(d => d.TareaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TareaDocumentoAdjunto>()
            .HasOne(d => d.SubidoPorUsuario)
            .WithMany()
            .HasForeignKey(d => d.SubidoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        // TareaEvidencia relationships
        modelBuilder.Entity<TareaEvidencia>()
            .HasOne(e => e.Tarea)
            .WithMany(t => t.Evidencias)
            .HasForeignKey(e => e.TareaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TareaEvidencia>()
            .HasOne(e => e.SubidoPorUsuario)
            .WithMany()
            .HasForeignKey(e => e.SubidoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for performance
        modelBuilder.Entity<TareaDocumentoAdjunto>()
            .HasIndex(d => d.TareaId)
            .HasDatabaseName("IX_TareaDocumentoAdjunto_TareaId");

        modelBuilder.Entity<TareaEvidencia>()
            .HasIndex(e => e.TareaId)
            .HasDatabaseName("IX_TareaEvidencia_TareaId");

        // ==================== CHAT CONFIGURATIONS ====================

        // ConversationMember: Composite primary key
        modelBuilder.Entity<ConversationMember>()
            .HasKey(cm => new { cm.ConversationId, cm.UserId });

        // ConversationMember relationships
        modelBuilder.Entity<ConversationMember>()
            .HasOne(cm => cm.Conversation)
            .WithMany(c => c.Members)
            .HasForeignKey(cm => cm.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ConversationMember>()
            .HasOne(cm => cm.User)
            .WithMany()
            .HasForeignKey(cm => cm.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Conversation relationships
        modelBuilder.Entity<Conversation>()
            .HasOne(c => c.CreatedBy)
            .WithMany()
            .HasForeignKey(c => c.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        // ChatMessage relationships
        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        // ChatMessage self-referencing for replies
        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.ReplyToMessage)
            .WithMany(m => m.Replies)
            .HasForeignKey(m => m.ReplyToMessageId)
            .OnDelete(DeleteBehavior.Restrict);

        // MessageDeliveryStatus relationships
        modelBuilder.Entity<MessageDeliveryStatus>()
            .HasOne(ds => ds.Message)
            .WithMany(m => m.DeliveryStatuses)
            .HasForeignKey(ds => ds.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MessageDeliveryStatus>()
            .HasOne(ds => ds.DeliveredToUser)
            .WithMany()
            .HasForeignKey(ds => ds.DeliveredToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // MessageReadStatus relationships
        modelBuilder.Entity<MessageReadStatus>()
            .HasOne(rs => rs.Message)
            .WithMany(m => m.ReadStatuses)
            .HasForeignKey(rs => rs.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MessageReadStatus>()
            .HasOne(rs => rs.ReadByUser)
            .WithMany()
            .HasForeignKey(rs => rs.ReadByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ==================== INDEXES FOR PERFORMANCE ====================

        // Conversation indexes
        modelBuilder.Entity<Conversation>()
            .HasIndex(c => c.CreatedById);

        modelBuilder.Entity<Conversation>()
            .HasIndex(c => c.LastActivityAt)
            .HasDatabaseName("IX_Conversation_LastActivityAt");

        // ConversationMember indexes
        modelBuilder.Entity<ConversationMember>()
            .HasIndex(cm => cm.UserId)
            .HasDatabaseName("IX_ConversationMember_UserId");

        modelBuilder.Entity<ConversationMember>()
            .HasIndex(cm => new { cm.UserId, cm.IsActive })
            .HasDatabaseName("IX_ConversationMember_UserId_IsActive");

        // ChatMessage indexes
        modelBuilder.Entity<ChatMessage>()
            .HasIndex(m => new { m.ConversationId, m.SentAt })
            .HasDatabaseName("IX_ChatMessage_ConversationId_SentAt");

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(m => m.SenderId)
            .HasDatabaseName("IX_ChatMessage_SenderId");

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(m => m.ReplyToMessageId)
            .HasDatabaseName("IX_ChatMessage_ReplyToMessageId");

        // MessageDeliveryStatus: Unique constraint - one delivery per user per message
        modelBuilder.Entity<MessageDeliveryStatus>()
            .HasIndex(ds => new { ds.MessageId, ds.DeliveredToUserId })
            .IsUnique()
            .HasDatabaseName("IX_MessageDeliveryStatus_MessageId_UserId_Unique");

        modelBuilder.Entity<MessageDeliveryStatus>()
            .HasIndex(ds => ds.DeliveredToUserId)
            .HasDatabaseName("IX_MessageDeliveryStatus_UserId");

        // MessageReadStatus: Unique constraint - one read per user per message
        modelBuilder.Entity<MessageReadStatus>()
            .HasIndex(rs => new { rs.MessageId, rs.ReadByUserId })
            .IsUnique()
            .HasDatabaseName("IX_MessageReadStatus_MessageId_UserId_Unique");

        modelBuilder.Entity<MessageReadStatus>()
            .HasIndex(rs => rs.ReadByUserId)
            .HasDatabaseName("IX_MessageReadStatus_UserId");
    }
}