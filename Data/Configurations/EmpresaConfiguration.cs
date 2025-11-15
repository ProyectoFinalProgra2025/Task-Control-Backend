using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskControlBackend.Models;

namespace TaskControlBackend.Data.Configurations;

public class EmpresaConfiguration : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> b)
    {
        b.ToTable("Empresas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
        b.Property(x => x.Estado).IsRequired();
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.HasMany(x => x.Usuarios)
            .WithOne(u => u.Empresa!)
            .HasForeignKey(u => u.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.Estado);
    }
}