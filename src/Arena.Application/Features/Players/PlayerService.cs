namespace Arena.Application.Features.Players;

/// <summary>FR05 and FR06: players and their membership history (transfers between teams).</summary>
public sealed class PlayerService(IUnitOfWork unitOfWork, TimeProvider clock) : ServiceBase(unitOfWork, clock)
{
    public Task<PageResult<Player>> ListAsync(int page, int pageSize, CancellationToken ct) =>
        PageAsync<Player>(page, pageSize, null, ct);

    public Task<Player> GetAsync(int id, CancellationToken ct) => GetRequiredAsync<Player>(id, ct);

    public Task<PageResult<PlayerMembership>> MembershipsAsync(int playerId, int page, int pageSize, CancellationToken ct) =>
        PageAsync<PlayerMembership>(page, pageSize, x => x.PlayerId == playerId, ct);

    public async Task<Player> CreateAsync(PlayerRequest request, CancellationToken ct)
    {
        Guard.Against(request.BirthDate < new DateOnly(1900, 1, 1) || request.BirthDate >= DateOnly.FromDateTime(Now),
            "Birth date must be a past date on or after 1900-01-01.");
        var player = new Player
        {
            FullName = request.FullName.Trim(), BirthDate = request.BirthDate,
            Nationality = request.Nationality.Trim(), Position = request.Position
        };
        Repo<Player>().Add(player);
        await Uow.SaveChangesAsync(ct);
        return player;
    }

    /// <summary>Closes the open membership on the transfer date and opens a new one, in one transaction.</summary>
    public Task<PlayerMembership> TransferAsync(int playerId, MembershipRequest request, CancellationToken ct) =>
        Uow.SerializableAsync(async () =>
        {
            var player = await GetRequiredAsync<Player>(playerId, ct);
            var team = await GetRequiredAsync<Team>(request.TeamId, ct);
            Guard.Against(!team.IsActive, "Team is archived.");
            Guard.Against(request.JoinedOn <= player.BirthDate || request.JoinedOn > DateOnly.FromDateTime(Now),
                "Membership date must follow the birth date and cannot be in the future.");
            var active = await Repo<PlayerMembership>().FindAsync(x => x.PlayerId == playerId && x.LeftOn == null, ct);
            Guard.Against(await Repo<PlayerMembership>().AnyAsync(x => x.PlayerId == playerId &&
                x.LeftOn != null && x.LeftOn > request.JoinedOn, ct), "Membership overlaps a historical membership.");
            if (active is not null)
            {
                Guard.Against(active.TeamId == request.TeamId || request.JoinedOn <= active.JoinedOn,
                    "Transfer must change teams and occur after the current membership started.");
                active.LeftOn = request.JoinedOn;
                await Uow.SaveChangesAsync(ct);
            }
            var membership = new PlayerMembership { PlayerId = playerId, TeamId = request.TeamId, JoinedOn = request.JoinedOn };
            Repo<PlayerMembership>().Add(membership);
            await Uow.SaveChangesAsync(ct);
            return membership;
        }, ct);
}
