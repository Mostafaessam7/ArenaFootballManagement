namespace Arena.Application.Features.Teams;

/// <summary>FR04: create, read, update and archive teams. The team code is unique and never changes.</summary>
public sealed class TeamService(IUnitOfWork unitOfWork, TimeProvider clock) : ServiceBase(unitOfWork, clock)
{
    public static TeamDto ToDto(Team x) => new(x.Id, x.Name, x.Code, x.City, x.IsActive, x.Version);

    public async Task<PageResult<TeamDto>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        var result = await PageAsync<Team>(page, pageSize, null, ct);
        return new(result.Items.Select(ToDto).ToArray(), page, pageSize, result.Total);
    }

    public async Task<TeamDto> GetAsync(int id, CancellationToken ct) => ToDto(await GetRequiredAsync<Team>(id, ct));

    public async Task<TeamDto> CreateAsync(TeamRequest request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        Guard.Against(await Repo<Team>().AnyAsync(x => x.Code == code, ct), "Team code is already in use.");
        var team = new Team { Name = request.Name.Trim(), Code = code, City = request.City.Trim() };
        Repo<Team>().Add(team);
        await Uow.SaveChangesAsync(ct);
        return ToDto(team);
    }

    public async Task<TeamDto> UpdateAsync(int id, UpdateTeamRequest request, CancellationToken ct)
    {
        var team = await GetRequiredAsync<Team>(id, ct);
        Guard.Version(team, request.Version);
        team.Name = request.Name.Trim();
        team.City = request.City.Trim();
        await Uow.SaveChangesAsync(ct);
        return ToDto(team);
    }

    /// <summary>Delete is an archive: history (results, rosters, bookings) stays intact.</summary>
    public async Task ArchiveAsync(int id, Guid version, CancellationToken ct)
    {
        var team = await GetRequiredAsync<Team>(id, ct);
        Guard.Version(team, version);
        team.IsActive = false;
        await Uow.SaveChangesAsync(ct);
    }
}
