using System.ComponentModel.DataAnnotations;

namespace Arena.Application.Features.Tournaments;

public sealed record RegisterTeamRequest([Range(1, int.MaxValue)] int TeamId);
