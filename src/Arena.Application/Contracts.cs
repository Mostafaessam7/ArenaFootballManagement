using System.ComponentModel.DataAnnotations;
using Arena.Domain;

namespace Arena.Application;

public sealed record RegisterRequest([Required, StringLength(120, MinimumLength = 2)] string FullName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 12)] string Password);
public sealed record LoginRequest([Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128)] string Password);
public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, UserDto User);
public sealed record UserDto(int Id, string FullName, string Email, string Role);
public sealed record TeamRequest([Required, StringLength(120, MinimumLength = 2)] string Name,
    [Required, RegularExpression("^[A-Za-z0-9]{2,10}$")] string Code,
    [Required, StringLength(80)] string City);
public sealed record UpdateTeamRequest([Required, StringLength(120, MinimumLength = 2)] string Name,
    [Required, StringLength(80)] string City, Guid Version);
public sealed record TeamDto(int Id, string Name, string Code, string City, bool IsActive, Guid Version);
public sealed record PlayerRequest([Required, StringLength(120, MinimumLength = 2)] string FullName,
    DateOnly BirthDate, [Required, StringLength(80)] string Nationality,
    [Required, RegularExpression("^(Goalkeeper|Defender|Midfielder|Forward)$")] string Position);
public sealed record MembershipRequest([Range(1, int.MaxValue)] int TeamId, DateOnly JoinedOn);
public sealed record TournamentRequest([Required, StringLength(160, MinimumLength = 2)] string Name,
    [Required, StringLength(30)] string Season, DateOnly StartDate, DateOnly EndDate);
public sealed record RegisterTeamRequest([Range(1, int.MaxValue)] int TeamId);
public sealed record RosterRequest([Range(1, int.MaxValue)] int TeamId,
    [Range(1, int.MaxValue)] int PlayerId, [Range(1, 99)] int ShirtNumber);
public sealed record VenueRequest([Required, StringLength(160)] string Name,
    [Required, StringLength(80)] string City, [Required, StringLength(300)] string Address,
    [Range(1, 200000)] int Capacity);
public sealed record MatchRequest([Range(1, int.MaxValue)] int TournamentId,
    [Range(1, int.MaxValue)] int HomeTeamId, [Range(1, int.MaxValue)] int AwayTeamId,
    [Range(1, int.MaxValue)] int VenueId, DateTime StartsAtUtc, DateTime EndsAtUtc,
    [Range(1, 100)] int RoundNumber, [Range(1, 2)] int Leg = 1);
public sealed record CommunityEventRequest([Required, StringLength(180)] string Title,
    [Required, StringLength(2000)] string Description, [Range(1, int.MaxValue)] int VenueId,
    DateTime StartsAtUtc, DateTime EndsAtUtc);
public sealed record ResultRequest([Range(0, 99)] int HomeScore, [Range(0, 99)] int AwayScore,
    [Required, StringLength(500, MinimumLength = 5)] string Reason, Guid Version);
public sealed record PlayerStatRequest([Range(1, int.MaxValue)] int PlayerId,
    [Range(1, int.MaxValue)] int TeamId, [Range(0, 99)] int Goals,
    [Range(0, 99)] int Assists, [Range(0, 2)] int YellowCards,
    [Range(0, 1)] int RedCards, [Range(0, 130)] int MinutesPlayed);
public sealed record TicketOfferRequest([Required, StringLength(80)] string Name,
    [Range(typeof(decimal), "0", "100000")] decimal Price, [Range(1, 200000)] int Capacity,
    DateTime SalesStartUtc, DateTime SalesEndUtc);
public sealed record CreateBookingRequest([Range(1, int.MaxValue)] int TicketOfferId,
    [Range(1, 10)] int Quantity,
    [Required, RegularExpression("^[A-Za-z0-9_-]{8,64}$")] string IdempotencyKey);
public sealed record CheckInRequest([Required, RegularExpression("^[A-Fa-f0-9]{64}$")] string Code);
public sealed record TicketDto(int Id, string Code, DateTime? CheckedInUtc);
public sealed record BookingDto(int Id, int TicketOfferId, int Quantity, decimal UnitPrice,
    decimal TotalAmount, string Currency, string Status, IReadOnlyList<TicketDto> Tickets);
public sealed record EventDto(int Id, string Type, string Title, int VenueId, DateTime StartsAtUtc,
    DateTime EndsAtUtc, string Status, int? TournamentId, int? HomeTeamId, int? AwayTeamId,
    int? HomeScore, int? AwayScore, Guid Version);
public sealed record StandingRow(int TeamId, string TeamName, int Played, int Won, int Drawn,
    int Lost, int GoalsFor, int GoalsAgainst, int GoalDifference, int Points);
public sealed record ScorerRow(int PlayerId, string PlayerName, int Goals, int Assists, int Appearances);
public sealed record PageResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
