using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskControlBackend.Models;

namespace TaskControlBackend.Data.Configurations;

public class CapacidadConfiguration : IEntityTypeConfiguration<Capacidad>
{
    public void Configure(EntityTypeBuilder<Capacidad> b)
    {
        b.ToTable("Capacidades");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        b.HasIndex(x => new { x.EmpresaId, x.Nombre }).IsUnique();
        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.IsActive).HasDefaultValue(true);
    }
}