using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arena.Infrastructure.Persistence.Configurations;

public sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> e)
    {
        e.ToTable("Players");
        e.Property(x => x.FullName).HasMaxLength(120).IsRequired();
        e.Property(x => x.Nationality).HasMaxLength(80).IsRequired();
        e.Property(x => x.Position).HasMaxLength(20).IsRequired();
    }
}
