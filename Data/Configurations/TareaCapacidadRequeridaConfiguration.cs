using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskControlBackend.Models;

namespace TaskControlBackend.Data.Configurations
{
    public class TareaCapacidadRequeridaConfiguration : IEntityTypeConfiguration<TareaCapacidadRequerida>
    {
        public void Configure(EntityTypeBuilder<TareaCapacidadRequerida> b)
        {
            b.ToTable("TareasCapacidadesRequeridas");
            b.HasKey(x => x.Id);
            b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();

            b.HasIndex(x => new { x.TareaId, x.Nombre }).IsUnique();
        }
    }
}