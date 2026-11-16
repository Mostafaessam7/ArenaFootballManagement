using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Arena.Api.Auth;
using Arena.Application.Features.Bookings;
using Arena.Application.Features.Events;
using Arena.Application.Features.Players;
using Arena.Application.Features.Results;
using Arena.Application.Features.Tickets;
using Arena.Application.Features.Tournaments;
using Arena.Application.Features.Teams;
using Arena.Application.Features.Venues;
using Arena.Infrastructure.Queries;
using Arena.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Arena.Api.Setup;

/// <summary>Dependency injection for each layer, so Program.cs only shows the order of the pipeline.</summary>
public static class ServiceCollectionExtensions
{
    public static bool UsesSqlite(IConfiguration config) =>
        string.Equals(config["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);

    /// <summary>DbContext (SQL Server, or SQLite in Development/Testing only), Unit of Work and queries.</summary>
    public static IServiceCollection AddArenaPersistence(this IServiceCollection services,
        IConfiguration config, IHostEnvironment environment)
    {
        var sqlite = UsesSqlite(config);
        if (sqlite && !environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
            throw new InvalidOperationException("SQLite is available only in Development or Testing. Use SQL Server for deployment.");
        var connection = config.GetConnectionString("Arena") ?? throw new InvalidOperationException("ConnectionStrings:Arena is required.");
        services.AddDbContext<ArenaDbContext>(o =>
        {
            if (sqlite) o.UseSqlite(connection);
            else o.UseSqlServer(connection);
        });
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICompetitionQueries, CompetitionQueries>();
        services.AddScoped<ITicketInventory, TicketInventory>();
        return services;
    }

    /// <summary>One service per feature; each is scoped so it shares the request's Unit of Work.</summary>
    public static IServiceCollection AddArenaApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<TeamService>();
        services.AddScoped<PlayerService>();
        services.AddScoped<TournamentService>();
        services.AddScoped<VenueService>();
        services.AddScoped<EventService>();
        services.AddScoped<ResultService>();
        services.AddScoped<TicketService>();
        services.AddScoped<BookingService>();
        return services;
    }

    /// <summary>
    /// JWT bearer authentication. Every request re-checks that the account is still active and has
    /// the same role as in the token. <paramref name="requireSigningKey"/> is false only for setup commands.
    /// </summary>
    public static IServiceCollection AddArenaAuthentication(this IServiceCollection services,
        IConfiguration config, bool requireSigningKey)
    {
        var key = config["Jwt:SigningKey"];
        if (requireSigningKey && (string.IsNullOrEmpty(key) || Encoding.UTF8.GetByteCount(key) < 32))
            throw new InvalidOperationException("Set Jwt__SigningKey to a random secret of at least 32 bytes. See scripts/start-demo.ps1.");
        services.AddScoped<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
        services.AddScoped<AuthService>();
        services.AddAuthorization();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                ValidIssuer = config["Jwt:Issuer"], ValidAudience = config["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key ?? new string('x', 32))),
                NameClaimType = JwtRegisteredClaimNames.Sub, RoleClaimType = "role", ClockSkew = TimeSpan.FromSeconds(30)
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    if (!int.TryParse(context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id))
                    { context.Fail("Invalid subject."); return; }
                    var user = await context.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>()
                        .Repository<UserAccount>().GetAsync(id, context.HttpContext.RequestAborted);
                    if (user is null || !user.IsActive || context.Principal?.FindFirstValue("role") != user.Role.ToString())
                        context.Fail("Account is inactive or permissions changed.");
                }
            };
        });
        return services;
    }

    /// <summary>"auth" policy: 10 requests per minute per client address on register and login.</summary>
    public static IServiceCollection AddArenaRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
                }));
        });
}
