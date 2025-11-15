using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskControlBackend.Models;

namespace TaskControlBackend.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).HasMaxLength(200).IsRequired();
        b.HasIndex(x => new { x.UsuarioId, x.TokenHash }).IsUnique();
        b.Property(x => x.IsRevoked).HasDefaultValue(false);
    }
}