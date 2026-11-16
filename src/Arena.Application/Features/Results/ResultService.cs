using Arena.Application.Features.Events;

namespace Arena.Application.Features.Results;

/// <summary>FR13 and FR15: approving or correcting match results (with audit) and player statistics.</summary>
public sealed class ResultService(IUnitOfWork unitOfWork, ICompetitionQueries queries, TimeProvider clock)
    : ServiceBase(unitOfWork, clock)
{
    public Task<PageResult<ResultAudit>> HistoryAsync(int matchId, int page, int pageSize, CancellationToken ct) =>
        PageAsync<ResultAudit>(page, pageSize, x => x.MatchId == matchId, ct);

    public Task<PageResult<MatchPlayerStat>> PlayerStatsAsync(int matchId, int page, int pageSize, CancellationToken ct) =>
        PageAsync<MatchPlayerStat>(page, pageSize, x => x.MatchId == matchId, ct);

    /// <summary>First approval and every correction write a ResultAudit row in the same transaction.</summary>
    public Task<EventDto> ApproveAsync(int matchId, ResultRequest request, int actorId, CancellationToken ct) =>
        Uow.SerializableAsync(async () =>
        {
            var match = await GetRequiredAsync<MatchEvent>(matchId, ct);
            Guard.Version(match, request.Version);
            Guard.Against(match.Status == EventStatus.Cancelled || Now < match.EndsAtUtc, "Only ended, non-cancelled matches can receive results.");
            Guard.Against((await GetRequiredAsync<Tournament>(match.TournamentId, ct)).Status != TournamentStatus.InProgress,
                "Tournament must be in progress before publishing results.");
            Guard.Against(request.HomeScore < 0 || request.AwayScore < 0 || request.HomeScore > 99 || request.AwayScore > 99,
                "Scores must be between 0 and 99.");
            Guard.Against(await queries.RecordedGoalsAsync(matchId, match.HomeTeamId, null, ct) > request.HomeScore ||
                await queries.RecordedGoalsAsync(matchId, match.AwayTeamId, null, ct) > request.AwayScore,
                "Correct player goal statistics before lowering the result.");
            Repo<ResultAudit>().Add(new ResultAudit
            {
                MatchId = matchId, ActorId = actorId,
                PreviousHomeScore = match.HomeScore, PreviousAwayScore = match.AwayScore,
                HomeScore = request.HomeScore, AwayScore = request.AwayScore, Reason = request.Reason.Trim()
            });
            match.HomeScore = request.HomeScore;
            match.AwayScore = request.AwayScore;
            match.Status = EventStatus.Finished;
            await Uow.SaveChangesAsync(ct);
            return EventService.ToDto(match);
        }, ct);

    /// <summary>One row per match and player; a second call replaces the values.</summary>
    public Task<MatchPlayerStat> SetPlayerStatAsync(int matchId, PlayerStatRequest request, CancellationToken ct) =>
        Uow.SerializableAsync(async () =>
        {
            var match = await GetRequiredAsync<MatchEvent>(matchId, ct);
            Guard.Against(match.Status != EventStatus.Finished, "Publish the match result before player statistics.");
            Guard.Against(request.TeamId != match.HomeTeamId && request.TeamId != match.AwayTeamId,
                "Player team must be a participant in this match.");
            Guard.Against(!await Repo<TournamentRoster>().AnyAsync(x => x.TournamentId == match.TournamentId &&
                x.TeamId == request.TeamId && x.PlayerId == request.PlayerId, ct), "Player is not in the tournament roster for this team.");
            var score = request.TeamId == match.HomeTeamId ? match.HomeScore!.Value : match.AwayScore!.Value;
            Guard.Against(await queries.RecordedGoalsAsync(matchId, request.TeamId, request.PlayerId, ct) + request.Goals > score,
                "Attributed player goals cannot exceed the team's result. Own goals may remain unattributed.");
            Guard.Against(request.Assists > score, "Assists cannot exceed the team's result.");
            var stat = await Repo<MatchPlayerStat>().FindAsync(x => x.MatchId == matchId && x.PlayerId == request.PlayerId, ct);
            if (stat is null)
            {
                stat = new MatchPlayerStat
                {
                    MatchId = matchId, TournamentId = match.TournamentId,
                    TeamId = request.TeamId, PlayerId = request.PlayerId
                };
                Repo<MatchPlayerStat>().Add(stat);
            }
            stat.Goals = request.Goals;
            stat.Assists = request.Assists;
            stat.YellowCards = request.YellowCards;
            stat.RedCards = request.RedCards;
            stat.MinutesPlayed = request.MinutesPlayed;
            await Uow.SaveChangesAsync(ct);
            return stat;
        }, ct);
}
