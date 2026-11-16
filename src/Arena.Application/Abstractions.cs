using System.Linq.Expressions;
using Arena.Domain;

namespace Arena.Application;

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

public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : Entity;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<T> SerializableAsync<T>(Func<Task<T>> action, CancellationToken ct = default);
}

public interface ICompetitionQueries
{
    Task<IReadOnlyList<StandingRow>> StandingsAsync(int tournamentId, CancellationToken ct);
    Task<IReadOnlyList<ScorerRow>> TopScorersAsync(int tournamentId, CancellationToken ct);
    Task<bool> HasScheduleConflictAsync(int venueId, DateTime start, DateTime end,
        int? homeTeamId, int? awayTeamId, CancellationToken ct);
    Task<int> RecordedGoalsAsync(int matchId, int teamId, int? excludePlayerId, CancellationToken ct);
    Task<int> AllocatedCapacityAsync(int eventId, CancellationToken ct);
    Task<IReadOnlyList<TicketDto>> TicketsAsync(int bookingId, CancellationToken ct);
}

public sealed class BusinessException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

public static class Guard
{
    public static void Against(bool condition, string message, string code = "business_rule")
    { if (condition) throw new BusinessException(409, code, message); }
    public static T Found<T>(T? value) where T : class => value ??
        throw new BusinessException(404, "not_found", "The requested resource was not found.");
    public static void Version(Entity entity, Guid expected) => Against(entity.Version != expected,
        "This resource changed. Reload it and retry with its current version.", "stale_version");
}
