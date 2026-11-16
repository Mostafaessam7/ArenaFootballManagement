using System.ComponentModel.DataAnnotations;
using System.Data;
using Arena.Application;
using Arena.Domain;
using Arena.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Arena.Api;

public static class Bootstrapper
{
    public static async Task CreateAdminAsync(IServiceProvider services, IConfiguration config)
    {
        var db = services.GetRequiredService<ArenaDbContext>();
        var email = config["Bootstrap:AdminEmail"];
        var password = config["Bootstrap:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        { Console.WriteLine("No bootstrap credentials supplied; no administrator was created."); return; }
        if (!new EmailAddressAttribute().IsValid(email) || password.Length is < 12 or > 128)
            throw new InvalidOperationException("Supply a valid Bootstrap__AdminEmail and a 12..128 character Bootstrap__AdminPassword.");
        if (await db.Users.AnyAsync(x => x.Role == UserRole.Admin))
        { Console.WriteLine("An administrator already exists; bootstrap did not change any credentials."); return; }
        var user = new UserAccount { FullName = "Arena Administrator", Email = email.Trim(),
            NormalizedEmail = email.Trim().ToUpperInvariant(), Role = UserRole.Admin };
        user.PasswordHash = services.GetRequiredService<IPasswordHasher<UserAccount>>().HashPassword(user, password);
        db.Users.Add(user); await db.SaveChangesAsync();
        Console.WriteLine("Administrator created from the supplied environment configuration.");
    }
    public static async Task SeedDemoAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ArenaDbContext>();
        if (await db.Teams.AnyAsync()) { Console.WriteLine("Demo seed skipped because teams already exist."); return; }
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var admin = await db.Users.FirstOrDefaultAsync(x => x.Role == UserRole.Admin) ??
            throw new InvalidOperationException("Create an admin before seeding demo data.");
        var service = services.GetRequiredService<ArenaService>();
        var ct = CancellationToken.None;
        var now = DateTime.UtcNow;
        var a = await service.CreateTeamAsync(new("Cairo Falcons", "FAL", "Cairo"), ct);
        var b = await service.CreateTeamAsync(new("Alexandria Waves", "WAV", "Alexandria"), ct);
        var c = await service.CreateTeamAsync(new("Giza Stars", "STR", "Giza"), ct);
        var tournament = await service.CreateTournamentAsync(new("Arena Demo League", "2026-2027",
            DateOnly.FromDateTime(now.AddDays(-30)), DateOnly.FromDateTime(now.AddDays(60))), admin.Id, ct);
        foreach (var team in new[] { a, b, c }) await service.RegisterTeamAsync(tournament.Id, new(team.Id), ct);
        var player = await service.CreatePlayerAsync(new("Omar Demo", new DateOnly(2002, 4, 12), "Egypt", "Forward"), ct);
        await service.TransferAsync(player.Id, new(a.Id, DateOnly.FromDateTime(now.AddDays(-30))), ct);
        await service.AddRosterAsync(tournament.Id, new(a.Id, player.Id, 9), ct);
        var venue = await service.CreateVenueAsync(new("Arena Training Stadium", "Cairo", "Demo sports complex", 2000), ct);
        var played = await service.CreateMatchAsync(new(tournament.Id, a.Id, b.Id, venue.Id, now.AddDays(-2), now.AddDays(-2).AddHours(2), 1), admin.Id, ct);
        var future = await service.CreateMatchAsync(new(tournament.Id, b.Id, c.Id, venue.Id, now.AddDays(7), now.AddDays(7).AddHours(2), 2), admin.Id, ct);
        await service.StartTournamentAsync(tournament.Id, tournament.Version, ct);
        await service.ApproveResultAsync(played.Id, new(2, 1, "Initial demonstration result", played.Version), admin.Id, ct);
        await service.SetPlayerStatAsync(played.Id, new(player.Id, a.Id, 2, 0, 0, 0, 90), ct);
        await service.CreateOfferAsync(future.Id, new("General admission", 150m, 1000, now.AddHours(-1), now.AddDays(7).AddHours(-1)), ct);
        await service.CreateCommunityAsync(new("Football fan day", "A community football event with training demonstrations.",
            venue.Id, now.AddDays(10), now.AddDays(10).AddHours(3)), admin.Id, ct);
        await transaction.CommitAsync();
        Console.WriteLine("Demo league, teams, player, result, future fixture and ticket offer created.");
    }
}
