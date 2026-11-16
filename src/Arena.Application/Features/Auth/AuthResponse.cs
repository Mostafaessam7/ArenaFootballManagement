namespace Arena.Application.Features.Auth;

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, UserDto User);
