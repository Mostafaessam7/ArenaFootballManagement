namespace Arena.Application.Features.Tournaments;

public sealed record StandingRow(int TeamId, string TeamName, int Played, int Won, int Drawn,
    int Lost, int GoalsFor, int GoalsAgainst, int GoalDifference, int Points);
