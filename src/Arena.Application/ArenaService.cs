using System.Linq.Expressions;
using System.Security.Cryptography;
using Arena.Domain;

namespace Arena.Application;

public sealed class ArenaService(IUnitOfWork uow, ICompetitionQueries queries, TimeProvider clock)
{
    private IRepository<T> Repo<T>() where T : Entity => uow.Repository<T>();
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    public async Task<PageResult<T>> ListAsync<T>(int page, int pageSize,
        Expression<Func<T, bool>>? filter = null, CancellationToken ct = default) where T : Entity =>
        new(await Repo<T>().ListAsync(filter, page, pageSize, ct), page, pageSize, await Repo<T>().CountAsync(filter, ct));
    public async Task<T> GetAsync<T>(int id, CancellationToken ct = default) where T : Entity =>
        Guard.Found(await Repo<T>().GetAsync(id, ct));
    public static TeamDto TeamView(Team x) => new(x.Id, x.Name, x.Code, x.City, x.IsActive, x.Version);
    public static EventDto EventView(SportEvent x) => new(x.Id, x is MatchEvent ? "Match" : "Community",
        x.Title, x.VenueId, x.StartsAtUtc, x.EndsAtUtc, x.Status.ToString(), (x as MatchEvent)?.TournamentId,
        (x as MatchEvent)?.HomeTeamId, (x as MatchEvent)?.AwayTeamId, (x as MatchEvent)?.HomeScore,
        (x as MatchEvent)?.AwayScore, x.Version);

