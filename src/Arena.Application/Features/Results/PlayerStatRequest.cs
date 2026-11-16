using System.ComponentModel.DataAnnotations;

namespace Arena.Application.Features.Results;

public sealed record PlayerStatRequest(
    [Range(1, int.MaxValue)] int PlayerId,
    [Range(1, int.MaxValue)] int TeamId,
    [Range(0, 99)] int Goals,
    [Range(0, 99)] int Assists,
    [Range(0, 2)] int YellowCards,
    [Range(0, 1)] int RedCards,
    [Range(0, 130)] int MinutesPlayed);
