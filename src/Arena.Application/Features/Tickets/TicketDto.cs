namespace Arena.Application.Features.Tickets;

public sealed record TicketDto(int Id, string Code, DateTime? CheckedInUtc);
