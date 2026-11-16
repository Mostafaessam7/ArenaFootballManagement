using System.ComponentModel.DataAnnotations;

namespace Arena.Application.Features.Events;

public sealed record MatchRequest(
    [Range(1, int.MaxValue)] int TournamentId,
    [Range(1, int.MaxValue)] int HomeTeamId,
    [Range(1, int.MaxValue)] int AwayTeamId,
    [Range(1, int.MaxValue)] int VenueId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    [Range(1, 100)] int RoundNumber,
    [Range(1, 2)] int Leg = 1);
