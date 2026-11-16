namespace Arena.Domain.Entities;

/// <summary>A player fixed to one team for one tournament, independent of later transfers.</summary>
public sealed class TournamentRoster : Entity
{
    public int TournamentId { get; set; }
    public int TeamId { get; set; }
    public int PlayerId { get; set; }
    public int ShirtNumber { get; set; }
}
