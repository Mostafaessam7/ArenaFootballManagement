using System.ComponentModel.DataAnnotations;

namespace Arena.Application.Features.Tickets;

public sealed record TicketOfferRequest(
    [Required, StringLength(80)] string Name,
    [Range(typeof(decimal), "0", "100000")] decimal Price,
    [Range(1, 200000)] int Capacity,
    DateTime SalesStartUtc,
    DateTime SalesEndUtc);
