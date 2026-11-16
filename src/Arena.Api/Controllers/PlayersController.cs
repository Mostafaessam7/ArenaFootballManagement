using Arena.Application.Features.Players;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arena.Api.Controllers;

[ApiController, Route("api/v1/players"), Authorize(Roles = Management)]
public sealed class PlayersController(PlayerService players) : ArenaController
{
    [HttpGet]
    public Task<PageResult<Player>> List(int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        players.ListAsync(page, pageSize, ct);

    [HttpGet("{id:int}")]
    public Task<Player> Get(int id, CancellationToken ct) => players.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<Player>> Create(PlayerRequest request, CancellationToken ct)
    {
        var player = await players.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = player.Id }, player);
    }

    [HttpPost("{id:int}/memberships")]
    public Task<PlayerMembership> Transfer(int id, MembershipRequest request, CancellationToken ct) =>
        players.TransferAsync(id, request, ct);

    [HttpGet("{id:int}/memberships")]
    public Task<PageResult<PlayerMembership>> History(int id, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        players.MembershipsAsync(id, page, pageSize, ct);
}
