namespace Arena.Application.Features.Tournaments;

public sealed record ScorerRow(int PlayerId, string PlayerName, int Goals, int Assists, int Appearances);
