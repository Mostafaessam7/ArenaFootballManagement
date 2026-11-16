using System.ComponentModel.DataAnnotations;

namespace Arena.Application.Features.Tournaments;

public sealed record TournamentRequest(
    [Required, StringLength(160, MinimumLength = 2)] string Name,
    [Required, StringLength(30)] string Season,
    DateOnly StartDate,
    DateOnly EndDate);
