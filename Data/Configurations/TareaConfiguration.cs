using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskControlBackend.Models;

namespace TaskControlBackend.Data.Configurations
{
    public class TareaConfiguration : IEntityTypeConfiguration<Tarea>
    {
        public void Configure(EntityTypeBuilder<Tarea> b)
        {
            b.ToTable("Tareas");
            b.HasKey(t => t.Id);

            b.Property(t => t.Titulo).HasMaxLength(200).IsRequired();
            b.Property(t => t.Descripcion).HasMaxLength(2000).IsRequired();

            b.HasOne(t => t.Empresa)
                .WithMany()
                .HasForeignKey(t => t.EmpresaId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(t => t.AsignadoAUsuario)
                .WithMany()
                .HasForeignKey(t => t.AsignadoAUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(t => t.CreatedByUsuario)
                .WithMany()
                .HasForeignKey(t => t.CreatedByUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configuración de delegación entre jefes
            b.HasOne(t => t.DelegadoPorUsuario)
                .WithMany()
                .HasForeignKey(t => t.DelegadoPorUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(t => t.DelegadoAUsuario)
                .WithMany()
                .HasForeignKey(t => t.DelegadoAUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            b.Property(t => t.EstaDelegada).HasDefaultValue(false);
            b.Property(t => t.MotivoRechazoJefe).HasMaxLength(500);

            b.Property(t => t.Estado).IsRequired();
            b.Property(t => t.Prioridad).IsRequired();
            b.Property(t => t.IsActive).HasDefaultValue(true);

            b.HasMany(t => t.CapacidadesRequeridas)
                .WithOne(cr => cr.Tarea)
                .HasForeignKey(cr => cr.TareaId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(t => new { t.EmpresaId, t.Estado });
            b.HasIndex(t => new { t.DelegadoAUsuarioId, t.EstaDelegada });
        }
    }
}