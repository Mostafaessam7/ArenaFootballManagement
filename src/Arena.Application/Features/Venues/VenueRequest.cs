using System.ComponentModel.DataAnnotations;

namespace Arena.Application.Features.Venues;

public sealed record VenueRequest(
    [Required, StringLength(160)] string Name,
    [Required, StringLength(80)] string City,
    [Required, StringLength(300)] string Address,
    [Range(1, 200000)] int Capacity);
