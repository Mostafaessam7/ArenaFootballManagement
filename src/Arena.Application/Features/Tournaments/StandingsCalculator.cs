namespace Arena.Application.Features.Tournaments;

/// <summary>
/// FR14: derives the league table from finished matches only (3/1/0 points).
/// Order: points, goal difference, goals scored, then TeamId for a stable display.
/// </summary>
public static class StandingsCalculator
{
    public static IReadOnlyList<StandingRow> Calculate(IReadOnlyDictionary<int, string> teams,
        IEnumerable<MatchEvent> matches)
    {
        // Per team: [0] played, [1] won, [2] drawn, [3] lost, [4] goals for, [5] goals against, [6] points.
        var totals = teams.ToDictionary(x => x.Key, _ => new int[7]);
        foreach (var match in matches.Where(x => x.Status == EventStatus.Finished &&
            x.HomeScore.HasValue && x.AwayScore.HasValue))
        {
            if (!totals.ContainsKey(match.HomeTeamId) || !totals.ContainsKey(match.AwayTeamId))
                throw new InvalidOperationException("A result references a team outside the tournament.");
            Add(totals[match.HomeTeamId], match.HomeScore!.Value, match.AwayScore!.Value);
            Add(totals[match.AwayTeamId], match.AwayScore!.Value, match.HomeScore!.Value);
        }
        return totals.Select(x => new StandingRow(x.Key, teams[x.Key], x.Value[0], x.Value[1],
            x.Value[2], x.Value[3], x.Value[4], x.Value[5], x.Value[4] - x.Value[5], x.Value[6]))
            .OrderByDescending(x => x.Points).ThenByDescending(x => x.GoalDifference)
            .ThenByDescending(x => x.GoalsFor).ThenBy(x => x.TeamId).ToArray();
    }

    private static void Add(int[] row, int scored, int conceded)
    {
        row[0]++; row[4] += scored; row[5] += conceded;
        if (scored > conceded) { row[1]++; row[6] += 3; }
        else if (scored == conceded) { row[2]++; row[6]++; }
        else row[3]++;
    }
}
