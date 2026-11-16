using System.ComponentModel.DataAnnotations;

namespace Arena.Application.Features.Players;

public sealed record PlayerRequest(
    [Required, StringLength(120, MinimumLength = 2)] string FullName,
    DateOnly BirthDate,
    [Required, StringLength(80)] string Nationality,
    [Required, RegularExpression("^(Goalkeeper|Defender|Midfielder|Forward)$")] string Position);
