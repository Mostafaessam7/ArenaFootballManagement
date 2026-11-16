using Arena.Application.Features.Bookings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arena.Api.Controllers;

[ApiController, Route("api/v1/bookings"), Authorize]
public sealed class BookingsController(BookingService bookings) : ArenaController
{
    [HttpPost]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingRequest request, CancellationToken ct)
    {
        var booking = await bookings.BookAsync(request, ActorId, ct);
        return CreatedAtAction(nameof(Get), new { id = booking.Id }, booking);
    }

    [HttpGet("{id:int}")]
    public Task<BookingDto> Get(int id, CancellationToken ct) =>
        bookings.GetAsync(id, ActorId, User.IsInRole("Admin"), ct);

    [HttpGet("mine")]
    public Task<PageResult<Booking>> Mine(int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        bookings.MineAsync(ActorId, page, pageSize, ct);

    [HttpPost("{id:int}/cancel")]
    public Task<BookingDto> Cancel(int id, CancellationToken ct) =>
        bookings.CancelAsync(id, ActorId, User.IsInRole("Admin"), ct);
}
