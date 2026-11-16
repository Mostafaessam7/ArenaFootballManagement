using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Arena.Infrastructure.Persistence;

/// <summary>Used by `dotnet ef` to build the context; migrations are always generated for SQL Server.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<ArenaDbContext>
{
    public ArenaDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Arena") ??
            "Server=.;Database=ArenaSports;Trusted_Connection=True;TrustServerCertificate=True";
        return new ArenaDbContext(new DbContextOptionsBuilder<ArenaDbContext>().UseSqlServer(connection).Options);
    }
}
