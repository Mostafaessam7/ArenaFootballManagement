using Arena.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Arena.Infrastructure;

public sealed class ArenaDbContext(DbContextOptions<ArenaDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<PlayerMembership> PlayerMemberships => Set<PlayerMembership>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<TournamentTeam> TournamentTeams => Set<TournamentTeam>();
    public DbSet<TournamentRoster> TournamentRosters => Set<TournamentRoster>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<SportEvent> Events => Set<SportEvent>();
    public DbSet<MatchPlayerStat> MatchPlayerStats => Set<MatchPlayerStat>();
    public DbSet<TicketOffer> TicketOffers => Set<TicketOffer>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<ResultAudit> ResultAudits => Set<ResultAudit>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<UserAccount>(e => {
            e.ToTable("Users", t => t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Admin','Organizer','TeamManager','Spectator','GateStaff')"));
            e.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.NormalizedEmail).HasMaxLength(256).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => x.NormalizedEmail).IsUnique();
        });
        b.Entity<Team>(e => {
            e.ToTable("Teams");
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Code).HasMaxLength(10).IsRequired();
            e.Property(x => x.City).HasMaxLength(80).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ManagerId);
        });
        b.Entity<Player>(e => {
            e.ToTable("Players");
            e.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            e.Property(x => x.Nationality).HasMaxLength(80).IsRequired();
            e.Property(x => x.Position).HasMaxLength(20).IsRequired();
        });
        b.Entity<PlayerMembership>(e => {
            e.ToTable("PlayerMemberships", t => t.HasCheckConstraint("CK_Membership_Dates", "[LeftOn] IS NULL OR [LeftOn] > [JoinedOn]"));
            e.HasOne<Player>().WithMany().HasForeignKey(x => x.PlayerId);
            e.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId);
            e.HasIndex(x => x.PlayerId).IsUnique().HasFilter("[LeftOn] IS NULL");
        });
        b.Entity<Tournament>(e => {
            e.ToTable("Tournaments", t => {
                t.HasCheckConstraint("CK_Tournament_Dates", "[EndDate] >= [StartDate]");
                t.HasCheckConstraint("CK_Tournament_Status", "[Status] IN ('Draft','RegistrationOpen','InProgress','Completed')");
            });
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Season).HasMaxLength(30).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => new { x.Name, x.Season }).IsUnique();
            e.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OrganizerId);
        });
        b.Entity<TournamentTeam>(e => {
            e.ToTable("TournamentTeams");
            e.HasAlternateKey(x => new { x.TournamentId, x.TeamId });
            e.HasOne<Tournament>().WithMany().HasForeignKey(x => x.TournamentId);
            e.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId);
        });
        b.Entity<TournamentRoster>(e => {
            e.ToTable("TournamentRosters", t => t.HasCheckConstraint("CK_Roster_Shirt", "[ShirtNumber] BETWEEN 1 AND 99"));
            e.HasAlternateKey(x => new { x.TournamentId, x.TeamId, x.PlayerId });
            e.HasIndex(x => new { x.TournamentId, x.PlayerId }).IsUnique();
            e.HasIndex(x => new { x.TournamentId, x.TeamId, x.ShirtNumber }).IsUnique();
            e.HasOne<TournamentTeam>().WithMany().HasForeignKey(x => new { x.TournamentId, x.TeamId })
                .HasPrincipalKey(x => new { x.TournamentId, x.TeamId });
            e.HasOne<Player>().WithMany().HasForeignKey(x => x.PlayerId);
        });
        b.Entity<Venue>(e => {
            e.ToTable("Venues", t => t.HasCheckConstraint("CK_Venue_Capacity", "[Capacity] > 0"));
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.City).HasMaxLength(80).IsRequired();
            e.Property(x => x.Address).HasMaxLength(300).IsRequired();
        });
        b.Entity<SportEvent>(e => {
            e.ToTable("Events", t => {
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
        });
        b.Entity<MatchEvent>(e => {
            e.HasOne<TournamentTeam>().WithMany().HasForeignKey(x => new { x.TournamentId, x.HomeTeamId })
                .HasPrincipalKey(x => new { x.TournamentId, x.TeamId });
            e.HasOne<TournamentTeam>().WithMany().HasForeignKey(x => new { x.TournamentId, x.AwayTeamId })
                .HasPrincipalKey(x => new { x.TournamentId, x.TeamId });
            e.HasIndex(x => new { x.TournamentId, x.HomeTeamId, x.AwayTeamId, x.Leg }).IsUnique()
                .HasFilter("[EventType] = 'Match'");
        });
        b.Entity<CommunityEvent>().Property(x => x.Description).HasMaxLength(2000);
        b.Entity<MatchPlayerStat>(e => {
            e.ToTable("MatchPlayerStats", t => t.HasCheckConstraint("CK_PlayerStats_Range", "[Goals] BETWEEN 0 AND 99 AND [Assists] BETWEEN 0 AND 99 AND [YellowCards] BETWEEN 0 AND 2 AND [RedCards] BETWEEN 0 AND 1 AND [MinutesPlayed] BETWEEN 0 AND 130"));
            e.HasOne<MatchEvent>().WithMany().HasForeignKey(x => x.MatchId);
            e.HasOne<TournamentRoster>().WithMany().HasForeignKey(x => new { x.TournamentId, x.TeamId, x.PlayerId })
                .HasPrincipalKey(x => new { x.TournamentId, x.TeamId, x.PlayerId });
            e.HasIndex(x => new { x.MatchId, x.PlayerId }).IsUnique();
        });
        b.Entity<TicketOffer>(e => {
            e.ToTable("TicketOffers", t => {
                t.HasCheckConstraint("CK_Offer_Inventory", "[Capacity] > 0 AND [SoldQuantity] >= 0 AND [SoldQuantity] <= [Capacity]");
                t.HasCheckConstraint("CK_Offer_Price", "[Price] >= 0");
                t.HasCheckConstraint("CK_Offer_Dates", "[SalesEndUtc] > [SalesStartUtc]");
            });
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.Property(x => x.Price).HasPrecision(12, 2);
            e.HasOne<SportEvent>().WithMany().HasForeignKey(x => x.EventId);
            e.HasIndex(x => new { x.EventId, x.Name }).IsUnique();
        });
        b.Entity<Booking>(e => {
            e.ToTable("Bookings", t => {
                t.HasCheckConstraint("CK_Booking_Quantity", "[Quantity] BETWEEN 1 AND 10 AND [UnitPrice] >= 0");
                t.HasCheckConstraint("CK_Booking_Status", "([Status] = 'Confirmed' AND [CancelledUtc] IS NULL) OR ([Status] = 'Cancelled' AND [CancelledUtc] IS NOT NULL)");
            });
            e.Property(x => x.IdempotencyKey).HasMaxLength(64).IsRequired();
            e.Property(x => x.UnitPrice).HasPrecision(12, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId);
            e.HasOne<TicketOffer>().WithMany().HasForeignKey(x => x.TicketOfferId);
            e.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
        });
        b.Entity<Ticket>(e => {
            e.ToTable("Tickets", t => t.HasCheckConstraint("CK_Ticket_CheckIn", "([CheckedInUtc] IS NULL AND [CheckedInById] IS NULL) OR ([CheckedInUtc] IS NOT NULL AND [CheckedInById] IS NOT NULL)"));
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId);
            e.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CheckedInById);
        });
        b.Entity<ResultAudit>(e => {
            e.ToTable("ResultAudits");
            e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            e.HasOne<MatchEvent>().WithMany().HasForeignKey(x => x.MatchId);
            e.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorId);
        });
        foreach (var entity in b.Model.GetEntityTypes().Where(x => x.BaseType is null))
        {
            b.Entity(entity.ClrType).HasKey(nameof(Entity.Id));
            b.Entity(entity.ClrType).Property(nameof(Entity.Version)).IsConcurrencyToken();
        }
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        var utcConverter = new ValueConverter<DateTime, DateTime>(value => value,
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        foreach (var property in b.Model.GetEntityTypes().SelectMany(x => x.GetProperties())
            .Where(x => x.ClrType == typeof(DateTime) || x.ClrType == typeof(DateTime?)))
            property.SetValueConverter(utcConverter);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Entity>().Where(x => x.State == EntityState.Modified))
            entry.Entity.Version = Guid.NewGuid();
        return base.SaveChangesAsync(cancellationToken);
    }
}
