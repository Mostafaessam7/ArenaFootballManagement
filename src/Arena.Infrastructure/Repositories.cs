using System.Data;
using System.Linq.Expressions;
using Arena.Application;
using Arena.Domain;
using Microsoft.EntityFrameworkCore;

namespace Arena.Infrastructure;

public sealed class EfRepository<T>(ArenaDbContext db) : IRepository<T> where T : Entity
{
    public Task<T?> GetAsync(int id, CancellationToken ct = default) =>
        db.Set<T>().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<T?> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        db.Set<T>().SingleOrDefaultAsync(predicate, ct);
    public async Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null,
        int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        if (page < 1 || page > 1000000 || pageSize is < 1 or > 100)
            throw new BusinessException(400, "invalid_pagination", "Page must be 1..1000000 and pageSize 1..100.");
        IQueryable<T> query = db.Set<T>().AsNoTracking();
        if (predicate is not null) query = query.Where(predicate);
        return await query.OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    }
    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => db.Set<T>().AnyAsync(predicate, ct);
    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default) =>
        predicate is null ? db.Set<T>().CountAsync(ct) : db.Set<T>().CountAsync(predicate, ct);
    public void Add(T entity) => db.Set<T>().Add(entity);
    public void Remove(T entity) => db.Set<T>().Remove(entity);
}

public sealed class UnitOfWork(ArenaDbContext db) : IUnitOfWork
{
    private readonly Dictionary<Type, object> repositories = [];
    public IRepository<T> Repository<T>() where T : Entity
    {
        if (!repositories.TryGetValue(typeof(T), out var repository))
            repositories[typeof(T)] = repository = new EfRepository<T>(db);
        return (IRepository<T>)repository;
    }
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
    public async Task<T> SerializableAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is not null) return await action();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var result = await action();
        await transaction.CommitAsync(ct);
        return result;
    }
}

public sealed class CompetitionQueries(ArenaDbContext db) : ICompetitionQueries
{
    public async Task<IReadOnlyList<StandingRow>> StandingsAsync(int tournamentId, CancellationToken ct)
    {
        var teams = await (from registration in db.TournamentTeams.AsNoTracking()
                           join team in db.Teams on registration.TeamId equals team.Id
                           where registration.TournamentId == tournamentId
                           select new { team.Id, team.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var matches = await db.Set<MatchEvent>().AsNoTracking()
            .Where(x => x.TournamentId == tournamentId && x.Status == EventStatus.Finished).ToListAsync(ct);
        return StandingsCalculator.Calculate(teams, matches);
    }
    public async Task<IReadOnlyList<ScorerRow>> TopScorersAsync(int tournamentId, CancellationToken ct) =>
        await (from stat in db.MatchPlayerStats.AsNoTracking()
               join match in db.Set<MatchEvent>() on stat.MatchId equals match.Id
               join player in db.Players on stat.PlayerId equals player.Id
               where match.TournamentId == tournamentId && match.Status == EventStatus.Finished
               group stat by new { player.Id, player.FullName } into grouped
               select new { PlayerId = grouped.Key.Id, PlayerName = grouped.Key.FullName,
                   Goals = grouped.Sum(x => x.Goals), Assists = grouped.Sum(x => x.Assists),
                   Appearances = grouped.Count(x => x.MinutesPlayed > 0) })
            .OrderByDescending(x => x.Goals).ThenByDescending(x => x.Assists).ThenBy(x => x.PlayerId)
            .Take(50).Select(x => new ScorerRow(x.PlayerId, x.PlayerName, x.Goals, x.Assists, x.Appearances))
            .ToListAsync(ct);
    public Task<bool> HasScheduleConflictAsync(int venueId, DateTime start, DateTime end,
        int? homeTeamId, int? awayTeamId, CancellationToken ct) => db.Events.AnyAsync(x =>
            x.Status != EventStatus.Cancelled && x.StartsAtUtc < end && start < x.EndsAtUtc &&
            (x.VenueId == venueId || (x is MatchEvent &&
                (((MatchEvent)x).HomeTeamId == homeTeamId || ((MatchEvent)x).HomeTeamId == awayTeamId ||
                 ((MatchEvent)x).AwayTeamId == homeTeamId || ((MatchEvent)x).AwayTeamId == awayTeamId))), ct);
    public Task<int> RecordedGoalsAsync(int matchId, int teamId, int? excludePlayerId, CancellationToken ct) =>
        db.MatchPlayerStats.Where(x => x.MatchId == matchId && x.TeamId == teamId &&
            (excludePlayerId == null || x.PlayerId != excludePlayerId)).SumAsync(x => x.Goals, ct);
    public Task<int> AllocatedCapacityAsync(int eventId, CancellationToken ct) =>
        db.TicketOffers.Where(x => x.EventId == eventId).SumAsync(x => x.Capacity, ct);
    public async Task<IReadOnlyList<TicketDto>> TicketsAsync(int bookingId, CancellationToken ct) =>
        await db.Tickets.AsNoTracking().Where(x => x.BookingId == bookingId).OrderBy(x => x.Id)
            .Select(x => new TicketDto(x.Id, x.Code, x.CheckedInUtc)).ToListAsync(ct);
}
