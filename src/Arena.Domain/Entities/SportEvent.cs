namespace Arena.Domain.Entities;

/// <summary>
/// EER specialization, total and disjoint: every event is exactly a <see cref="MatchEvent"/>
/// or a <see cref="CommunityEvent"/>. Mapped as one Events table with an EventType discriminator.
/// </summary>
public abstract class SportEvent : Entity
{
    public string Title { get; set; } = "";
    public int VenueId { get; set; }
    public int OrganizerId { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Scheduled;
}
