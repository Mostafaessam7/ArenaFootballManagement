namespace Arena.Application.Features.Tournaments;

/// <summary>FR07 to FR10, FR14 and FR16: tournaments, team registration, rosters, start, standings and scorers.</summary>
public sealed class TournamentService(IUnitOfWork unitOfWork, ICompetitionQueries queries, TimeProvider clock)
    : ServiceBase(unitOfWork, clock)
{
    public Task<PageResult<Tournament>> ListAsync(int page, int pageSize, CancellationToken ct) =>
        PageAsync<Tournament>(page, pageSize, null, ct);

    public Task<Tournament> GetAsync(int id, CancellationToken ct) => GetRequiredAsync<Tournament>(id, ct);

    public Task<PageResult<TournamentTeam>> TeamsAsync(int tournamentId, int page, int pageSize, CancellationToken ct) =>
        PageAsync<TournamentTeam>(page, pageSize, x => x.TournamentId == tournamentId, ct);

    public Task<PageResult<TournamentRoster>> RosterAsync(int tournamentId, int page, int pageSize, CancellationToken ct) =>
        PageAsync<TournamentRoster>(page, pageSize, x => x.TournamentId == tournamentId, ct);

    public async Task<Tournament> CreateAsync(TournamentRequest request, int actorId, CancellationToken ct)
    {
        Guard.Against(request.EndDate < request.StartDate, "Tournament end date must not precede its start date.");
        var tournament = new Tournament
        {
            Name = request.Name.Trim(), Season = request.Season.Trim(),
            StartDate = request.StartDate, EndDate = request.EndDate, OrganizerId = actorId
        };
        Repo<Tournament>().Add(tournament);
        await Uow.SaveChangesAsync(ct);
        return tournament;
    }

    public Task<TournamentTeam> RegisterTeamAsync(int tournamentId, RegisterTeamRequest request, CancellationToken ct) =>
        Uow.SerializableAsync(async () =>
        {
            var tournament = await GetRequiredAsync<Tournament>(tournamentId, ct);
            Guard.Against(tournament.Status != TournamentStatus.RegistrationOpen, "Registration is closed.");
            Guard.Against(!(await GetRequiredAsync<Team>(request.TeamId, ct)).IsActive, "Team is archived.");
            Guard.Against(await Repo<TournamentTeam>().AnyAsync(x => x.TournamentId == tournamentId && x.TeamId == request.TeamId, ct),
                "Team is already registered.");
            var item = new TournamentTeam { TournamentId = tournamentId, TeamId = request.TeamId };
            Repo<TournamentTeam>().Add(item);
            await Uow.SaveChangesAsync(ct);
            return item;
        }, ct);

    public Task<TournamentRoster> AddRosterAsync(int tournamentId, RosterRequest request, CancellationToken ct) =>
        Uow.SerializableAsync(async () =>
        {
            var tournament = await GetRequiredAsync<Tournament>(tournamentId, ct);
            Guard.Against(tournament.Status != TournamentStatus.RegistrationOpen, "Rosters are locked after registration closes.");
            Guard.Against(!await Repo<TournamentTeam>().AnyAsync(x => x.TournamentId == tournamentId && x.TeamId == request.TeamId, ct),
                "Team is not registered in this tournament.");
            await GetRequiredAsync<Player>(request.PlayerId, ct);
            Guard.Against(!await Repo<PlayerMembership>().AnyAsync(x => x.PlayerId == request.PlayerId &&
                x.TeamId == request.TeamId && x.LeftOn == null, ct), "Player must have an active membership in this team.");
            var roster = new TournamentRoster
            {
                TournamentId = tournamentId, TeamId = request.TeamId,
                PlayerId = request.PlayerId, ShirtNumber = request.ShirtNumber
            };
            Repo<TournamentRoster>().Add(roster);
            await Uow.SaveChangesAsync(ct);
            return roster;
        }, ct);

    /// <summary>Needs two registered teams and one scheduled match; closes registration and rosters.</summary>
    public Task<Tournament> StartAsync(int id, Guid version, CancellationToken ct) =>
        Uow.SerializableAsync(async () =>
        {
            var tournament = await GetRequiredAsync<Tournament>(id, ct);
            Guard.Version(tournament, version);
            Guard.Against(tournament.Status != TournamentStatus.RegistrationOpen, "Tournament must be open for registration.");
            Guard.Against(await Repo<TournamentTeam>().CountAsync(x => x.TournamentId == id, ct) < 2, "Register at least two teams first.");
            Guard.Against(!await Repo<MatchEvent>().AnyAsync(x => x.TournamentId == id, ct), "Schedule at least one match first.");
            tournament.Status = TournamentStatus.InProgress;
            await Uow.SaveChangesAsync(ct);
            return tournament;
        }, ct);

    public async Task<IReadOnlyList<StandingRow>> StandingsAsync(int id, CancellationToken ct)
    {
        await GetRequiredAsync<Tournament>(id, ct);
        return await queries.StandingsAsync(id, ct);
    }

    public async Task<IReadOnlyList<ScorerRow>> ScorersAsync(int id, CancellationToken ct)
    {
        await GetRequiredAsync<Tournament>(id, ct);
        return await queries.TopScorersAsync(id, ct);
    }
}
