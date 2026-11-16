namespace Arena.Application.Features.Auth;

public sealed record UserDto(int Id, string FullName, string Email, string Role);
