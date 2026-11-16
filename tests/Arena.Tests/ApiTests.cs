using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Arena.Application;
using Arena.Domain;
using Arena.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Arena.Tests;

public sealed class ArenaFactory : WebApplicationFactory<Program>
{
    private readonly string dbName = "arena-api-" + Guid.NewGuid().ToString("N");
    private SqliteConnection? keeper;
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connection = $"Data Source={dbName};Mode=Memory;Cache=Shared";
        keeper = new SqliteConnection(connection); keeper.Open();
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider","Sqlite");
        builder.UseSetting("ConnectionStrings:Arena",connection);
        builder.UseSetting("Jwt:SigningKey","Test-only-key-never-use-in-deployment-123456789");
        builder.UseSetting("Jwt:Issuer","Arena.Api");builder.UseSetting("Jwt:Audience","Arena.Client");
    }
    public async Task InitializeAsync()
    {
        using var scope=Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ArenaDbContext>();
        await db.Database.EnsureCreatedAsync();
        var admin=new UserAccount {FullName="Test Admin",Email="admin@example.test",NormalizedEmail="ADMIN@EXAMPLE.TEST",Role=UserRole.Admin};
        admin.PasswordHash=new PasswordHasher<UserAccount>().HashPassword(admin,"Test-password-12345");
        db.Users.Add(admin);await db.SaveChangesAsync();
    }
    protected override void Dispose(bool disposing) {base.Dispose(disposing);if(disposing)keeper?.Dispose();}
}

public sealed class ApiTests
{
    [Fact]
    public async Task Authentication_Validation_Roles_AndPublicRead_WorkThroughHttp()
    {
        await using var factory=new ArenaFactory();await factory.InitializeAsync();using var client=factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/v1/teams",new TeamRequest("Test Team","TST","Cairo"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/v1/auth/register",new RegisterRequest("Test Fan","fan@example.test","short"))).StatusCode);
        var fanResponse=await client.PostAsJsonAsync("/api/v1/auth/register",new RegisterRequest("Test Fan","fan@example.test","Long-test-password-1"));
        fanResponse.EnsureSuccessStatusCode();var fan=(await fanResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.Equal("Spectator",fan.User.Role);
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",fan.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/v1/teams",new TeamRequest("Test Team","TST","Cairo"))).StatusCode);
        var login=await client.PostAsJsonAsync("/api/v1/auth/login",new LoginRequest("admin@example.test","Test-password-12345"));
        login.EnsureSuccessStatusCode();var admin=(await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",admin.AccessToken);
        var created=await client.PostAsJsonAsync("/api/v1/teams",new TeamRequest("Test Team","TST","Cairo"));
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        var team=(await created.Content.ReadFromJsonAsync<TeamDto>())!;
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/v1/teams",new TeamRequest("Duplicate","tst","Cairo"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync($"/api/v1/teams/{team.Id}",new UpdateTeamRequest("Updated Team","Cairo",Guid.NewGuid()))).StatusCode);
        var updated=await client.PutAsJsonAsync($"/api/v1/teams/{team.Id}",new UpdateTeamRequest("Updated Team","Cairo",team.Version));
        updated.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization=null;
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/v1/teams?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/v1/teams/999999")).StatusCode);
    }
    [Fact]
    public async Task Registration_NormalizesEmail_AndCannotEscalateRole()
    {
        await using var factory=new ArenaFactory();await factory.InitializeAsync();using var client=factory.CreateClient();
        var response=await client.PostAsJsonAsync("/api/v1/auth/register",new {fullName="New Fan",email="User@example.test",password="Test-password-123456",role="Admin"});
        response.EnsureSuccessStatusCode();Assert.Equal("Spectator",(await response.Content.ReadFromJsonAsync<AuthResponse>())!.User.Role);
        var duplicate=await client.PostAsJsonAsync("/api/v1/auth/register",new RegisterRequest("Another Fan","user@EXAMPLE.test","Test-password-987654"));
        Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);
    }
}
