using System.ComponentModel.DataAnnotations;

namespace Arena.Application.Features.Results;

public sealed record ResultRequest(
    [Range(0, 99)] int HomeScore,
    [Range(0, 99)] int AwayScore,
    [Required, StringLength(500, MinimumLength = 5)] string Reason,
    Guid Version);
