namespace Arena.Application.Interfaces;

/// <summary>One DbContext per request: hands out repositories, saves, and runs transactions.</summary>
public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : Entity;
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Read-committed transaction. Use it when correctness comes from row locks taken by updates,
    /// concurrency tokens and unique indexes, so concurrent requests wait briefly instead of deadlocking.
    /// </summary>
    Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct = default);

    /// <summary>Serializable transaction for rules that check rows that do not exist yet (overlaps, totals).</summary>
    Task<T> SerializableAsync<T>(Func<Task<T>> action, CancellationToken ct = default);
}
