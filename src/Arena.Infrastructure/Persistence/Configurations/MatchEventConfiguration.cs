using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arena.Infrastructure.Persistence.Configurations;

public sealed class MatchEventConfiguration : IEntityTypeConfiguration<MatchEvent>
{
    public void Configure(EntityTypeBuilder<MatchEvent> e)
    {
        // BR05: both sides must be registered in the match's tournament (composite FKs to TournamentTeams).
        e.HasOne<TournamentTeam>().WithMany().HasForeignKey(x => new { x.TournamentId, x.HomeTeamId })
            .HasPrincipalKey(x => new { x.TournamentId, x.TeamId });
        e.HasOne<TournamentTeam>().WithMany().HasForeignKey(x => new { x.TournamentId, x.AwayTeamId })
            .HasPrincipalKey(x => new { x.TournamentId, x.TeamId });
        e.HasIndex(x => new { x.TournamentId, x.HomeTeamId, x.AwayTeamId, x.Leg }).IsUnique()
            .HasFilter("[EventType] = 'Match'");
    }
}