    public async Task<TeamDto> CreateTeamAsync(TeamRequest request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        Guard.Against(await Repo<Team>().AnyAsync(x => x.Code == code, ct), "Team code is already in use.");
        var team = new Team { Name = request.Name.Trim(), Code = code, City = request.City.Trim() };
        Repo<Team>().Add(team); await uow.SaveChangesAsync(ct);
        return TeamView(team);
    }
    public async Task<TeamDto> UpdateTeamAsync(int id, UpdateTeamRequest request, CancellationToken ct)
    {
        var team = await GetAsync<Team>(id, ct); Guard.Version(team, request.Version);
        team.Name = request.Name.Trim(); team.City = request.City.Trim();
        await uow.SaveChangesAsync(ct); return TeamView(team);
    }
    public async Task ArchiveTeamAsync(int id, Guid version, CancellationToken ct)
    {
        var team = await GetAsync<Team>(id, ct); Guard.Version(team, version);
        team.IsActive = false; await uow.SaveChangesAsync(ct);
    }
    public async Task<Player> CreatePlayerAsync(PlayerRequest request, CancellationToken ct)
    {
        Guard.Against(request.BirthDate < new DateOnly(1900, 1, 1) || request.BirthDate >= DateOnly.FromDateTime(Now),
            "Birth date must be a past date on or after 1900-01-01.");
        var player = new Player { FullName = request.FullName.Trim(), BirthDate = request.BirthDate,
            Nationality = request.Nationality.Trim(), Position = request.Position };
        Repo<Player>().Add(player); await uow.SaveChangesAsync(ct); return player;
    }
    public Task<PlayerMembership> TransferAsync(int playerId, MembershipRequest request, CancellationToken ct) =>
        uow.SerializableAsync(async () => {
            var player = await GetAsync<Player>(playerId, ct);
            var team = await GetAsync<Team>(request.TeamId, ct);
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
                await uow.SaveChangesAsync(ct);
            }
            var membership = new PlayerMembership { PlayerId = playerId, TeamId = request.TeamId, JoinedOn = request.JoinedOn };
            Repo<PlayerMembership>().Add(membership); await uow.SaveChangesAsync(ct); return membership;
        }, ct);
    public async Task<Tournament> CreateTournamentAsync(TournamentRequest request, int actorId, CancellationToken ct)
    {
        Guard.Against(request.EndDate < request.StartDate, "Tournament end date must not precede its start date.");
        var tournament = new Tournament { Name = request.Name.Trim(), Season = request.Season.Trim(),
            StartDate = request.StartDate, EndDate = request.EndDate, OrganizerId = actorId };
        Repo<Tournament>().Add(tournament); await uow.SaveChangesAsync(ct); return tournament;
    }
    public Task<TournamentTeam> RegisterTeamAsync(int tournamentId, RegisterTeamRequest request, CancellationToken ct) =>
        uow.SerializableAsync(async () => {
            var tournament = await GetAsync<Tournament>(tournamentId, ct);
            Guard.Against(tournament.Status != TournamentStatus.RegistrationOpen, "Registration is closed.");
            Guard.Against(!(await GetAsync<Team>(request.TeamId, ct)).IsActive, "Team is archived.");
            Guard.Against(await Repo<TournamentTeam>().AnyAsync(x => x.TournamentId == tournamentId && x.TeamId == request.TeamId, ct),
                "Team is already registered.");
            var item = new TournamentTeam { TournamentId = tournamentId, TeamId = request.TeamId };
            Repo<TournamentTeam>().Add(item); await uow.SaveChangesAsync(ct); return item;
        }, ct);
    public Task<TournamentRoster> AddRosterAsync(int tournamentId, RosterRequest request, CancellationToken ct) =>
        uow.SerializableAsync(async () => {
            var tournament = await GetAsync<Tournament>(tournamentId, ct);
            Guard.Against(tournament.Status != TournamentStatus.RegistrationOpen, "Rosters are locked after registration closes.");
            Guard.Against(!await Repo<TournamentTeam>().AnyAsync(x => x.TournamentId == tournamentId && x.TeamId == request.TeamId, ct),
                "Team is not registered in this tournament.");
            await GetAsync<Player>(request.PlayerId, ct);
            Guard.Against(!await Repo<PlayerMembership>().AnyAsync(x => x.PlayerId == request.PlayerId &&
                x.TeamId == request.TeamId && x.LeftOn == null, ct), "Player must have an active membership in this team.");
            var roster = new TournamentRoster { TournamentId = tournamentId, TeamId = request.TeamId,
                PlayerId = request.PlayerId, ShirtNumber = request.ShirtNumber };
            Repo<TournamentRoster>().Add(roster); await uow.SaveChangesAsync(ct); return roster;
        }, ct);
    public Task<Tournament> StartTournamentAsync(int id, Guid version, CancellationToken ct) =>
        uow.SerializableAsync(async () => {
            var tournament = await GetAsync<Tournament>(id, ct); Guard.Version(tournament, version);
            Guard.Against(tournament.Status != TournamentStatus.RegistrationOpen, "Tournament must be open for registration.");
            Guard.Against(await Repo<TournamentTeam>().CountAsync(x => x.TournamentId == id, ct) < 2, "Register at least two teams first.");
            Guard.Against(!await Repo<MatchEvent>().AnyAsync(x => x.TournamentId == id, ct), "Schedule at least one match first.");
            tournament.Status = TournamentStatus.InProgress; await uow.SaveChangesAsync(ct); return tournament;
        }, ct);
    public async Task<Venue> CreateVenueAsync(VenueRequest request, CancellationToken ct)
    {
        var venue = new Venue { Name = request.Name.Trim(), City = request.City.Trim(), Address = request.Address.Trim(), Capacity = request.Capacity };
        Repo<Venue>().Add(venue); await uow.SaveChangesAsync(ct); return venue;
    }
    private async Task ValidateScheduleAsync(int venueId, DateTime start, DateTime end,
        int? home, int? away, CancellationToken ct)
    {
        Guard.Against(start.Kind != DateTimeKind.Utc || end.Kind != DateTimeKind.Utc || end <= start,
            "Supply UTC timestamps ending in Z; event end must follow start.");
        await GetAsync<Venue>(venueId, ct);
        Guard.Against(await queries.HasScheduleConflictAsync(venueId, start, end, home, away, ct),
            "Venue or team already has an overlapping event.", "schedule_conflict");
    }
    public Task<EventDto> CreateMatchAsync(MatchRequest request, int actorId, CancellationToken ct) =>
        uow.SerializableAsync(async () => {
            Guard.Against(request.HomeTeamId == request.AwayTeamId, "A team cannot play itself.");
            var tournament = await GetAsync<Tournament>(request.TournamentId, ct);
            Guard.Against(tournament.Status is TournamentStatus.Draft or TournamentStatus.Completed, "Tournament is not open for scheduling.");
            Guard.Against(DateOnly.FromDateTime(request.StartsAtUtc) < tournament.StartDate ||
                DateOnly.FromDateTime(request.EndsAtUtc) > tournament.EndDate, "Match must fall within tournament dates in UTC.");
            var home = await GetAsync<Team>(request.HomeTeamId, ct);
            var away = await GetAsync<Team>(request.AwayTeamId, ct);
            Guard.Against(!home.IsActive || !away.IsActive, "Both teams must be active.");
            Guard.Against(!await Repo<TournamentTeam>().AnyAsync(x => x.TournamentId == request.TournamentId && x.TeamId == home.Id, ct) ||
                !await Repo<TournamentTeam>().AnyAsync(x => x.TournamentId == request.TournamentId && x.TeamId == away.Id, ct),
                "Both teams must be registered in this tournament.");
            await ValidateScheduleAsync(request.VenueId, request.StartsAtUtc, request.EndsAtUtc, home.Id, away.Id, ct);
            var match = new MatchEvent { Title = $"{home.Name} vs {away.Name}", TournamentId = tournament.Id,
                HomeTeamId = home.Id, AwayTeamId = away.Id, VenueId = request.VenueId, OrganizerId = actorId,
                StartsAtUtc = request.StartsAtUtc, EndsAtUtc = request.EndsAtUtc, RoundNumber = request.RoundNumber, Leg = request.Leg };
            Repo<MatchEvent>().Add(match); await uow.SaveChangesAsync(ct); return EventView(match);
        }, ct);
    public Task<EventDto> CreateCommunityAsync(CommunityEventRequest request, int actorId, CancellationToken ct) =>
        uow.SerializableAsync(async () => {
            await ValidateScheduleAsync(request.VenueId, request.StartsAtUtc, request.EndsAtUtc, null, null, ct);
            var item = new CommunityEvent { Title = request.Title.Trim(), Description = request.Description.Trim(),
                VenueId = request.VenueId, OrganizerId = actorId, StartsAtUtc = request.StartsAtUtc, EndsAtUtc = request.EndsAtUtc };
            Repo<CommunityEvent>().Add(item); await uow.SaveChangesAsync(ct); return EventView(item);
        }, ct);
    public Task<EventDto> ApproveResultAsync(int id, ResultRequest request, int actorId, CancellationToken ct) =>
        uow.SerializableAsync(async () => {
            var match = await GetAsync<MatchEvent>(id, ct); Guard.Version(match, request.Version);
            Guard.Against(match.Status == EventStatus.Cancelled || Now < match.EndsAtUtc, "Only ended, non-cancelled matches can receive results.");
            Guard.Against((await GetAsync<Tournament>(match.TournamentId, ct)).Status != TournamentStatus.InProgress,
                "Tournament must be in progress before publishing results.");
            Guard.Against(request.HomeScore < 0 || request.AwayScore < 0 || request.HomeScore > 99 || request.AwayScore > 99,
                "Scores must be between 0 and 99.");
            Guard.Against(await queries.RecordedGoalsAsync(id, match.HomeTeamId, null, ct) > request.HomeScore ||
                await queries.RecordedGoalsAsync(id, match.AwayTeamId, null, ct) > request.AwayScore,
                "Correct player goal statistics before lowering the result.");
            Repo<ResultAudit>().Add(new ResultAudit { MatchId = id, ActorId = actorId,
                PreviousHomeScore = match.HomeScore, PreviousAwayScore = match.AwayScore,
                HomeScore = request.HomeScore, AwayScore = request.AwayScore, Reason = request.Reason.Trim() });
            match.HomeScore = request.HomeScore; match.AwayScore = request.AwayScore; match.Status = EventStatus.Finished;
            await uow.SaveChangesAsync(ct); return EventView(match);
        }, ct);
    public Task<MatchPlayerStat> SetPlayerStatAsync(int matchId, PlayerStatRequest request, CancellationToken ct) =>
        uow.SerializableAsync(async () => {
            var match = await GetAsync<MatchEvent>(matchId, ct);
            Guard.Against(match.Status != EventStatus.Finished, "Publish the match result before player statistics.");
            Guard.Against(request.TeamId != match.HomeTeamId && request.TeamId != match.AwayTeamId,
                "Player team must be a participant in this match.");
            Guard.Against(!await Repo<TournamentRoster>().AnyAsync(x => x.TournamentId == match.TournamentId &&
                x.TeamId == request.TeamId && x.PlayerId == request.PlayerId, ct), "Player is not in the tournament roster for this team.");
            var score = request.TeamId == match.HomeTeamId ? match.HomeScore!.Value : match.AwayScore!.Value;
            Guard.Against(await queries.RecordedGoalsAsync(matchId, request.TeamId, request.PlayerId, ct) + request.Goals > score,
                "Attributed player goals cannot exceed the team's result. Own goals may remain unattributed.");
            Guard.Against(request.Assists > score, "Assists cannot exceed the team's result.");
            var stat = await Repo<MatchPlayerStat>().FindAsync(x => x.MatchId == matchId && x.PlayerId == request.PlayerId, ct);
            if (stat is null) { stat = new MatchPlayerStat { MatchId = matchId, TournamentId = match.TournamentId,
                TeamId = request.TeamId, PlayerId = request.PlayerId }; Repo<MatchPlayerStat>().Add(stat); }
            stat.Goals = request.Goals; stat.Assists = request.Assists; stat.YellowCards = request.YellowCards;
            stat.RedCards = request.RedCards; stat.MinutesPlayed = request.MinutesPlayed;
            await uow.SaveChangesAsync(ct); return stat;
        }, ct);
    public async Task<IReadOnlyList<StandingRow>> StandingsAsync(int id, CancellationToken ct)
    { await GetAsync<Tournament>(id, ct); return await queries.StandingsAsync(id, ct); }
    public async Task<IReadOnlyList<ScorerRow>> ScorersAsync(int id, CancellationToken ct)
    { await GetAsync<Tournament>(id, ct); return await queries.TopScorersAsync(id, ct); }
    public Task<TicketOffer> CreateOfferAsync(int eventId, TicketOfferRequest request, CancellationToken ct) =>
        uow.SerializableAsync(async () => {
            var item = await GetAsync<SportEvent>(eventId, ct);
            var venue = await GetAsync<Venue>(item.VenueId, ct);
            Guard.Against(item.Status != EventStatus.Scheduled || Now >= item.StartsAtUtc, "Ticket sales require a future scheduled event.");
            Guard.Against(request.Price < 0 || decimal.Round(request.Price, 2) != request.Price,
                "Price must be non-negative with at most two decimal places.");
            Guard.Against(request.Capacity < 1 || await queries.AllocatedCapacityAsync(eventId, ct) + request.Capacity > venue.Capacity,
                "Total ticket allocation cannot exceed venue capacity.");
            Guard.Against(request.SalesStartUtc.Kind != DateTimeKind.Utc || request.SalesEndUtc.Kind != DateTimeKind.Utc ||
                request.SalesEndUtc <= request.SalesStartUtc || request.SalesEndUtc > item.StartsAtUtc || request.SalesEndUtc <= Now,
                "Supply a valid UTC sales window ending no later than the event start.");
            var offer = new TicketOffer { EventId = eventId, Name = request.Name.Trim(), Price = request.Price,
                Capacity = request.Capacity, SalesStartUtc = request.SalesStartUtc, SalesEndUtc = request.SalesEndUtc };
            Repo<TicketOffer>().Add(offer); await uow.SaveChangesAsync(ct); return offer;
        }, ct);
    private async Task<BookingDto> BookingViewAsync(Booking b, CancellationToken ct) =>
        new(b.Id, b.TicketOfferId, b.Quantity, b.UnitPrice, b.Quantity * b.UnitPrice, "EGP", b.Status.ToString(),
            await queries.TicketsAsync(b.Id, ct));
    public Task<BookingDto> BookAsync(CreateBookingRequest request, int userId, CancellationToken ct) =>
        uow.SerializableAsync(async () => {
            Guard.Against(request.Quantity is < 1 or > 10, "Quantity must be between 1 and 10.");
            var previous = await Repo<Booking>().FindAsync(x => x.UserId == userId && x.IdempotencyKey == request.IdempotencyKey, ct);
            if (previous is not null)
            {
                Guard.Against(previous.TicketOfferId != request.TicketOfferId || previous.Quantity != request.Quantity,
                    "Idempotency key was already used with a different request.", "idempotency_conflict");
                return await BookingViewAsync(previous, ct);
            }
            var offer = await GetAsync<TicketOffer>(request.TicketOfferId, ct);
            var item = await GetAsync<SportEvent>(offer.EventId, ct);
            Guard.Against(item.Status != EventStatus.Scheduled || Now >= item.StartsAtUtc ||
                Now < offer.SalesStartUtc || Now >= offer.SalesEndUtc, "Ticket sales are closed.");
            Guard.Against(offer.Capacity - offer.SoldQuantity < request.Quantity, "Not enough tickets remain.", "sold_out");
            offer.SoldQuantity += request.Quantity;
            var booking = new Booking { UserId = userId, TicketOfferId = offer.Id, Quantity = request.Quantity,
                UnitPrice = offer.Price, IdempotencyKey = request.IdempotencyKey };
            Repo<Booking>().Add(booking); await uow.SaveChangesAsync(ct);
            for (var i = 0; i < request.Quantity; i++)
                Repo<Ticket>().Add(new Ticket { BookingId = booking.Id, Code = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) });
            await uow.SaveChangesAsync(ct); return await BookingViewAsync(booking, ct);
        }, ct);
    public async Task<BookingDto> GetBookingAsync(int id, int userId, bool admin, CancellationToken ct)
    {
        var booking = await GetAsync<Booking>(id, ct);
        if (!admin && booking.UserId != userId) throw new BusinessException(404, "not_found", "Booking was not found.");
        return await BookingViewAsync(booking, ct);
    }
    public Task<BookingDto> CancelBookingAsync(int id, int userId, bool admin, CancellationToken ct) =>
        uow.SerializableAsync(async () => {
            var booking = await GetAsync<Booking>(id, ct);
            if (!admin && booking.UserId != userId) throw new BusinessException(404, "not_found", "Booking was not found.");
            if (booking.Status == BookingStatus.Cancelled) return await BookingViewAsync(booking, ct);
            var offer = await GetAsync<TicketOffer>(booking.TicketOfferId, ct);
            var item = await GetAsync<SportEvent>(offer.EventId, ct);
            Guard.Against(Now >= item.StartsAtUtc.AddHours(-2), "Cancellations close two hours before the event.");
            Guard.Against(await Repo<Ticket>().AnyAsync(x => x.BookingId == id && x.CheckedInUtc != null, ct),
                "A checked-in booking cannot be cancelled.");
            booking.Status = BookingStatus.Cancelled; booking.CancelledUtc = Now; offer.SoldQuantity -= booking.Quantity;
            await uow.SaveChangesAsync(ct); return await BookingViewAsync(booking, ct);
        }, ct);
    public Task<TicketDto> CheckInAsync(CheckInRequest request, int eventId, int actorId, CancellationToken ct) =>
        uow.SerializableAsync(async () => {
            var code = request.Code.ToUpperInvariant();
            var ticket = Guard.Found(await Repo<Ticket>().FindAsync(x => x.Code == code, ct));
            var booking = await GetAsync<Booking>(ticket.BookingId, ct);
            var offer = await GetAsync<TicketOffer>(booking.TicketOfferId, ct);
            Guard.Against(offer.EventId != eventId, "Ticket belongs to another event.");
            var item = await GetAsync<SportEvent>(eventId, ct);
            Guard.Against(booking.Status != BookingStatus.Confirmed || item.Status == EventStatus.Cancelled,
                "Ticket or event is cancelled.");
            Guard.Against(ticket.CheckedInUtc is not null, "Ticket has already been used.", "already_checked_in");
            Guard.Against(Now < item.StartsAtUtc.AddHours(-2) || Now > item.EndsAtUtc, "Check-in is outside the event admission window.");
            ticket.CheckedInUtc = Now; ticket.CheckedInById = actorId;
            await uow.SaveChangesAsync(ct); return new TicketDto(ticket.Id, ticket.Code, ticket.CheckedInUtc);
        }, ct);
}
