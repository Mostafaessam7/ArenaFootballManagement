using Arena.Application.Features.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arena.Api.Controllers;

/// <summary>Ticket offers of an event and gate check-in. Routes live under the event they belong to.</summary>
[ApiController, Route("api/v1/events")]
public sealed class TicketsController(TicketService tickets) : ArenaController
{
    [HttpPost("{id:int}/ticket-offers"), Authorize(Roles = Management)]
    public Task<TicketOffer> CreateOffer(int id, TicketOfferRequest request, CancellationToken ct) =>
        tickets.CreateOfferAsync(id, request, ct);

    [HttpGet("{id:int}/ticket-offers")]
    public Task<PageResult<TicketOffer>> Offers(int id, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        tickets.OffersAsync(id, page, pageSize, ct);

    [HttpPost("{id:int}/check-in"), Authorize(Roles = "Admin,GateStaff")]
    public Task<TicketDto> CheckIn(int id, CheckInRequest request, CancellationToken ct) =>
        tickets.CheckInAsync(request, id, ActorId, ct);
}
