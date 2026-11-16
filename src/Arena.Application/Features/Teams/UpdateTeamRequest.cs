using System.ComponentModel.DataAnnotations;

namespace Arena.Application.Features.Teams;

public sealed record UpdateTeamRequest(
    [Required, StringLength(120, MinimumLength = 2)] string Name,
    [Required, StringLength(80)] string City,
    Guid Version);
