using System.ComponentModel.DataAnnotations;

namespace Arena.Application.Features.Tickets;

public sealed record CheckInRequest([Required, RegularExpression("^[A-Fa-f0-9]{64}$")] string Code);
