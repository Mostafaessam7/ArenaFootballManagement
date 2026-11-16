namespace Arena.Application.Features.Tickets;

/// <summary>FR19 and FR24: ticket offers (price, capacity, sales window) and gate check-in.</summary>
public sealed class TicketService(IUnitOfWork unitOfWork, ICompetitionQueries queries, TimeProvider clock)
    : ServiceBase(unitOfWork, clock)
{
    public Task<PageResult<TicketOffer>> OffersAsync(int eventId, int page, int pageSize, CancellationToken ct) =>
        PageAsync<TicketOffer>(page, pageSize, x => x.EventId == eventId, ct);

    /// <summary>The sum of all offers for an event can never exceed the venue capacity.</summary>
    public Task<TicketOffer> CreateOfferAsync(int eventId, TicketOfferRequest request, CancellationToken ct) =>
        Uow.SerializableAsync(async () =>
        {
            var item = await GetRequiredAsync<SportEvent>(eventId, ct);
            var venue = await GetRequiredAsync<Venue>(item.VenueId, ct);
            Guard.Against(item.Status != EventStatus.Scheduled || Now >= item.StartsAtUtc, "Ticket sales require a future scheduled event.");
            Guard.Against(request.Price < 0 || decimal.Round(request.Price, 2) != request.Price,
                "Price must be non-negative with at most two decimal places.");
            Guard.Against(request.Capacity < 1 || await queries.AllocatedCapacityAsync(eventId, ct) + request.Capacity > venue.Capacity,
                "Total ticket allocation cannot exceed venue capacity.");
            Guard.Against(request.SalesStartUtc.Kind != DateTimeKind.Utc || request.SalesEndUtc.Kind != DateTimeKind.Utc ||
                request.SalesEndUtc <= request.SalesStartUtc || request.SalesEndUtc > item.StartsAtUtc || request.SalesEndUtc <= Now,
                "Supply a valid UTC sales window ending no later than the event start.");
            var offer = new TicketOffer
            {
                EventId = eventId, Name = request.Name.Trim(), Price = request.Price,
                Capacity = request.Capacity, SalesStartUtc = request.SalesStartUtc, SalesEndUtc = request.SalesEndUtc
            };
            Repo<TicketOffer>().Add(offer);
            await Uow.SaveChangesAsync(ct);
            return offer;
        }, ct);

    /// <summary>Admission window: two hours before start until the event ends. Each ticket is used once.</summary>
    public Task<TicketDto> CheckInAsync(CheckInRequest request, int eventId, int actorId, CancellationToken ct) =>
        Uow.SerializableAsync(async () =>
        {
            var code = request.Code.ToUpperInvariant();
            var ticket = Guard.Found(await Repo<Ticket>().FindAsync(x => x.Code == code, ct));
            var booking = await GetRequiredAsync<Booking>(ticket.BookingId, ct);
            var offer = await GetRequiredAsync<TicketOffer>(booking.TicketOfferId, ct);
            Guard.Against(offer.EventId != eventId, "Ticket belongs to another event.");
            var item = await GetRequiredAsync<SportEvent>(eventId, ct);
            Guard.Against(booking.Status != BookingStatus.Confirmed || item.Status == EventStatus.Cancelled,
                "Ticket or event is cancelled.");
            Guard.Against(ticket.CheckedInUtc is not null, "Ticket has already been used.", "already_checked_in");
            Guard.Against(Now < item.StartsAtUtc.AddHours(-2) || Now > item.EndsAtUtc, "Check-in is outside the event admission window.");
            ticket.CheckedInUtc = Now;
            ticket.CheckedInById = actorId;
            await Uow.SaveChangesAsync(ct);
            return new TicketDto(ticket.Id, ticket.Code, ticket.CheckedInUtc);
        }, ct);
}
