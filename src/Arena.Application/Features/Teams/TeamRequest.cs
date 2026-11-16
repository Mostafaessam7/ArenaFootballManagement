using System.ComponentModel.DataAnnotations;

namespace Arena.Application.Features.Teams;

public sealed record TeamRequest(
    [Required, StringLength(120, MinimumLength = 2)] string Name,
    [Required, RegularExpression("^[A-Za-z0-9]{2,10}$")] string Code,
    [Required, StringLength(80)] string City);
