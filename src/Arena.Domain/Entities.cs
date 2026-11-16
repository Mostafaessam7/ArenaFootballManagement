namespace Arena.Domain;

public abstract class Entity
{
    public int Id { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
}

public sealed class UserAccount : Entity
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Spectator;
    public bool IsActive { get; set; } = true;
}

public sealed class Team : Entity
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string City { get; set; } = "";
    public int? ManagerId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Player : Entity
{
    public string FullName { get; set; } = "";
    public DateOnly BirthDate { get; set; }
    public string Nationality { get; set; } = "";
    public string Position { get; set; } = "";
}

public sealed class PlayerMembership : Entity
{
    public int PlayerId { get; set; }
    public int TeamId { get; set; }
    public DateOnly JoinedOn { get; set; }
    public DateOnly? LeftOn { get; set; }
}

public sealed class Tournament : Entity
{
    public string Name { get; set; } = "";
    public string Season { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int OrganizerId { get; set; }
    public TournamentStatus Status { get; set; } = TournamentStatus.RegistrationOpen;
}

public sealed class TournamentTeam : Entity
{
    public int TournamentId { get; set; }
    public int TeamId { get; set; }
}

public sealed class TournamentRoster : Entity
{
    public int TournamentId { get; set; }
    public int TeamId { get; set; }
    public int PlayerId { get; set; }
    public int ShirtNumber { get; set; }
}

public sealed class Venue : Entity
{
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    public string Address { get; set; } = "";
    public int Capacity { get; set; }
}

// EER specialization: total and disjoint. Mapped as one table with a discriminator.
public abstract class SportEvent : Entity
{
    public string Title { get; set; } = "";
    public int VenueId { get; set; }
    public int OrganizerId { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Scheduled;
}

public sealed class MatchEvent : SportEvent
{
    public int TournamentId { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public int RoundNumber { get; set; }
    public int Leg { get; set; } = 1;
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
}

public sealed class CommunityEvent : SportEvent
{
    public string Description { get; set; } = "";
}

public sealed class MatchPlayerStat : Entity
{
    public int MatchId { get; set; }
    public int TournamentId { get; set; }
    public int TeamId { get; set; }
    public int PlayerId { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public int MinutesPlayed { get; set; }
}

public sealed class TicketOffer : Entity
{
    public int EventId { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Capacity { get; set; }
    public int SoldQuantity { get; set; }
    public DateTime SalesStartUtc { get; set; }
    public DateTime SalesEndUtc { get; set; }
}

public sealed class Booking : Entity
{
    public int UserId { get; set; }
    public int TicketOfferId { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public DateTime? CancelledUtc { get; set; }
}

public sealed class Ticket : Entity
{
    public int BookingId { get; set; }
    public string Code { get; set; } = "";
    public DateTime? CheckedInUtc { get; set; }
    public int? CheckedInById { get; set; }
}

public sealed class ResultAudit : Entity
{
    public int MatchId { get; set; }
    public int ActorId { get; set; }
    public int? PreviousHomeScore { get; set; }
    public int? PreviousAwayScore { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public string Reason { get; set; } = "";
}
