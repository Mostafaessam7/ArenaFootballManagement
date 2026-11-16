using Arena.Application.Common;
using Arena.Application.Features.Bookings;
using Arena.Application.Features.Tournaments;
using Arena.Domain.Entities;
using Arena.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Arena.Tests;

public sealed class BusinessRuleTests
{
    [Fact]
    public void Standings_ExcludeScheduledResults_AndUseThreeOneZero()
    {
        var teams = new Dictionary<int,string> { [1] = "A", [2] = "B", [3] = "C" };
        var matches = new[] {
            new MatchEvent { HomeTeamId=1,AwayTeamId=2,HomeScore=2,AwayScore=0,Status=EventStatus.Finished },
            new MatchEvent { HomeTeamId=2,AwayTeamId=3,HomeScore=1,AwayScore=1,Status=EventStatus.Finished },
            new MatchEvent { HomeTeamId=3,AwayTeamId=1,HomeScore=9,AwayScore=0,Status=EventStatus.Scheduled }
        };
        var rows = StandingsCalculator.Calculate(teams,matches);
        Assert.Equal(new[]{1,3,2},rows.Select(x=>x.TeamId));
        Assert.Equal(3,rows[0].Points); Assert.Equal(1,rows[1].Points);
        Assert.Equal(2,rows.Single(x=>x.TeamId==2).Played);
    }
    [Fact]
    public void Standings_TiesUseGoalDifferenceThenGoalsThenStableId()
    {
        var teams = new Dictionary<int,string> { [9]="Z",[2]="B",[1]="A" };
        Assert.Equal(new[]{1,2,9},StandingsCalculator.Calculate(teams,[]).Select(x=>x.TeamId));
    }
    [Fact]
    public async Task Booking_IsAtomic_HasPriceSnapshot_AndIsIdempotent()
    {
        await using var f=await TestDatabase.CreateAsync(); var data=await f.AddOfferAsync();
        var request=new CreateBookingRequest(data.Offer.Id,2,"same-request-123");
        var first=await f.Bookings.BookAsync(request,f.Fan.Id,default);
        data.Offer.Price=999m;await f.Db.SaveChangesAsync();
        var retry=await f.Bookings.BookAsync(request,f.Fan.Id,default);
        Assert.Equal(first.Id,retry.Id);Assert.Equal(201m,retry.TotalAmount);
        Assert.Equal(2,await f.Db.Tickets.CountAsync());Assert.Equal(2,data.Offer.SoldQuantity);
    }
    [Fact]
    public async Task IdempotencyKey_CannotChangeRequestBody()
    {
        await using var f=await TestDatabase.CreateAsync(); var data=await f.AddOfferAsync();
        await f.Bookings.BookAsync(new(data.Offer.Id,1,"repeat-key-123"),f.Fan.Id,default);
        var error=await Assert.ThrowsAsync<BusinessException>(()=>f.Bookings.BookAsync(new(data.Offer.Id,2,"repeat-key-123"),f.Fan.Id,default));
        Assert.Equal("idempotency_conflict",error.Code); Assert.Equal(1,await f.Db.Bookings.CountAsync());
    }
    [Fact]
    public async Task Booking_RejectsOverbooking_WithoutPartialRows()
    {
        await using var f=await TestDatabase.CreateAsync();var data=await f.AddOfferAsync(1);
        var error=await Assert.ThrowsAsync<BusinessException>(()=>f.Bookings.BookAsync(new(data.Offer.Id,2,"stock-request-12"),f.Fan.Id,default));
        Assert.Equal("sold_out",error.Code);Assert.Empty(await f.Db.Bookings.ToListAsync());Assert.Equal(0,data.Offer.SoldQuantity);
    }
    [Fact]
    public async Task Cancellation_ReleasesStockExactlyOnce()
    {
        await using var f=await TestDatabase.CreateAsync();var data=await f.AddOfferAsync();
        var booking=await f.Bookings.BookAsync(new(data.Offer.Id,3,"cancel-request-1"),f.Fan.Id,default);
        await f.Bookings.CancelAsync(booking.Id,f.Fan.Id,false,default);
        await f.Bookings.CancelAsync(booking.Id,f.Fan.Id,false,default);
        Assert.Equal(0,data.Offer.SoldQuantity);Assert.Equal(3,await f.Db.Tickets.CountAsync());
    }
    [Fact]
    public async Task Booking_IsPrivateToOwnerOrAdmin()
    {
        await using var f=await TestDatabase.CreateAsync();var data=await f.AddOfferAsync();
        var booking=await f.Bookings.BookAsync(new(data.Offer.Id,1,"private-request-1"),f.Fan.Id,default);
        var error=await Assert.ThrowsAsync<BusinessException>(()=>f.Bookings.GetAsync(booking.Id,f.OtherFan.Id,false,default));
        Assert.Equal(404,error.StatusCode);
    }
    [Fact]
    public async Task CheckIn_IsSingleUse_AndChecksEvent()
    {
        await using var f=await TestDatabase.CreateAsync();var data=await f.AddOfferAsync();
        var booking=await f.Bookings.BookAsync(new(data.Offer.Id,1,"checkin-request-1"),f.Fan.Id,default);
        f.Clock.Utc=data.Event.StartsAtUtc.AddMinutes(-30);
        await Assert.ThrowsAsync<BusinessException>(()=>f.Tickets.CheckInAsync(new(booking.Tickets[0].Code),data.Event.Id+1,f.Admin.Id,default));
        var scanned=await f.Tickets.CheckInAsync(new(booking.Tickets[0].Code),data.Event.Id,f.Admin.Id,default);
        Assert.NotNull(scanned.CheckedInUtc);
        var error=await Assert.ThrowsAsync<BusinessException>(()=>f.Tickets.CheckInAsync(new(booking.Tickets[0].Code),data.Event.Id,f.Admin.Id,default));
        Assert.Equal("already_checked_in",error.Code);
    }
    [Fact]
    public async Task CancelledTicket_CannotBeCheckedIn()
    {
        await using var f=await TestDatabase.CreateAsync();var data=await f.AddOfferAsync();
        var booking=await f.Bookings.BookAsync(new(data.Offer.Id,1,"cancel-checkin-1"),f.Fan.Id,default);
        await f.Bookings.CancelAsync(booking.Id,f.Fan.Id,false,default);
        f.Clock.Utc=data.Event.StartsAtUtc;
        await Assert.ThrowsAsync<BusinessException>(()=>f.Tickets.CheckInAsync(new(booking.Tickets[0].Code),data.Event.Id,f.Admin.Id,default));
    }
    [Fact]
    public async Task Cancellation_ClosesTwoHoursBeforeStart()
    {
        await using var f=await TestDatabase.CreateAsync();var data=await f.AddOfferAsync();
        var booking=await f.Bookings.BookAsync(new(data.Offer.Id,1,"cutoff-request-1"),f.Fan.Id,default);
        f.Clock.Utc=data.Event.StartsAtUtc.AddHours(-2);
        await Assert.ThrowsAsync<BusinessException>(()=>f.Bookings.CancelAsync(booking.Id,f.Fan.Id,false,default));
        Assert.Equal(1,data.Offer.SoldQuantity);
    }
    [Fact]
    public async Task CheckedInBooking_CannotBeCancelled_AndStockIsKept()
    {
        await using var f=await TestDatabase.CreateAsync();var data=await f.AddOfferAsync();
        var booking=await f.Bookings.BookAsync(new(data.Offer.Id,2,"checked-cancel-1"),f.Fan.Id,default);
        // A gate check-in committed by another request (written through a separate context).
        await using(var gate=f.NewContext())
        {
            var ticket=await gate.Tickets.SingleAsync(x=>x.Id==booking.Tickets[0].Id);
            ticket.CheckedInUtc=f.Clock.Utc;ticket.CheckedInById=f.Admin.Id;await gate.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<BusinessException>(()=>f.Bookings.CancelAsync(booking.Id,f.Fan.Id,false,default));
        // The rejected cancel rolled back completely: booking still confirmed, stock not released.
        await using var check=f.NewContext();
        Assert.Equal(2,(await check.TicketOffers.SingleAsync(x=>x.Id==data.Offer.Id)).SoldQuantity);
        Assert.Equal(BookingStatus.Confirmed,(await check.Bookings.SingleAsync(x=>x.Id==booking.Id)).Status);
    }
    [Fact]
    public async Task Inventory_ReservesOnlyWhatRemains_AndReleaseRestoresIt()
    {
        await using var f=await TestDatabase.CreateAsync();var data=await f.AddOfferAsync(3);
        var inventory=new Arena.Infrastructure.Repositories.TicketInventory(f.Db);
        Assert.True(await inventory.TryReserveAsync(data.Offer.Id,2,default));
        Assert.False(await inventory.TryReserveAsync(data.Offer.Id,2,default));
        Assert.True(await inventory.TryReserveAsync(data.Offer.Id,1,default));
        Assert.Equal(3,data.Offer.SoldQuantity);
        await inventory.ReleaseAsync(data.Offer.Id,3,default);
        Assert.Equal(0,data.Offer.SoldQuantity);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>inventory.ReleaseAsync(data.Offer.Id,1,default));
    }
    [Fact]
    public async Task Concurrency_StaleInventoryWriteIsRejected()
    {
        await using var f=await TestDatabase.CreateAsync();var data=await f.AddOfferAsync(1);
        await using var first=f.NewContext();await using var second=f.NewContext();
        var a=await first.TicketOffers.SingleAsync();var b=await second.TicketOffers.SingleAsync();
        a.SoldQuantity=1;await first.SaveChangesAsync();b.SoldQuantity=1;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>second.SaveChangesAsync());
    }
    [Fact]
    public async Task Database_EnforcesInventoryBounds()
    {
        await using var f=await TestDatabase.CreateAsync();var data=await f.AddOfferAsync(1);
        data.Offer.SoldQuantity=2;
        await Assert.ThrowsAsync<DbUpdateException>(()=>f.Db.SaveChangesAsync());
    }
    [Fact]
    public async Task Roster_RejectsSecondTeamInSameTournament()
    {
        await using var f=await TestDatabase.CreateAsync();var c=await f.CompetitionAsync();
        await f.Tournaments.AddRosterAsync(c.Tournament.Id,new(c.Home.Id,c.Player.Id,9),default);
        f.Db.TournamentRosters.Add(new(){TournamentId=c.Tournament.Id,TeamId=c.Away.Id,PlayerId=c.Player.Id,ShirtNumber=10});
        await Assert.ThrowsAsync<DbUpdateException>(()=>f.Db.SaveChangesAsync());
    }
    [Fact]
    public async Task Schedule_RejectsVenueAndTeamOverlaps()
    {
        await using var f=await TestDatabase.CreateAsync();var c=await f.CompetitionAsync();
        var start=f.Clock.Utc.AddDays(2);
        await f.Events.CreateMatchAsync(new(c.Tournament.Id,c.Home.Id,c.Away.Id,c.Venue.Id,start,start.AddHours(2),1),f.Admin.Id,default);
        var error=await Assert.ThrowsAsync<BusinessException>(()=>f.Events.CreateCommunityAsync(new("Fan event","Football event",c.Venue.Id,start.AddHours(1),start.AddHours(3)),f.Admin.Id,default));
        Assert.Equal("schedule_conflict",error.Code);
    }
    [Fact]
    public async Task ResultCorrection_RecalculatesStandings_AndCreatesAudit()
    {
        await using var f=await TestDatabase.CreateAsync();var c=await f.CompetitionAsync();
        var match=await f.Events.CreateMatchAsync(new(c.Tournament.Id,c.Home.Id,c.Away.Id,c.Venue.Id,f.Clock.Utc.AddDays(-2),f.Clock.Utc.AddDays(-2).AddHours(2),1),f.Admin.Id,default);
        await f.Tournaments.StartAsync(c.Tournament.Id,c.Tournament.Version,default);
        var result=await f.Results.ApproveAsync(match.Id,new(2,0,"Initial official result",match.Version),f.Admin.Id,default);
        await f.Results.ApproveAsync(match.Id,new(0,1,"Correction after review",result.Version),f.Admin.Id,default);
        var rows=await f.Tournaments.StandingsAsync(c.Tournament.Id,default);
        Assert.Equal(c.Away.Id,rows[0].TeamId);Assert.Equal(3,rows[0].Points);Assert.Equal(1,rows[0].Played);
        Assert.Equal(2,await f.Db.ResultAudits.CountAsync());
    }
    [Fact]
    public async Task Transfer_PreservesHistory_AndOneActiveMembership()
    {
        await using var f=await TestDatabase.CreateAsync();var c=await f.CompetitionAsync();
        await f.Players.TransferAsync(c.Player.Id,new(c.Away.Id,new(2026,11,10)),default);
        Assert.Equal(2,await f.Db.PlayerMemberships.CountAsync());
        Assert.Equal(c.Away.Id,(await f.Db.PlayerMemberships.SingleAsync(x=>x.LeftOn==null)).TeamId);
    }
    [Fact]
    public async Task OfferAllocation_CannotExceedVenueCapacity()
    {
        await using var f=await TestDatabase.CreateAsync();var data=await f.AddOfferAsync(90);
        await Assert.ThrowsAsync<BusinessException>(()=>f.Tickets.CreateOfferAsync(data.Event.Id,
            new("VIP",200m,11,f.Clock.Utc.AddHours(-1),data.Event.StartsAtUtc),default));
        Assert.Equal(1,await f.Db.TicketOffers.CountAsync());
    }
    [Fact]
    public async Task Scorers_ExecuteAsRelationalQuery_AndAggregateFinalResults()
    {
        await using var f = await TestDatabase.CreateAsync(); var c = await f.CompetitionAsync();
        await f.Tournaments.AddRosterAsync(c.Tournament.Id, new(c.Home.Id, c.Player.Id, 9), default);
        var match = await f.Events.CreateMatchAsync(new(c.Tournament.Id, c.Home.Id, c.Away.Id, c.Venue.Id,
            f.Clock.Utc.AddDays(-2), f.Clock.Utc.AddDays(-2).AddHours(2), 1), f.Admin.Id, default);
        await f.Tournaments.StartAsync(c.Tournament.Id, c.Tournament.Version, default);
        await f.Results.ApproveAsync(match.Id, new(2, 0, "Official result", match.Version), f.Admin.Id, default);
        await f.Results.SetPlayerStatAsync(match.Id, new(c.Player.Id, c.Home.Id, 2, 0, 0, 0, 90), default);
        var scorer = Assert.Single(await f.Tournaments.ScorersAsync(c.Tournament.Id, default));
        Assert.Equal(c.Player.Id, scorer.PlayerId); Assert.Equal(2, scorer.Goals); Assert.Equal(1, scorer.Appearances);
    }
    [Fact]
    public async Task FinishedMatch_RequiresScoresAtDatabaseLevel()
    {
        await using var f=await TestDatabase.CreateAsync();var c=await f.CompetitionAsync();
        f.Db.Add(new MatchEvent { Title="Invalid result",TournamentId=c.Tournament.Id,HomeTeamId=c.Home.Id,AwayTeamId=c.Away.Id,
            VenueId=c.Venue.Id,OrganizerId=f.Admin.Id,StartsAtUtc=f.Clock.Utc,EndsAtUtc=f.Clock.Utc.AddHours(2),RoundNumber=1,Status=EventStatus.Finished });
        await Assert.ThrowsAsync<DbUpdateException>(()=>f.Db.SaveChangesAsync());
    }
}
