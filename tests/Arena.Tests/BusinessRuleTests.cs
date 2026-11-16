using Arena.Application;
using Arena.Domain;
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
        var first=await f.Service.BookAsync(request,f.Fan.Id,default);
        data.Offer.Price=999m;await f.Db.SaveChangesAsync();
        var retry=await f.Service.BookAsync(request,f.Fan.Id,default);
        Assert.Equal(first.Id,retry.Id);Assert.Equal(201m,retry.TotalAmount);
        Assert.Equal(2,await f.Db.Tickets.CountAsync());Assert.Equal(2,data.Offer.SoldQuantity);
    }
    [Fact]
    public async Task IdempotencyKey_CannotChangeRequestBody()
    {
        await using var f=await TestDatabase.CreateAsync(); var data=await f.AddOfferAsync();
        await f.Service.BookAsync(new(data.Offer.Id,1,"repeat-key-123"),f.Fan.Id,default);
        var error=await Assert.ThrowsAsync<BusinessException>(()=>f.Service.BookAsync(new(data.Offer.Id,2,"repeat-key-123"),f.Fan.Id,default));
        Assert.Equal("idempotency_conflict",error.Code); Assert.Equal(1,await f.Db.Bookings.CountAsync());
    }
    [Fact]
    public async Task Booking_RejectsOverbooking_WithoutPartialRows()
    {
        await using var f=await TestDatabase.CreateAsync();var data=await f.AddOfferAsync(1);
        var error=await Assert.ThrowsAsync<BusinessException>(()=>f.Service.BookAsync(new(data.Offer.Id,2,"stock-request-12"),f.Fan.Id,default));
        Assert.Equal("sold_out",error.Code);Assert.Empty(await f.Db.Bookings.ToListAsync());Assert.Equal(0,data.Offer.SoldQuantity);
    }
    [Fact]
    public async Task Cancellation_ReleasesStockExactlyOnce()
    {
        await using var f=await TestDatabase.CreateAsync();var data=await f.AddOfferAsync();
        var booking=await f.Service.BookAsync(new(data.Offer.Id,3,"cancel-request-1"),f.Fan.Id,default);
        await f.Service.CancelBookingAsync(booking.Id,f.Fan.Id,false,default);
        await f.Service.CancelBookingAsync(booking.Id,f.Fan.Id,false,default);
        Assert.Equal(0,data.Offer.SoldQuantity);Assert.Equal(3,await f.Db.Tickets.CountAsync());
    }
    [Fact]
    public async Task Booking_IsPrivateToOwnerOrAdmin()
    {
        await using var f=await TestDatabase.CreateAsync();var data=await f.AddOfferAsync();
        var booking=await f.Service.BookAsync(new(data.Offer.Id,1,"private-request-1"),f.Fan.Id,default);
        var error=await Assert.ThrowsAsync<BusinessException>(()=>f.Service.GetBookingAsync(booking.Id,f.OtherFan.Id,false,default));
        Assert.Equal(404,error.StatusCode);
    }
    [Fact]
    public async Task CheckIn_IsSingleUse_AndChecksEvent()
    {
        await using var f=await TestDatabase.CreateAsync();var data=await f.AddOfferAsync();
        var booking=await f.Service.BookAsync(new(data.Offer.Id,1,"checkin-request-1"),f.Fan.Id,default);
        f.Clock.Utc=data.Event.StartsAtUtc.AddMinutes(-30);
        await Assert.ThrowsAsync<BusinessException>(()=>f.Service.CheckInAsync(new(booking.Tickets[0].Code),data.Event.Id+1,f.Admin.Id,default));
        var scanned=await f.Service.CheckInAsync(new(booking.Tickets[0].Code),data.Event.Id,f.Admin.Id,default);
        Assert.NotNull(scanned.CheckedInUtc);
        var error=await Assert.ThrowsAsync<BusinessException>(()=>f.Service.CheckInAsync(new(booking.Tickets[0].Code),data.Event.Id,f.Admin.Id,default));
        Assert.Equal("already_checked_in",error.Code);
    }
    [Fact]
    public async Task CancelledTicket_CannotBeCheckedIn()
    {
        await using var f=await TestDatabase.CreateAsync();var data=await f.AddOfferAsync();
        var booking=await f.Service.BookAsync(new(data.Offer.Id,1,"cancel-checkin-1"),f.Fan.Id,default);
        await f.Service.CancelBookingAsync(booking.Id,f.Fan.Id,false,default);
        f.Clock.Utc=data.Event.StartsAtUtc;
        await Assert.ThrowsAsync<BusinessException>(()=>f.Service.CheckInAsync(new(booking.Tickets[0].Code),data.Event.Id,f.Admin.Id,default));
    }
    [Fact]
    public async Task Cancellation_ClosesTwoHoursBeforeStart()
    {
        await using var f=await TestDatabase.CreateAsync();var data=await f.AddOfferAsync();
        var booking=await f.Service.BookAsync(new(data.Offer.Id,1,"cutoff-request-1"),f.Fan.Id,default);
        f.Clock.Utc=data.Event.StartsAtUtc.AddHours(-2);
        await Assert.ThrowsAsync<BusinessException>(()=>f.Service.CancelBookingAsync(booking.Id,f.Fan.Id,false,default));
        Assert.Equal(1,data.Offer.SoldQuantity);
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
        await f.Service.AddRosterAsync(c.Tournament.Id,new(c.Home.Id,c.Player.Id,9),default);
        f.Db.TournamentRosters.Add(new(){TournamentId=c.Tournament.Id,TeamId=c.Away.Id,PlayerId=c.Player.Id,ShirtNumber=10});
        await Assert.ThrowsAsync<DbUpdateException>(()=>f.Db.SaveChangesAsync());
    }
    [Fact]
    public async Task Schedule_RejectsVenueAndTeamOverlaps()
    {
        await using var f=await TestDatabase.CreateAsync();var c=await f.CompetitionAsync();
        var start=f.Clock.Utc.AddDays(2);
        await f.Service.CreateMatchAsync(new(c.Tournament.Id,c.Home.Id,c.Away.Id,c.Venue.Id,start,start.AddHours(2),1),f.Admin.Id,default);
        var error=await Assert.ThrowsAsync<BusinessException>(()=>f.Service.CreateCommunityAsync(new("Fan event","Football event",c.Venue.Id,start.AddHours(1),start.AddHours(3)),f.Admin.Id,default));
        Assert.Equal("schedule_conflict",error.Code);
    }
    [Fact]
    public async Task ResultCorrection_RecalculatesStandings_AndCreatesAudit()
    {
        await using var f=await TestDatabase.CreateAsync();var c=await f.CompetitionAsync();
        var match=await f.Service.CreateMatchAsync(new(c.Tournament.Id,c.Home.Id,c.Away.Id,c.Venue.Id,f.Clock.Utc.AddDays(-2),f.Clock.Utc.AddDays(-2).AddHours(2),1),f.Admin.Id,default);
        await f.Service.StartTournamentAsync(c.Tournament.Id,c.Tournament.Version,default);
        var result=await f.Service.ApproveResultAsync(match.Id,new(2,0,"Initial official result",match.Version),f.Admin.Id,default);
        await f.Service.ApproveResultAsync(match.Id,new(0,1,"Correction after review",result.Version),f.Admin.Id,default);
        var rows=await f.Service.StandingsAsync(c.Tournament.Id,default);
        Assert.Equal(c.Away.Id,rows[0].TeamId);Assert.Equal(3,rows[0].Points);Assert.Equal(1,rows[0].Played);
        Assert.Equal(2,await f.Db.ResultAudits.CountAsync());
    }
    [Fact]
    public async Task Transfer_PreservesHistory_AndOneActiveMembership()
    {
        await using var f=await TestDatabase.CreateAsync();var c=await f.CompetitionAsync();
        await f.Service.TransferAsync(c.Player.Id,new(c.Away.Id,new(2026,11,10)),default);
        Assert.Equal(2,await f.Db.PlayerMemberships.CountAsync());
        Assert.Equal(c.Away.Id,(await f.Db.PlayerMemberships.SingleAsync(x=>x.LeftOn==null)).TeamId);
    }
    [Fact]
    public async Task OfferAllocation_CannotExceedVenueCapacity()
    {
        await using var f=await TestDatabase.CreateAsync();var data=await f.AddOfferAsync(90);
        await Assert.ThrowsAsync<BusinessException>(()=>f.Service.CreateOfferAsync(data.Event.Id,
            new("VIP",200m,11,f.Clock.Utc.AddHours(-1),data.Event.StartsAtUtc),default));
        Assert.Equal(1,await f.Db.TicketOffers.CountAsync());
    }
    [Fact]
    public async Task Scorers_ExecuteAsRelationalQuery_AndAggregateFinalResults()
    {
        await using var f = await TestDatabase.CreateAsync(); var c = await f.CompetitionAsync();
        await f.Service.AddRosterAsync(c.Tournament.Id, new(c.Home.Id, c.Player.Id, 9), default);
        var match = await f.Service.CreateMatchAsync(new(c.Tournament.Id, c.Home.Id, c.Away.Id, c.Venue.Id,
            f.Clock.Utc.AddDays(-2), f.Clock.Utc.AddDays(-2).AddHours(2), 1), f.Admin.Id, default);
        await f.Service.StartTournamentAsync(c.Tournament.Id, c.Tournament.Version, default);
        await f.Service.ApproveResultAsync(match.Id, new(2, 0, "Official result", match.Version), f.Admin.Id, default);
        await f.Service.SetPlayerStatAsync(match.Id, new(c.Player.Id, c.Home.Id, 2, 0, 0, 0, 90), default);
        var scorer = Assert.Single(await f.Service.ScorersAsync(c.Tournament.Id, default));
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
