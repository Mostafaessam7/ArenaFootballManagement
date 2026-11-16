using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arena.Infrastructure.Persistence.Configurations;

public sealed class MatchPlayerStatConfiguration : IEntityTypeConfiguration<MatchPlayerStat>
{
    public void Configure(EntityTypeBuilder<MatchPlayerStat> e)
    {
        e.ToTable("MatchPlayerStats", t => t.HasCheckConstraint("CK_PlayerStats_Range", "[Goals] BETWEEN 0 AND 99 AND [Assists] BETWEEN 0 AND 99 AND [YellowCards] BETWEEN 0 AND 2 AND [RedCards] BETWEEN 0 AND 1 AND [MinutesPlayed] BETWEEN 0 AND 130"));
        e.HasOne<MatchEvent>().WithMany().HasForeignKey(x => x.MatchId);
        // BR13: the player must be on the tournament roster for that team.
        e.HasOne<TournamentRoster>().WithMany().HasForeignKey(x => new { x.TournamentId, x.TeamId, x.PlayerId })
            .HasPrincipalKey(x => new { x.TournamentId, x.TeamId, x.PlayerId });
        e.HasIndex(x => new { x.MatchId, x.PlayerId }).IsUnique();
    }
}
