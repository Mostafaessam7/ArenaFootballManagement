using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arena.Infrastructure.Persistence.Configurations;

public sealed class TournamentRosterConfiguration : IEntityTypeConfiguration<TournamentRoster>
{
    public void Configure(EntityTypeBuilder<TournamentRoster> e)
    {
        e.ToTable("TournamentRosters", t => t.HasCheckConstraint("CK_Roster_Shirt", "[ShirtNumber] BETWEEN 1 AND 99"));
        e.HasAlternateKey(x => new { x.TournamentId, x.TeamId, x.PlayerId });
        // BR04: one team per player in a tournament; shirt numbers unique inside a team.
        e.HasIndex(x => new { x.TournamentId, x.PlayerId }).IsUnique();
        e.HasIndex(x => new { x.TournamentId, x.TeamId, x.ShirtNumber }).IsUnique();
        e.HasOne<TournamentTeam>().WithMany().HasForeignKey(x => new { x.TournamentId, x.TeamId })
            .HasPrincipalKey(x => new { x.TournamentId, x.TeamId });
        e.HasOne<Player>().WithMany().HasForeignKey(x => x.PlayerId);
    }
}
