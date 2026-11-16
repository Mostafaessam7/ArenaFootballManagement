using System.Linq.Expressions;
using Arena.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Arena.Infrastructure.Repositories;

public sealed class EfRepository<T>(ArenaDbContext db) : IRepository<T> where T : Entity
{
    public Task<T?> GetAsync(int id, CancellationToken ct = default) =>
        db.Set<T>().SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<T?> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        db.Set<T>().SingleOrDefaultAsync(predicate, ct);

    /// <summary>Read-only page ordered by Id so paging is stable.</summary>
    public async Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null,
        int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        if (page < 1 || page > 1000000 || pageSize is < 1 or > 100)
            throw new BusinessException(400, "invalid_pagination", "Page must be 1..1000000 and pageSize 1..100.");
        IQueryable<T> query = db.Set<T>().AsNoTracking();
        if (predicate is not null) query = query.Where(predicate);
        return await query.OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    }

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        db.Set<T>().AnyAsync(predicate, ct);

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default) =>
        predicate is null ? db.Set<T>().CountAsync(ct) : db.Set<T>().CountAsync(predicate, ct);

    public void Add(T entity) => db.Set<T>().Add(entity);

    public void Remove(T entity) => db.Set<T>().Remove(entity);
}
