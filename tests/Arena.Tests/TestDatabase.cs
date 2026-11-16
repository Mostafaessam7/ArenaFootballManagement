using Arena.Application.Features.Bookings;
using Arena.Application.Features.Events;
using Arena.Application.Features.Players;
using Arena.Application.Features.Results;
using Arena.Application.Features.Tickets;
using Arena.Application.Features.Tournaments;
using Arena.Domain.Entities;
using Arena.Domain.Enums;
using Arena.Infrastructure.Persistence;
using Arena.Infrastructure.Queries;
using Arena.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Arena.Tests;

public sealed class FixedClock(DateTime utc) : TimeProvider
{
    public DateTime Utc { get; set; } = utc;
    public override DateTimeOffset GetUtcNow() => new(Utc, TimeSpan.Zero);
}

/// <summary>In-memory relational SQLite database with the real services wired to one Unit of Work.</summary>
public sealed class TestDatabase : IAsyncDisposable
{
    public SqliteConnection Connection { get; } = new("Data Source=:memory:");
    public ArenaDbContext Db { get; }
    public FixedClock Clock { get; } = new(new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc));
    public PlayerService Players { get; }
    public TournamentService Tournaments { get; }
    public EventService Events { get; }
    public ResultService Results { get; }
    public TicketService Tickets { get; }
    public BookingService Bookings { get; }
    public UserAccount Admin { get; } = new() { Email = "admin@test.invalid", NormalizedEmail = "ADMIN@TEST.INVALID", FullName = "Test Admin", Role = UserRole.Admin };
    public UserAccount Fan { get; } = new() { Email = "fan@test.invalid", NormalizedEmail = "FAN@TEST.INVALID", FullName = "Test Fan" };
    public UserAccount OtherFan { get; } = new() { Email = "other@test.invalid", NormalizedEmail = "OTHER@TEST.INVALID", FullName = "Other Fan" };

    public TestDatabase()
    {
        Connection.Open();
        Db = NewContext();
        var uow = new UnitOfWork(Db);
        var queries = new CompetitionQueries(Db);
        Players = new(uow, Clock);
        Tournaments = new(uow, queries, Clock);
        Events = new(uow, queries, Clock);
        Results = new(uow, queries, Clock);
        Tickets = new(uow, queries, Clock);
        Bookings = new(uow, queries, new TicketInventory(Db), Clock);
    }

    public ArenaDbContext NewContext() => new(new DbContextOptionsBuilder<ArenaDbContext>().UseSqlite(Connection).Options);

    public static async Task<TestDatabase> CreateAsync()
    {
        var fixture = new TestDatabase();
        await fixture.Db.Database.EnsureCreatedAsync();
        fixture.Db.AddRange(fixture.Admin, fixture.Fan, fixture.OtherFan);
        await fixture.Db.SaveChangesAsync();
        return fixture;
    }

    public async Task<(CommunityEvent Event, TicketOffer Offer)> AddOfferAsync(int capacity = 5)
    {
        var venue = new Venue { Name = "Test ground", City = "Cairo", Address = "Test", Capacity = 100 };
        Db.Add(venue); await Db.SaveChangesAsync();
        var item = new CommunityEvent { Title = "Fan day", Description = "Football community", VenueId = venue.Id,
            OrganizerId = Admin.Id, StartsAtUtc = Clock.Utc.AddDays(5), EndsAtUtc = Clock.Utc.AddDays(5).AddHours(2) };
        Db.Add(item); await Db.SaveChangesAsync();
        var offer = new TicketOffer { EventId = item.Id, Name = "General", Price = 100.50m, Capacity = capacity,
            SalesStartUtc = Clock.Utc.AddDays(-1), SalesEndUtc = item.StartsAtUtc.AddHours(-1) };
        Db.Add(offer); await Db.SaveChangesAsync();
        return (item, offer);
    }

    public async Task<(Tournament Tournament, Team Home, Team Away, Venue Venue, Player Player)> CompetitionAsync()
    {
        var home = new Team { Name = "Home", Code = "HOM", City = "Cairo" };
        var away = new Team { Name = "Away", Code = "AWY", City = "Giza" };
        var venue = new Venue { Name = "Ground", City = "Cairo", Address = "Address", Capacity = 500 };
        var player = new Player { FullName = "Player", Nationality = "Egypt", Position = "Forward", BirthDate = new(2000,1,1) };
        var tournament = new Tournament { Name = "League", Season = "2026", OrganizerId = Admin.Id,
            StartDate = new(2026,11,1), EndDate = new(2026,12,30) };
        Db.AddRange(home, away, venue, player, tournament); await Db.SaveChangesAsync();
        Db.AddRange(new TournamentTeam { TournamentId = tournament.Id, TeamId = home.Id },
            new TournamentTeam { TournamentId = tournament.Id, TeamId = away.Id },
            new PlayerMembership { PlayerId = player.Id, TeamId = home.Id, JoinedOn = new(2026,10,1) });
        await Db.SaveChangesAsync();
        return (tournament, home, away, venue, player);
    }

    public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
}
