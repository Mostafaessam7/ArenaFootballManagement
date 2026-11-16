using Arena.Application.Features.Venues;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arena.Api.Controllers;

[ApiController, Route("api/v1/venues")]
public sealed class VenuesController(VenueService venues) : ArenaController
{
    [HttpGet]
    public Task<PageResult<Venue>> List(int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        venues.ListAsync(page, pageSize, ct);

    [HttpPost, Authorize(Roles = Management)]
    public async Task<ActionResult<Venue>> Create(VenueRequest request, CancellationToken ct)
    {
        var venue = await venues.CreateAsync(request, ct);
        return Created($"/api/v1/venues/{venue.Id}", venue);
    }

    [HttpGet("{id:int}")]
    public Task<Venue> Get(int id, CancellationToken ct) => venues.GetAsync(id, ct);
}
