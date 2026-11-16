using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Arena.Api;
using Arena.Application;
using Arena.Domain;
using Arena.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var sqlite = string.Equals(builder.Configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);
if (sqlite && !builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
    throw new InvalidOperationException("SQLite is available only in Development or Testing. Use SQL Server for deployment.");
var connection = builder.Configuration.GetConnectionString("Arena") ?? throw new InvalidOperationException("ConnectionStrings:Arena is required.");
builder.Services.AddDbContext<ArenaDbContext>(o => {
    if (sqlite) o.UseSqlite(connection);
    else o.UseSqlServer(connection);
});
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ICompetitionQueries, CompetitionQueries>();
builder.Services.AddScoped<ArenaService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers().AddJsonOptions(x => x.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerOpenApiTransformer>());
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options => {
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions {
            PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var init = args.Contains("--init-db");
var schemaIndex = Array.IndexOf(args, "--export-schema");
var key = builder.Configuration["Jwt:SigningKey"];
if (!init && schemaIndex < 0 && (string.IsNullOrEmpty(key) || Encoding.UTF8.GetByteCount(key) < 32))
    throw new InvalidOperationException("Set Jwt__SigningKey to a random secret of at least 32 bytes. See scripts/start-demo.ps1.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => {
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key ?? new string('x', 32))),
        NameClaimType = JwtRegisteredClaimNames.Sub, RoleClaimType = "role", ClockSkew = TimeSpan.FromSeconds(30)
    };
    options.Events = new JwtBearerEvents { OnTokenValidated = async context => {
        if (!int.TryParse(context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id))
        { context.Fail("Invalid subject."); return; }
        var user = await context.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>()
            .Repository<UserAccount>().GetAsync(id, context.HttpContext.RequestAborted);
        if (user is null || !user.IsActive || context.Principal?.FindFirstValue("role") != user.Role.ToString())
            context.Fail("Account is inactive or permissions changed.");
    }};
});
var app = builder.Build();
if (schemaIndex >= 0)
{
    if (schemaIndex + 1 >= args.Length) throw new ArgumentException("--export-schema requires an output path.");
    using var scope = app.Services.CreateScope();
    SchemaExporter.Write(scope.ServiceProvider.GetRequiredService<ArenaDbContext>(), args[schemaIndex + 1]);
    return;
}
if (init)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ArenaDbContext>();
    if (sqlite) await db.Database.EnsureCreatedAsync(); else await db.Database.MigrateAsync();
    await Bootstrapper.CreateAdminAsync(scope.ServiceProvider, builder.Configuration);
    if (args.Contains("--seed-demo"))
    {
        if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Demo seeding is Development only.");
        await Bootstrapper.SeedDemoAsync(scope.ServiceProvider);
    }
    Console.WriteLine("Database initialized. Start the API without --init-db.");
    return;
}
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else if (!app.Environment.IsEnvironment("Testing")) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/health/ready", async (ArenaDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "Ready" }) : Results.StatusCode(503));
app.MapControllers();
app.Run();

public partial class Program { }
