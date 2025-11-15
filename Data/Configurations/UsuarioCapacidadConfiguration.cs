using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskControlBackend.Models;

namespace TaskControlBackend.Data.Configurations;

public class UsuarioCapacidadConfiguration : IEntityTypeConfiguration<UsuarioCapacidad>
{
    public void Configure(EntityTypeBuilder<UsuarioCapacidad> b)
    {
        b.ToTable("UsuarioCapacidades");
        b.HasKey(x => new { x.UsuarioId, x.CapacidadId });
        b.Property(x => x.Nivel).HasDefaultValue(1);
        b.HasOne(x => x.Usuario).WithMany(u => u.UsuarioCapacidades).HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Capacidad).WithMany(c => c.UsuarioCapacidades).HasForeignKey(x => x.CapacidadId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}