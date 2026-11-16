using Arena.Application.Features.Tickets;
using Arena.Application.Features.Tournaments;
using Arena.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Arena.Infrastructure.Queries;

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
               select new
               {
                   PlayerId = grouped.Key.Id, PlayerName = grouped.Key.FullName,
                   Goals = grouped.Sum(x => x.Goals), Assists = grouped.Sum(x => x.Assists),
                   Appearances = grouped.Count(x => x.MinutesPlayed > 0)
               })
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
