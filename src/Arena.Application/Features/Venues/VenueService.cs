namespace Arena.Application.Features.Venues;

/// <summary>FR17: venues and their admission capacity.</summary>
public sealed class VenueService(IUnitOfWork unitOfWork, TimeProvider clock) : ServiceBase(unitOfWork, clock)
{
    public Task<PageResult<Venue>> ListAsync(int page, int pageSize, CancellationToken ct) =>
        PageAsync<Venue>(page, pageSize, null, ct);

    public Task<Venue> GetAsync(int id, CancellationToken ct) => GetRequiredAsync<Venue>(id, ct);

    public async Task<Venue> CreateAsync(VenueRequest request, CancellationToken ct)
    {
        var venue = new Venue
        {
            Name = request.Name.Trim(), City = request.City.Trim(),
            Address = request.Address.Trim(), Capacity = request.Capacity
        };
        Repo<Venue>().Add(venue);
        await Uow.SaveChangesAsync(ct);
        return venue;
    }
}
