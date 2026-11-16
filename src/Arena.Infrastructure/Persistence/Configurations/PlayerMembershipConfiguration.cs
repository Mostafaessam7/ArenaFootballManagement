using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arena.Infrastructure.Persistence.Configurations;

public sealed class PlayerMembershipConfiguration : IEntityTypeConfiguration<PlayerMembership>
{
    public void Configure(EntityTypeBuilder<PlayerMembership> e)
    {
        e.ToTable("PlayerMemberships", t => t.HasCheckConstraint("CK_Membership_Dates", "[LeftOn] IS NULL OR [LeftOn] > [JoinedOn]"));
        e.HasOne<Player>().WithMany().HasForeignKey(x => x.PlayerId);
        e.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId);
        // BR02: only one open membership per player.
        e.HasIndex(x => x.PlayerId).IsUnique().HasFilter("[LeftOn] IS NULL");
    }
}
