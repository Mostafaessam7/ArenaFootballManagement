using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Arena.Application;
using Arena.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Arena.Api;

public abstract class ArenaController : ControllerBase
{
    protected const string Management = "Admin,Organizer";
    protected int ActorId => int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}

[ApiController, Route("api/v1/auth"), EnableRateLimiting("auth")]
public sealed class AuthController(AuthService auth) : ArenaController
{
    [HttpPost("register")]
    public Task<AuthResponse> Register(RegisterRequest request, CancellationToken ct) => auth.RegisterAsync(request, ct);
    [HttpPost("login")]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);
}

[ApiController, Route("api/v1/teams")]
public sealed class TeamsController(ArenaService service) : ArenaController
{
    [HttpGet]
    public async Task<PageResult<TeamDto>> List(int page = 1, int pageSize = 20, CancellationToken ct = default)
    { var result = await service.ListAsync<Team>(page, pageSize, ct: ct); return new(result.Items.Select(ArenaService.TeamView).ToArray(), page, pageSize, result.Total); }
    [HttpGet("{id:int}")]
    public async Task<TeamDto> Get(int id, CancellationToken ct) => ArenaService.TeamView(await service.GetAsync<Team>(id, ct));
    [HttpPost, Authorize(Roles = Management)]
    public async Task<ActionResult<TeamDto>> Create(TeamRequest request, CancellationToken ct)
    { var team = await service.CreateTeamAsync(request, ct); return CreatedAtAction(nameof(Get), new { id = team.Id }, team); }
    [HttpPut("{id:int}"), Authorize(Roles = Management)]
    public Task<TeamDto> Update(int id, UpdateTeamRequest request, CancellationToken ct) => service.UpdateTeamAsync(id, request, ct);
    [HttpDelete("{id:int}"), Authorize(Roles = Management)]
    public async Task<IActionResult> Archive(int id, Guid version, CancellationToken ct)
    { await service.ArchiveTeamAsync(id, version, ct); return NoContent(); }
}

