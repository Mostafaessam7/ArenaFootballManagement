using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arena.Infrastructure.Persistence.Configurations;

public sealed class TournamentTeamConfiguration : IEntityTypeConfiguration<TournamentTeam>
{
    public void Configure(EntityTypeBuilder<TournamentTeam> e)
    {
        e.ToTable("TournamentTeams");
        // BR03: a team registers once per tournament. Also the target of the composite FKs from matches and rosters.
        e.HasAlternateKey(x => new { x.TournamentId, x.TeamId });
        e.HasOne<Tournament>().WithMany().HasForeignKey(x => x.TournamentId);
        e.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId);
    }
}
