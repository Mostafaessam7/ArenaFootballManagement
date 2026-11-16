namespace Arena.Domain.Entities;

/// <summary>One period of a player at a team. Only one open period (LeftOn is null) per player.</summary>
public sealed class PlayerMembership : Entity
{
    public int PlayerId { get; set; }
    public int TeamId { get; set; }
    public DateOnly JoinedOn { get; set; }
    public DateOnly? LeftOn { get; set; }
}