[ApiController, Route("api/v1/players"), Authorize(Roles = Management)]
public sealed class PlayersController(ArenaService service) : ArenaController
{
    [HttpGet]
    public Task<PageResult<Player>> List(int page = 1, int pageSize = 20, CancellationToken ct = default) => service.ListAsync<Player>(page, pageSize, ct: ct);
    [HttpGet("{id:int}")]
    public Task<Player> Get(int id, CancellationToken ct) => service.GetAsync<Player>(id, ct);
    [HttpPost]
    public async Task<ActionResult<Player>> Create(PlayerRequest request, CancellationToken ct)
    { var player = await service.CreatePlayerAsync(request, ct); return CreatedAtAction(nameof(Get), new { id = player.Id }, player); }
    [HttpPost("{id:int}/memberships")]
    public Task<PlayerMembership> Transfer(int id, MembershipRequest request, CancellationToken ct) => service.TransferAsync(id, request, ct);
    [HttpGet("{id:int}/memberships")]
    public Task<PageResult<PlayerMembership>> History(int id, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        service.ListAsync<PlayerMembership>(page, pageSize, x => x.PlayerId == id, ct);
}

[ApiController, Route("api/v1/tournaments")]
public sealed class TournamentsController(ArenaService service) : ArenaController
{
    [HttpGet]
    public Task<PageResult<Tournament>> List(int page = 1, int pageSize = 20, CancellationToken ct = default) => service.ListAsync<Tournament>(page, pageSize, ct: ct);
    [HttpGet("{id:int}")]
    public Task<Tournament> Get(int id, CancellationToken ct) => service.GetAsync<Tournament>(id, ct);
    [HttpPost, Authorize(Roles = Management)]
    public async Task<ActionResult<Tournament>> Create(TournamentRequest request, CancellationToken ct)
    { var item = await service.CreateTournamentAsync(request, ActorId, ct); return CreatedAtAction(nameof(Get), new { id = item.Id }, item); }
    [HttpPost("{id:int}/teams"), Authorize(Roles = Management)]
    public Task<TournamentTeam> Register(int id, RegisterTeamRequest request, CancellationToken ct) => service.RegisterTeamAsync(id, request, ct);
    [HttpGet("{id:int}/teams")]
    public Task<PageResult<TournamentTeam>> Teams(int id, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        service.ListAsync<TournamentTeam>(page, pageSize, x => x.TournamentId == id, ct);
    [HttpPost("{id:int}/roster"), Authorize(Roles = Management)]
    public Task<TournamentRoster> AddRoster(int id, RosterRequest request, CancellationToken ct) => service.AddRosterAsync(id, request, ct);
    [HttpGet("{id:int}/roster"), Authorize(Roles = Management)]
    public Task<PageResult<TournamentRoster>> Roster(int id, int page = 1, int pageSize = 50, CancellationToken ct = default) =>
        service.ListAsync<TournamentRoster>(page, pageSize, x => x.TournamentId == id, ct);
    [HttpPost("{id:int}/start"), Authorize(Roles = Management)]
    public Task<Tournament> Start(int id, Guid version, CancellationToken ct) => service.StartTournamentAsync(id, version, ct);
    [HttpGet("{id:int}/standings")]
    public Task<IReadOnlyList<StandingRow>> Standings(int id, CancellationToken ct) => service.StandingsAsync(id, ct);
    [HttpGet("{id:int}/scorers")]
    public Task<IReadOnlyList<ScorerRow>> Scorers(int id, CancellationToken ct) => service.ScorersAsync(id, ct);
}

[ApiController, Route("api/v1/venues")]
public sealed class VenuesController(ArenaService service) : ArenaController
{
    [HttpGet]
    public Task<PageResult<Venue>> List(int page = 1, int pageSize = 20, CancellationToken ct = default) => service.ListAsync<Venue>(page, pageSize, ct: ct);
    [HttpPost, Authorize(Roles = Management)]
    public async Task<ActionResult<Venue>> Create(VenueRequest request, CancellationToken ct)
    { var venue = await service.CreateVenueAsync(request, ct); return Created($"/api/v1/venues/{venue.Id}", venue); }
    [HttpGet("{id:int}")]
    public Task<Venue> Get(int id, CancellationToken ct) => service.GetAsync<Venue>(id, ct);
}

[ApiController, Route("api/v1/events")]
public sealed class EventsController(ArenaService service) : ArenaController
{
    [HttpGet]
    public async Task<PageResult<EventDto>> List(int page = 1, int pageSize = 20, CancellationToken ct = default)
    { var result = await service.ListAsync<SportEvent>(page, pageSize, ct: ct); return new(result.Items.Select(ArenaService.EventView).ToArray(), page, pageSize, result.Total); }
    [HttpGet("{id:int}")]
    public async Task<EventDto> Get(int id, CancellationToken ct) => ArenaService.EventView(await service.GetAsync<SportEvent>(id, ct));
    [HttpPost("matches"), Authorize(Roles = Management)]
    public async Task<ActionResult<EventDto>> CreateMatch(MatchRequest request, CancellationToken ct)
    { var item = await service.CreateMatchAsync(request, ActorId, ct); return CreatedAtAction(nameof(Get), new { id = item.Id }, item); }
    [HttpPost("community"), Authorize(Roles = Management)]
    public async Task<ActionResult<EventDto>> CreateCommunity(CommunityEventRequest request, CancellationToken ct)
    { var item = await service.CreateCommunityAsync(request, ActorId, ct); return CreatedAtAction(nameof(Get), new { id = item.Id }, item); }
    [HttpPut("{id:int}/result"), Authorize(Roles = Management)]
    public Task<EventDto> Result(int id, ResultRequest request, CancellationToken ct) => service.ApproveResultAsync(id, request, ActorId, ct);
    [HttpGet("{id:int}/result-history"), Authorize(Roles = Management)]
    public Task<PageResult<ResultAudit>> ResultHistory(int id, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        service.ListAsync<ResultAudit>(page, pageSize, x => x.MatchId == id, ct);
    [HttpPut("{id:int}/player-stats"), Authorize(Roles = Management)]
    public Task<MatchPlayerStat> Stats(int id, PlayerStatRequest request, CancellationToken ct) => service.SetPlayerStatAsync(id, request, ct);
    [HttpGet("{id:int}/player-stats")]
    public Task<PageResult<MatchPlayerStat>> Stats(int id, int page = 1, int pageSize = 50, CancellationToken ct = default) =>
        service.ListAsync<MatchPlayerStat>(page, pageSize, x => x.MatchId == id, ct);
    [HttpPost("{id:int}/ticket-offers"), Authorize(Roles = Management)]
    public Task<TicketOffer> CreateOffer(int id, TicketOfferRequest request, CancellationToken ct) => service.CreateOfferAsync(id, request, ct);
    [HttpGet("{id:int}/ticket-offers")]
    public Task<PageResult<TicketOffer>> Offers(int id, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        service.ListAsync<TicketOffer>(page, pageSize, x => x.EventId == id, ct);
    [HttpPost("{id:int}/check-in"), Authorize(Roles = "Admin,GateStaff")]
    public Task<TicketDto> CheckIn(int id, CheckInRequest request, CancellationToken ct) => service.CheckInAsync(request, id, ActorId, ct);
}

[ApiController, Route("api/v1/bookings"), Authorize]
public sealed class BookingsController(ArenaService service) : ArenaController
{
    [HttpPost]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingRequest request, CancellationToken ct)
    { var booking = await service.BookAsync(request, ActorId, ct); return CreatedAtAction(nameof(Get), new { id = booking.Id }, booking); }
    [HttpGet("{id:int}")]
    public Task<BookingDto> Get(int id, CancellationToken ct) => service.GetBookingAsync(id, ActorId, User.IsInRole("Admin"), ct);
    [HttpGet("mine")]
    public Task<PageResult<Booking>> Mine(int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        service.ListAsync<Booking>(page, pageSize, x => x.UserId == ActorId, ct);
    [HttpPost("{id:int}/cancel")]
    public Task<BookingDto> Cancel(int id, CancellationToken ct) => service.CancelBookingAsync(id, ActorId, User.IsInRole("Admin"), ct);
}
