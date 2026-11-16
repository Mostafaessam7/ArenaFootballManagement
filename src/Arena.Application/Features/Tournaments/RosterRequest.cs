using System.ComponentModel.DataAnnotations;

namespace Arena.Application.Features.Tournaments;

public sealed record RosterRequest(
    [Range(1, int.MaxValue)] int TeamId,
    [Range(1, int.MaxValue)] int PlayerId,
    [Range(1, 99)] int ShirtNumber);
