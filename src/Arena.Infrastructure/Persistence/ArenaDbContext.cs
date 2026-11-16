using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Arena.Infrastructure.Persistence;

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
        // One IEntityTypeConfiguration per table, in Persistence/Configurations.
        b.ApplyConfigurationsFromAssembly(typeof(ArenaDbContext).Assembly);

        // Conventions shared by every table.
        foreach (var entity in b.Model.GetEntityTypes().Where(x => x.BaseType is null))
        {
            b.Entity(entity.ClrType).HasKey(nameof(Entity.Id));
            b.Entity(entity.ClrType).Property(nameof(Entity.Version)).IsConcurrencyToken();
        }
        // History is never deleted by cascade.
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        // Every DateTime is stored and read back as UTC.
        var utcConverter = new ValueConverter<DateTime, DateTime>(value => value,
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        foreach (var property in b.Model.GetEntityTypes().SelectMany(x => x.GetProperties())
            .Where(x => x.ClrType == typeof(DateTime) || x.ClrType == typeof(DateTime?)))
            property.SetValueConverter(utcConverter);
    }

    /// <summary>A new Version on every update makes concurrent writes on stale data fail.</summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Entity>().Where(x => x.State == EntityState.Modified))
            entry.Entity.Version = Guid.NewGuid();
        return base.SaveChangesAsync(cancellationToken);
    }
}
