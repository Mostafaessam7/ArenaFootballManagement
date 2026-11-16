using System.ComponentModel.DataAnnotations;

namespace Arena.Application.Features.Events;

public sealed record CommunityEventRequest(
    [Required, StringLength(180)] string Title,
    [Required, StringLength(2000)] string Description,
    [Range(1, int.MaxValue)] int VenueId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc);
