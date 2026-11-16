namespace Arena.Domain.Entities;

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
