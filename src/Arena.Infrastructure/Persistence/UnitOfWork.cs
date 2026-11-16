using System.Data;
using Arena.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Arena.Infrastructure.Persistence;

public sealed class UnitOfWork(ArenaDbContext db) : IUnitOfWork
{
    private readonly Dictionary<Type, object> repositories = [];

    public IRepository<T> Repository<T>() where T : Entity
    {
        if (!repositories.TryGetValue(typeof(T), out var repository))
            repositories[typeof(T)] = repository = new EfRepository<T>(db);
        return (IRepository<T>)repository;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    /// <summary>Runs <paramref name="action"/> in a serializable transaction, or joins the one already open.</summary>
    public async Task<T> SerializableAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is not null) return await action();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var result = await action();
        await transaction.CommitAsync(ct);
        return result;
    }
}
