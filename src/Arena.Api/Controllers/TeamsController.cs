using Arena.Application.Features.Teams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arena.Api.Controllers;

[ApiController, Route("api/v1/teams")]
public sealed class TeamsController(TeamService teams) : ArenaController
{
    [HttpGet]
    public Task<PageResult<TeamDto>> List(int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        teams.ListAsync(page, pageSize, ct);

    [HttpGet("{id:int}")]
    public Task<TeamDto> Get(int id, CancellationToken ct) => teams.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = Management)]
    public async Task<ActionResult<TeamDto>> Create(TeamRequest request, CancellationToken ct)
    {
        var team = await teams.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = team.Id }, team);
    }

    [HttpPut("{id:int}"), Authorize(Roles = Management)]
    public Task<TeamDto> Update(int id, UpdateTeamRequest request, CancellationToken ct) => teams.UpdateAsync(id, request, ct);

    [HttpDelete("{id:int}"), Authorize(Roles = Management)]
    public async Task<IActionResult> Archive(int id, Guid version, CancellationToken ct)
    {
        await teams.ArchiveAsync(id, version, ct);
        return NoContent();
    }
}
