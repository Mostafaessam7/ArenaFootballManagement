using Arena.Application.Features.Tournaments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arena.Api.Controllers;

[ApiController, Route("api/v1/tournaments")]
public sealed class TournamentsController(TournamentService tournaments) : ArenaController
{
    [HttpGet]
    public Task<PageResult<Tournament>> List(int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        tournaments.ListAsync(page, pageSize, ct);

    [HttpGet("{id:int}")]
    public Task<Tournament> Get(int id, CancellationToken ct) => tournaments.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = Management)]
    public async Task<ActionResult<Tournament>> Create(TournamentRequest request, CancellationToken ct)
    {
        var item = await tournaments.CreateAsync(request, ActorId, ct);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPost("{id:int}/teams"), Authorize(Roles = Management)]
    public Task<TournamentTeam> Register(int id, RegisterTeamRequest request, CancellationToken ct) =>
        tournaments.RegisterTeamAsync(id, request, ct);

    [HttpGet("{id:int}/teams")]
    public Task<PageResult<TournamentTeam>> Teams(int id, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        tournaments.TeamsAsync(id, page, pageSize, ct);

    [HttpPost("{id:int}/roster"), Authorize(Roles = Management)]
    public Task<TournamentRoster> AddRoster(int id, RosterRequest request, CancellationToken ct) =>
        tournaments.AddRosterAsync(id, request, ct);

    [HttpGet("{id:int}/roster"), Authorize(Roles = Management)]
    public Task<PageResult<TournamentRoster>> Roster(int id, int page = 1, int pageSize = 50, CancellationToken ct = default) =>
        tournaments.RosterAsync(id, page, pageSize, ct);

    [HttpPost("{id:int}/start"), Authorize(Roles = Management)]
    public Task<Tournament> Start(int id, Guid version, CancellationToken ct) => tournaments.StartAsync(id, version, ct);

    [HttpGet("{id:int}/standings")]
    public Task<IReadOnlyList<StandingRow>> Standings(int id, CancellationToken ct) => tournaments.StandingsAsync(id, ct);

    [HttpGet("{id:int}/scorers")]
    public Task<IReadOnlyList<ScorerRow>> Scorers(int id, CancellationToken ct) => tournaments.ScorersAsync(id, ct);
}
