using Arena.Api.Auth;
using Arena.Application.Features.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Arena.Api.Controllers;

[ApiController, Route("api/v1/auth"), EnableRateLimiting("auth")]
public sealed class AuthController(AuthService auth) : ArenaController
{
    [HttpPost("register")]
    public Task<AuthResponse> Register(RegisterRequest request, CancellationToken ct) => auth.RegisterAsync(request, ct);

    [HttpPost("login")]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);
}
