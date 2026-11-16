using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Arena.Application;
using Arena.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Arena.Api;

public sealed class AuthService(IUnitOfWork uow, IPasswordHasher<UserAccount> hasher,
    IConfiguration config, TimeProvider clock)
{
    private static readonly UserAccount Dummy = new();
    private static readonly string DummyHash = new PasswordHasher<UserAccount>().HashPassword(Dummy, Guid.NewGuid().ToString());
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var normalized = request.Email.Trim().ToUpperInvariant();
        Guard.Against(await uow.Repository<UserAccount>().AnyAsync(x => x.NormalizedEmail == normalized, ct),
            "Email is already registered.", "email_in_use");
        var user = new UserAccount { FullName = request.FullName.Trim(), Email = request.Email.Trim(), NormalizedEmail = normalized };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        uow.Repository<UserAccount>().Add(user); await uow.SaveChangesAsync(ct);
        return Issue(user);
    }
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var normalized = request.Email.Trim().ToUpperInvariant();
        var user = await uow.Repository<UserAccount>().FindAsync(x => x.NormalizedEmail == normalized, ct);
        var verification = hasher.VerifyHashedPassword(user ?? Dummy, user?.PasswordHash ?? DummyHash, request.Password);
        if (user is null || !user.IsActive || verification == PasswordVerificationResult.Failed)
            throw new BusinessException(401, "invalid_credentials", "Invalid email or password.");
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        { user.PasswordHash = hasher.HashPassword(user, request.Password); await uow.SaveChangesAsync(ct); }
        return Issue(user);
    }
    private AuthResponse Issue(UserAccount user)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expiry = now.AddMinutes(config.GetValue("Jwt:LifetimeMinutes", 60));
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("role", user.Role.ToString()), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) };
        var jwt = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], claims, now, expiry,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(jwt), expiry,
            new UserDto(user.Id, user.FullName, user.Email, user.Role.ToString()));
    }
}
