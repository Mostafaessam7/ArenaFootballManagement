using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arena.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table Per Hierarchy: MatchEvent and CommunityEvent share the Events table.
/// CK_Event_Specialization makes the specialization total and disjoint at database level.
/// </summary>
public sealed class SportEventConfiguration : IEntityTypeConfiguration<SportEvent>
{
    public void Configure(EntityTypeBuilder<SportEvent> e)
    {
        e.ToTable("Events", t =>
        {
            t.HasCheckConstraint("CK_Event_Dates", "[EndsAtUtc] > [StartsAtUtc]");
            t.HasCheckConstraint("CK_Event_Status", "[Status] IN ('Scheduled','Finished','Cancelled')");
            t.HasCheckConstraint("CK_Event_Specialization", "([EventType] = 'Match' AND [TournamentId] IS NOT NULL AND [HomeTeamId] IS NOT NULL AND [AwayTeamId] IS NOT NULL AND [RoundNumber] IS NOT NULL AND [Leg] IS NOT NULL AND [Description] IS NULL) OR ([EventType] = 'Community' AND [Description] IS NOT NULL AND [TournamentId] IS NULL AND [HomeTeamId] IS NULL AND [AwayTeamId] IS NULL AND [RoundNumber] IS NULL AND [Leg] IS NULL AND [HomeScore] IS NULL AND [AwayScore] IS NULL)");
            t.HasCheckConstraint("CK_Match_Teams", "[HomeTeamId] IS NULL OR [HomeTeamId] <> [AwayTeamId]");
            t.HasCheckConstraint("CK_Match_Round", "[RoundNumber] IS NULL OR ([RoundNumber] > 0 AND [Leg] BETWEEN 1 AND 2)");
            t.HasCheckConstraint("CK_Match_Scores", "[EventType] <> 'Match' OR (([Status] = 'Finished' AND [HomeScore] IS NOT NULL AND [AwayScore] IS NOT NULL AND [HomeScore] BETWEEN 0 AND 99 AND [AwayScore] BETWEEN 0 AND 99) OR ([Status] <> 'Finished' AND [HomeScore] IS NULL AND [AwayScore] IS NULL))");
        });
        e.Property(x => x.Title).HasMaxLength(180).IsRequired();
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        e.HasDiscriminator<string>("EventType").HasValue<MatchEvent>("Match").HasValue<CommunityEvent>("Community");
        e.Property<string>("EventType").HasMaxLength(20);
        e.HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId);
        e.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OrganizerId);
        e.HasIndex(x => new { x.VenueId, x.StartsAtUtc });
    }
}
