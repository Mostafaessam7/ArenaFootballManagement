namespace Arena.Domain.Entities;

/// <summary>A team's registration in a tournament; matches reference this, not Teams directly.</summary>
public sealed class TournamentTeam : Entity
{
    public int TournamentId { get; set; }
    public int TeamId { get; set; }
}
