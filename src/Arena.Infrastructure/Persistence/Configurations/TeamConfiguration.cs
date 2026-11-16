using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arena.Infrastructure.Persistence.Configurations;

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> e)
    {
        e.ToTable("Teams");
        e.Property(x => x.Name).HasMaxLength(120).IsRequired();
        e.Property(x => x.Code).HasMaxLength(10).IsRequired();
        e.Property(x => x.City).HasMaxLength(80).IsRequired();
        e.HasIndex(x => x.Code).IsUnique();
        e.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ManagerId);
    }
}
