using System.Text.Json.Serialization;
using Arena.Api.Documentation;
using Arena.Api.Errors;
using Arena.Api.Setup;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Setup commands: `--init-db [--seed-demo]` and `--export-schema <path>` run once and exit.
var init = args.Contains("--init-db");
var schemaIndex = Array.IndexOf(args, "--export-schema");

builder.Services.AddArenaPersistence(builder.Configuration, builder.Environment);
builder.Services.AddArenaApplication();
builder.Services.AddArenaAuthentication(builder.Configuration, requireSigningKey: !init && schemaIndex < 0);
builder.Services.AddArenaRateLimiting();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers().AddJsonOptions(x => x.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerOpenApiTransformer>());

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
    if (ServiceCollectionExtensions.UsesSqlite(builder.Configuration)) await db.Database.EnsureCreatedAsync();
    else await db.Database.MigrateAsync();
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
