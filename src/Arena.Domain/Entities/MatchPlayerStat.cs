namespace Arena.Domain.Entities;

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
