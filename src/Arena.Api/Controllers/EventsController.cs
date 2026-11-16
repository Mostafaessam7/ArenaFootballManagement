using Arena.Application.Features.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arena.Api.Controllers;

[ApiController, Route("api/v1/events")]
public sealed class EventsController(EventService events) : ArenaController
{
    [HttpGet]
    public Task<PageResult<EventDto>> List(int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        events.ListAsync(page, pageSize, ct);

    [HttpGet("{id:int}")]
    public Task<EventDto> Get(int id, CancellationToken ct) => events.GetAsync(id, ct);

    [HttpPost("matches"), Authorize(Roles = Management)]
    public async Task<ActionResult<EventDto>> CreateMatch(MatchRequest request, CancellationToken ct)
    {
        var item = await events.CreateMatchAsync(request, ActorId, ct);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPost("community"), Authorize(Roles = Management)]
    public async Task<ActionResult<EventDto>> CreateCommunity(CommunityEventRequest request, CancellationToken ct)
    {
        var item = await events.CreateCommunityAsync(request, ActorId, ct);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }
}
