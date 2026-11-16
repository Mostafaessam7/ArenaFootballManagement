using System.ComponentModel.DataAnnotations;

namespace Arena.Application.Features.Players;

public sealed record MembershipRequest([Range(1, int.MaxValue)] int TeamId, DateOnly JoinedOn);
