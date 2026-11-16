using System.Linq.Expressions;

namespace Arena.Application.Common;

/// <summary>
/// Shared plumbing for the feature services: repository access through the unit of work,
/// the current UTC time, "get or 404" and paged listing.
/// </summary>
public abstract class ServiceBase(IUnitOfWork unitOfWork, TimeProvider clock)
{
    protected IUnitOfWork Uow { get; } = unitOfWork;

    protected DateTime Now => clock.GetUtcNow().UtcDateTime;

    protected IRepository<T> Repo<T>() where T : Entity => Uow.Repository<T>();

    protected async Task<T> GetRequiredAsync<T>(int id, CancellationToken ct) where T : Entity =>
        Guard.Found(await Repo<T>().GetAsync(id, ct));

    protected async Task<PageResult<T>> PageAsync<T>(int page, int pageSize,
        Expression<Func<T, bool>>? filter, CancellationToken ct) where T : Entity =>
        new(await Repo<T>().ListAsync(filter, page, pageSize, ct), page, pageSize,
            await Repo<T>().CountAsync(filter, ct));
}
