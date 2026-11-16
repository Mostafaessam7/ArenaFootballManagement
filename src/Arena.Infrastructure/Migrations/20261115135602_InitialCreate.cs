using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arena.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Nationality = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_Role", "[Role] IN ('Admin','Organizer','TeamManager','Spectator','GateStaff')");
                });

            migrationBuilder.CreateTable(
                name: "Venues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    City = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venues", x => x.Id);
                    table.CheckConstraint("CK_Venue_Capacity", "[Capacity] > 0");
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    City = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ManagerId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_Users_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Season = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OrganizerId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                    table.CheckConstraint("CK_Tournament_Dates", "[EndDate] >= [StartDate]");
                    table.CheckConstraint("CK_Tournament_Status", "[Status] IN ('Draft','RegistrationOpen','InProgress','Completed')");
                    table.ForeignKey(
                        name: "FK_Tournaments_Users_OrganizerId",
                        column: x => x.OrganizerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlayerMemberships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    JoinedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    LeftOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerMemberships", x => x.Id);
                    table.CheckConstraint("CK_Membership_Dates", "[LeftOn] IS NULL OR [LeftOn] > [JoinedOn]");
                    table.ForeignKey(
                        name: "FK_PlayerMemberships_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlayerMemberships_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TournamentTeams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentTeams", x => x.Id);
                    table.UniqueConstraint("AK_TournamentTeams_TournamentId_TeamId", x => new { x.TournamentId, x.TeamId });
                    table.ForeignKey(
                        name: "FK_TournamentTeams_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentTeams_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    VenueId = table.Column<int>(type: "int", nullable: false),
                    OrganizerId = table.Column<int>(type: "int", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TournamentId = table.Column<int>(type: "int", nullable: true),
                    HomeTeamId = table.Column<int>(type: "int", nullable: true),
                    AwayTeamId = table.Column<int>(type: "int", nullable: true),
                    RoundNumber = table.Column<int>(type: "int", nullable: true),
                    Leg = table.Column<int>(type: "int", nullable: true),
                    HomeScore = table.Column<int>(type: "int", nullable: true),
                    AwayScore = table.Column<int>(type: "int", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.CheckConstraint("CK_Event_Dates", "[EndsAtUtc] > [StartsAtUtc]");
                    table.CheckConstraint("CK_Event_Specialization", "([EventType] = 'Match' AND [TournamentId] IS NOT NULL AND [HomeTeamId] IS NOT NULL AND [AwayTeamId] IS NOT NULL AND [RoundNumber] IS NOT NULL AND [Leg] IS NOT NULL AND [Description] IS NULL) OR ([EventType] = 'Community' AND [Description] IS NOT NULL AND [TournamentId] IS NULL AND [HomeTeamId] IS NULL AND [AwayTeamId] IS NULL AND [RoundNumber] IS NULL AND [Leg] IS NULL AND [HomeScore] IS NULL AND [AwayScore] IS NULL)");
                    table.CheckConstraint("CK_Event_Status", "[Status] IN ('Scheduled','Finished','Cancelled')");
                    table.CheckConstraint("CK_Match_Round", "[RoundNumber] IS NULL OR ([RoundNumber] > 0 AND [Leg] BETWEEN 1 AND 2)");
                    table.CheckConstraint("CK_Match_Scores", "[EventType] <> 'Match' OR (([Status] = 'Finished' AND [HomeScore] IS NOT NULL AND [AwayScore] IS NOT NULL AND [HomeScore] BETWEEN 0 AND 99 AND [AwayScore] BETWEEN 0 AND 99) OR ([Status] <> 'Finished' AND [HomeScore] IS NULL AND [AwayScore] IS NULL))");
                    table.CheckConstraint("CK_Match_Teams", "[HomeTeamId] IS NULL OR [HomeTeamId] <> [AwayTeamId]");
                    table.ForeignKey(
                        name: "FK_Events_TournamentTeams_TournamentId_AwayTeamId",
                        columns: x => new { x.TournamentId, x.AwayTeamId },
                        principalTable: "TournamentTeams",
                        principalColumns: new[] { "TournamentId", "TeamId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_TournamentTeams_TournamentId_HomeTeamId",
                        columns: x => new { x.TournamentId, x.HomeTeamId },
                        principalTable: "TournamentTeams",
                        principalColumns: new[] { "TournamentId", "TeamId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Users_OrganizerId",
                        column: x => x.OrganizerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TournamentRosters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    ShirtNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentRosters", x => x.Id);
                    table.UniqueConstraint("AK_TournamentRosters_TournamentId_TeamId_PlayerId", x => new { x.TournamentId, x.TeamId, x.PlayerId });
                    table.CheckConstraint("CK_Roster_Shirt", "[ShirtNumber] BETWEEN 1 AND 99");
                    table.ForeignKey(
                        name: "FK_TournamentRosters_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentRosters_TournamentTeams_TournamentId_TeamId",
                        columns: x => new { x.TournamentId, x.TeamId },
                        principalTable: "TournamentTeams",
                        principalColumns: new[] { "TournamentId", "TeamId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResultAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<int>(type: "int", nullable: false),
                    PreviousHomeScore = table.Column<int>(type: "int", nullable: true),
                    PreviousAwayScore = table.Column<int>(type: "int", nullable: true),
                    HomeScore = table.Column<int>(type: "int", nullable: false),
                    AwayScore = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultAudits_Events_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResultAudits_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketOffers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    SoldQuantity = table.Column<int>(type: "int", nullable: false),
                    SalesStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SalesEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketOffers", x => x.Id);
                    table.CheckConstraint("CK_Offer_Dates", "[SalesEndUtc] > [SalesStartUtc]");
                    table.CheckConstraint("CK_Offer_Inventory", "[Capacity] > 0 AND [SoldQuantity] >= 0 AND [SoldQuantity] <= [Capacity]");
                    table.CheckConstraint("CK_Offer_Price", "[Price] >= 0");
                    table.ForeignKey(
                        name: "FK_TicketOffers_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatchPlayerStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    Goals = table.Column<int>(type: "int", nullable: false),
                    Assists = table.Column<int>(type: "int", nullable: false),
                    YellowCards = table.Column<int>(type: "int", nullable: false),
                    RedCards = table.Column<int>(type: "int", nullable: false),
                    MinutesPlayed = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPlayerStats", x => x.Id);
                    table.CheckConstraint("CK_PlayerStats_Range", "[Goals] BETWEEN 0 AND 99 AND [Assists] BETWEEN 0 AND 99 AND [YellowCards] BETWEEN 0 AND 2 AND [RedCards] BETWEEN 0 AND 1 AND [MinutesPlayed] BETWEEN 0 AND 130");
                    table.ForeignKey(
                        name: "FK_MatchPlayerStats_Events_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchPlayerStats_TournamentRosters_TournamentId_TeamId_PlayerId",
                        columns: x => new { x.TournamentId, x.TeamId, x.PlayerId },
                        principalTable: "TournamentRosters",
                        principalColumns: new[] { "TournamentId", "TeamId", "PlayerId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TicketOfferId = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CancelledUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.CheckConstraint("CK_Booking_Quantity", "[Quantity] BETWEEN 1 AND 10 AND [UnitPrice] >= 0");
                    table.CheckConstraint("CK_Booking_Status", "([Status] = 'Confirmed' AND [CancelledUtc] IS NULL) OR ([Status] = 'Cancelled' AND [CancelledUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Bookings_TicketOffers_TicketOfferId",
                        column: x => x.TicketOfferId,
                        principalTable: "TicketOffers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CheckedInUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedInById = table.Column<int>(type: "int", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.CheckConstraint("CK_Ticket_CheckIn", "([CheckedInUtc] IS NULL AND [CheckedInById] IS NULL) OR ([CheckedInUtc] IS NOT NULL AND [CheckedInById] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Tickets_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_Users_CheckedInById",
                        column: x => x.CheckedInById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TicketOfferId",
                table: "Bookings",
                column: "TicketOfferId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_UserId_IdempotencyKey",
                table: "Bookings",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_OrganizerId",
                table: "Events",
                column: "OrganizerId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_TournamentId_AwayTeamId",
                table: "Events",
                columns: new[] { "TournamentId", "AwayTeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_TournamentId_HomeTeamId_AwayTeamId_Leg",
                table: "Events",
                columns: new[] { "TournamentId", "HomeTeamId", "AwayTeamId", "Leg" },
                unique: true,
                filter: "[EventType] = 'Match'");

            migrationBuilder.CreateIndex(
                name: "IX_Events_VenueId_StartsAtUtc",
                table: "Events",
                columns: new[] { "VenueId", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayerStats_MatchId_PlayerId",
                table: "MatchPlayerStats",
                columns: new[] { "MatchId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayerStats_TournamentId_TeamId_PlayerId",
                table: "MatchPlayerStats",
                columns: new[] { "TournamentId", "TeamId", "PlayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMemberships_PlayerId",
                table: "PlayerMemberships",
                column: "PlayerId",
                unique: true,
                filter: "[LeftOn] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMemberships_TeamId",
                table: "PlayerMemberships",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultAudits_ActorId",
                table: "ResultAudits",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultAudits_MatchId",
                table: "ResultAudits",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Code",
                table: "Teams",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_ManagerId",
                table: "Teams",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketOffers_EventId_Name",
                table: "TicketOffers",
                columns: new[] { "EventId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_BookingId",
                table: "Tickets",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CheckedInById",
                table: "Tickets",
                column: "CheckedInById");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Code",
                table: "Tickets",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentRosters_PlayerId",
                table: "TournamentRosters",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentRosters_TournamentId_PlayerId",
                table: "TournamentRosters",
                columns: new[] { "TournamentId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentRosters_TournamentId_TeamId_ShirtNumber",
                table: "TournamentRosters",
                columns: new[] { "TournamentId", "TeamId", "ShirtNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_Name_Season",
                table: "Tournaments",
                columns: new[] { "Name", "Season" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_OrganizerId",
                table: "Tournaments",
                column: "OrganizerId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentTeams_TeamId",
                table: "TournamentTeams",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchPlayerStats");

            migrationBuilder.DropTable(
                name: "PlayerMemberships");

            migrationBuilder.DropTable(
                name: "ResultAudits");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropTable(
                name: "TournamentRosters");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "TicketOffers");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "TournamentTeams");

            migrationBuilder.DropTable(
                name: "Venues");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "Tournaments");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
