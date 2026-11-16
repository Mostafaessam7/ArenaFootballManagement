using Arena.Application.Features.Tickets;
using Arena.Application.Features.Tournaments;

namespace Arena.Application.Interfaces;

/// <summary>Specialised read queries (aggregations and joins) that do not fit the generic repository.</summary>
public interface ICompetitionQueries
{
    Task<IReadOnlyList<StandingRow>> StandingsAsync(int tournamentId, CancellationToken ct);
    Task<IReadOnlyList<ScorerRow>> TopScorersAsync(int tournamentId, CancellationToken ct);
    Task<bool> HasScheduleConflictAsync(int venueId, DateTime start, DateTime end,
        int? homeTeamId, int? awayTeamId, CancellationToken ct);
    Task<int> RecordedGoalsAsync(int matchId, int teamId, int? excludePlayerId, CancellationToken ct);
    Task<int> AllocatedCapacityAsync(int eventId, CancellationToken ct);
    Task<IReadOnlyList<TicketDto>> TicketsAsync(int bookingId, CancellationToken ct);
}
