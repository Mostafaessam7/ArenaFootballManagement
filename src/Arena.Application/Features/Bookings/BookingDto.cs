using Arena.Application.Features.Tickets;

namespace Arena.Application.Features.Bookings;

public sealed record BookingDto(int Id, int TicketOfferId, int Quantity, decimal UnitPrice,
    decimal TotalAmount, string Currency, string Status, IReadOnlyList<TicketDto> Tickets);
