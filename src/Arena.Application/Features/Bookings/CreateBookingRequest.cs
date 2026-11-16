using System.ComponentModel.DataAnnotations;

namespace Arena.Application.Features.Bookings;

public sealed record CreateBookingRequest(
    [Range(1, int.MaxValue)] int TicketOfferId,
    [Range(1, 10)] int Quantity,
    [Required, RegularExpression("^[A-Za-z0-9_-]{8,64}$")] string IdempotencyKey);
