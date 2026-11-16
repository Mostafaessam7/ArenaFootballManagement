namespace Arena.Application.Interfaces;

/// <summary>One DbContext per request: hands out repositories, saves, and runs serializable transactions.</summary>
public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : Entity;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<T> SerializableAsync<T>(Func<Task<T>> action, CancellationToken ct = default);
}
