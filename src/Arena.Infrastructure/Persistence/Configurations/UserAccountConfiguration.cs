using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arena.Infrastructure.Persistence.Configurations;

public sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> e)
    {
        e.ToTable("Users", t => t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Admin','Organizer','TeamManager','Spectator','GateStaff')"));
        e.Property(x => x.FullName).HasMaxLength(120).IsRequired();
        e.Property(x => x.Email).HasMaxLength(256).IsRequired();
        e.Property(x => x.NormalizedEmail).HasMaxLength(256).IsRequired();
        e.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        e.Property(x => x.Role).HasConversion<string>().HasMaxLength(30);
        e.HasIndex(x => x.NormalizedEmail).IsUnique();
    }
}
