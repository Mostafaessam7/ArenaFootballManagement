namespace Arena.Application.Features.Events;

public sealed record EventDto(int Id, string Type, string Title, int VenueId, DateTime StartsAtUtc,
    DateTime EndsAtUtc, string Status, int? TournamentId, int? HomeTeamId, int? AwayTeamId,
    int? HomeScore, int? AwayScore, Guid Version);
