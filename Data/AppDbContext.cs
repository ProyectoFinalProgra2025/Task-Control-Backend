using Microsoft.EntityFrameworkCore;
using TaskControlBackend.Models;

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
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Soft delete global
        modelBuilder.Entity<Empresa>().HasQueryFilter(e => e.IsActive);
        modelBuilder.Entity<Usuario>().HasQueryFilter(u => u.IsActive);
    }
    
}