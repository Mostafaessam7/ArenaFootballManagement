using System.Linq.Expressions;

namespace Arena.Application.Interfaces;

/// <summary>Generic data access for one entity type. IQueryable never leaves the infrastructure layer.</summary>
public interface IRepository<T> where T : Entity
{
    Task<T?> GetAsync(int id, CancellationToken ct = default);
    Task<T?> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null,
        int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
    void Add(T entity);
    void Remove(T entity);
}
