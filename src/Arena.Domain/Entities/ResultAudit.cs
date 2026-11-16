namespace Arena.Domain.Entities;

/// <summary>One approval or correction of a match result, with the previous score and the actor.</summary>
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
