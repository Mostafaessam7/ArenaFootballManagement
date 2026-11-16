using System.ComponentModel.DataAnnotations;

namespace Arena.Application.Features.Auth;

public sealed record RegisterRequest(
    [Required, StringLength(120, MinimumLength = 2)] string FullName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 12)] string Password);
