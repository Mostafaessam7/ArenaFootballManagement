namespace Arena.Application.Features.Events;

/// <summary>FR11, FR12 and FR18: scheduling matches and community events without venue or team overlaps.</summary>
public sealed class EventService(IUnitOfWork unitOfWork, ICompetitionQueries queries, TimeProvider clock)
    : ServiceBase(unitOfWork, clock)
{
    public static EventDto ToDto(SportEvent x) => new(x.Id, x is MatchEvent ? "Match" : "Community",
        x.Title, x.VenueId, x.StartsAtUtc, x.EndsAtUtc, x.Status.ToString(), (x as MatchEvent)?.TournamentId,
        (x as MatchEvent)?.HomeTeamId, (x as MatchEvent)?.AwayTeamId, (x as MatchEvent)?.HomeScore,
        (x as MatchEvent)?.AwayScore, x.Version);

    public async Task<PageResult<EventDto>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        var result = await PageAsync<SportEvent>(page, pageSize, null, ct);
        return new(result.Items.Select(ToDto).ToArray(), page, pageSize, result.Total);
    }

    public async Task<EventDto> GetAsync(int id, CancellationToken ct) => ToDto(await GetRequiredAsync<SportEvent>(id, ct));

    public Task<EventDto> CreateMatchAsync(MatchRequest request, int actorId, CancellationToken ct) =>
        Uow.SerializableAsync(async () =>
        {
            Guard.Against(request.HomeTeamId == request.AwayTeamId, "A team cannot play itself.");
            var tournament = await GetRequiredAsync<Tournament>(request.TournamentId, ct);
            Guard.Against(tournament.Status is TournamentStatus.Draft or TournamentStatus.Completed, "Tournament is not open for scheduling.");
            Guard.Against(DateOnly.FromDateTime(request.StartsAtUtc) < tournament.StartDate ||
                DateOnly.FromDateTime(request.EndsAtUtc) > tournament.EndDate, "Match must fall within tournament dates in UTC.");
            var home = await GetRequiredAsync<Team>(request.HomeTeamId, ct);
            var away = await GetRequiredAsync<Team>(request.AwayTeamId, ct);
            Guard.Against(!home.IsActive || !away.IsActive, "Both teams must be active.");
            Guard.Against(!await Repo<TournamentTeam>().AnyAsync(x => x.TournamentId == request.TournamentId && x.TeamId == home.Id, ct) ||
                !await Repo<TournamentTeam>().AnyAsync(x => x.TournamentId == request.TournamentId && x.TeamId == away.Id, ct),
                "Both teams must be registered in this tournament.");
            await ValidateScheduleAsync(request.VenueId, request.StartsAtUtc, request.EndsAtUtc, home.Id, away.Id, ct);
            var match = new MatchEvent
            {
                Title = $"{home.Name} vs {away.Name}", TournamentId = tournament.Id,
                HomeTeamId = home.Id, AwayTeamId = away.Id, VenueId = request.VenueId, OrganizerId = actorId,
                StartsAtUtc = request.StartsAtUtc, EndsAtUtc = request.EndsAtUtc,
                RoundNumber = request.RoundNumber, Leg = request.Leg
            };
            Repo<MatchEvent>().Add(match);
            await Uow.SaveChangesAsync(ct);
            return ToDto(match);
        }, ct);

    public Task<EventDto> CreateCommunityAsync(CommunityEventRequest request, int actorId, CancellationToken ct) =>
        Uow.SerializableAsync(async () =>
        {
            await ValidateScheduleAsync(request.VenueId, request.StartsAtUtc, request.EndsAtUtc, null, null, ct);
            var item = new CommunityEvent
            {
                Title = request.Title.Trim(), Description = request.Description.Trim(),
                VenueId = request.VenueId, OrganizerId = actorId,
                StartsAtUtc = request.StartsAtUtc, EndsAtUtc = request.EndsAtUtc
            };
            Repo<CommunityEvent>().Add(item);
            await Uow.SaveChangesAsync(ct);
            return ToDto(item);
        }, ct);

    /// <summary>Two events overlap when each starts before the other ends; back-to-back is allowed.</summary>
    private async Task ValidateScheduleAsync(int venueId, DateTime start, DateTime end,
        int? home, int? away, CancellationToken ct)
    {
        Guard.Against(start.Kind != DateTimeKind.Utc || end.Kind != DateTimeKind.Utc || end <= start,
            "Supply UTC timestamps ending in Z; event end must follow start.");
        await GetRequiredAsync<Venue>(venueId, ct);
        Guard.Against(await queries.HasScheduleConflictAsync(venueId, start, end, home, away, ct),
            "Venue or team already has an overlapping event.", "schedule_conflict");
    }
}
