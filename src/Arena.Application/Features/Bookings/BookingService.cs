using System.Security.Cryptography;

namespace Arena.Application.Features.Bookings;

/// <summary>FR20 to FR23: booking tickets (idempotent), reading own bookings and cancelling.</summary>
public sealed class BookingService(IUnitOfWork unitOfWork, ICompetitionQueries queries, TimeProvider clock)
    : ServiceBase(unitOfWork, clock)
{
    public Task<PageResult<Booking>> MineAsync(int userId, int page, int pageSize, CancellationToken ct) =>
        PageAsync<Booking>(page, pageSize, x => x.UserId == userId, ct);

    /// <summary>Another user's booking returns 404 so its existence is not revealed.</summary>
    public async Task<BookingDto> GetAsync(int id, int userId, bool admin, CancellationToken ct)
    {
        var booking = await GetRequiredAsync<Booking>(id, ct);
        if (!admin && booking.UserId != userId) throw new BusinessException(404, "not_found", "Booking was not found.");
        return await ToDtoAsync(booking, ct);
    }

    /// <summary>
    /// Stock check, stock update, booking and tickets are saved in one transaction.
    /// Repeating the same key and body returns the first booking; a different body is rejected.
    /// </summary>
    public Task<BookingDto> BookAsync(CreateBookingRequest request, int userId, CancellationToken ct) =>
        Uow.SerializableAsync(async () =>
        {
            Guard.Against(request.Quantity is < 1 or > 10, "Quantity must be between 1 and 10.");
            var previous = await Repo<Booking>().FindAsync(x => x.UserId == userId && x.IdempotencyKey == request.IdempotencyKey, ct);
            if (previous is not null)
            {
                Guard.Against(previous.TicketOfferId != request.TicketOfferId || previous.Quantity != request.Quantity,
                    "Idempotency key was already used with a different request.", "idempotency_conflict");
                return await ToDtoAsync(previous, ct);
            }
            var offer = await GetRequiredAsync<TicketOffer>(request.TicketOfferId, ct);
            var item = await GetRequiredAsync<SportEvent>(offer.EventId, ct);
            Guard.Against(item.Status != EventStatus.Scheduled || Now >= item.StartsAtUtc ||
                Now < offer.SalesStartUtc || Now >= offer.SalesEndUtc, "Ticket sales are closed.");
            Guard.Against(offer.Capacity - offer.SoldQuantity < request.Quantity, "Not enough tickets remain.", "sold_out");
            offer.SoldQuantity += request.Quantity;
            var booking = new Booking
            {
                UserId = userId, TicketOfferId = offer.Id, Quantity = request.Quantity,
                UnitPrice = offer.Price, IdempotencyKey = request.IdempotencyKey
            };
            Repo<Booking>().Add(booking);
            await Uow.SaveChangesAsync(ct);
            for (var i = 0; i < request.Quantity; i++)
                Repo<Ticket>().Add(new Ticket { BookingId = booking.Id, Code = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) });
            await Uow.SaveChangesAsync(ct);
            return await ToDtoAsync(booking, ct);
        }, ct);

    /// <summary>Allowed until two hours before the event and only if no ticket was used. Stock returns once.</summary>
    public Task<BookingDto> CancelAsync(int id, int userId, bool admin, CancellationToken ct) =>
        Uow.SerializableAsync(async () =>
        {
            var booking = await GetRequiredAsync<Booking>(id, ct);
            if (!admin && booking.UserId != userId) throw new BusinessException(404, "not_found", "Booking was not found.");
            if (booking.Status == BookingStatus.Cancelled) return await ToDtoAsync(booking, ct);
            var offer = await GetRequiredAsync<TicketOffer>(booking.TicketOfferId, ct);
            var item = await GetRequiredAsync<SportEvent>(offer.EventId, ct);
            Guard.Against(Now >= item.StartsAtUtc.AddHours(-2), "Cancellations close two hours before the event.");
            Guard.Against(await Repo<Ticket>().AnyAsync(x => x.BookingId == id && x.CheckedInUtc != null, ct),
                "A checked-in booking cannot be cancelled.");
            booking.Status = BookingStatus.Cancelled;
            booking.CancelledUtc = Now;
            offer.SoldQuantity -= booking.Quantity;
            await Uow.SaveChangesAsync(ct);
            return await ToDtoAsync(booking, ct);
        }, ct);

    private async Task<BookingDto> ToDtoAsync(Booking b, CancellationToken ct) =>
        new(b.Id, b.TicketOfferId, b.Quantity, b.UnitPrice, b.Quantity * b.UnitPrice, "EGP", b.Status.ToString(),
            await queries.TicketsAsync(b.Id, ct));
}
