IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE TABLE [Players] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(120) NOT NULL,
        [BirthDate] date NOT NULL,
        [Nationality] nvarchar(80) NOT NULL,
        [Position] nvarchar(20) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Players] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(120) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [NormalizedEmail] nvarchar(256) NOT NULL,
        [PasswordHash] nvarchar(512) NOT NULL,
        [Role] nvarchar(30) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Users_Role] CHECK ([Role] IN ('Admin','Organizer','TeamManager','Spectator','GateStaff'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE TABLE [Venues] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(160) NOT NULL,
        [City] nvarchar(80) NOT NULL,
        [Address] nvarchar(300) NOT NULL,
        [Capacity] int NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Venues] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Venue_Capacity] CHECK ([Capacity] > 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE TABLE [Teams] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(120) NOT NULL,
        [Code] nvarchar(10) NOT NULL,
        [City] nvarchar(80) NOT NULL,
        [ManagerId] int NULL,
        [IsActive] bit NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Teams] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Teams_Users_ManagerId] FOREIGN KEY ([ManagerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE TABLE [Tournaments] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(160) NOT NULL,
        [Season] nvarchar(30) NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [OrganizerId] int NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Tournaments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Tournament_Dates] CHECK ([EndDate] >= [StartDate]),
        CONSTRAINT [CK_Tournament_Status] CHECK ([Status] IN ('Draft','RegistrationOpen','InProgress','Completed')),
        CONSTRAINT [FK_Tournaments_Users_OrganizerId] FOREIGN KEY ([OrganizerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE TABLE [PlayerMemberships] (
        [Id] int NOT NULL IDENTITY,
        [PlayerId] int NOT NULL,
        [TeamId] int NOT NULL,
        [JoinedOn] date NOT NULL,
        [LeftOn] date NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_PlayerMemberships] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Membership_Dates] CHECK ([LeftOn] IS NULL OR [LeftOn] > [JoinedOn]),
        CONSTRAINT [FK_PlayerMemberships_Players_PlayerId] FOREIGN KEY ([PlayerId]) REFERENCES [Players] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PlayerMemberships_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE TABLE [TournamentTeams] (
        [Id] int NOT NULL IDENTITY,
        [TournamentId] int NOT NULL,
        [TeamId] int NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_TournamentTeams] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_TournamentTeams_TournamentId_TeamId] UNIQUE ([TournamentId], [TeamId]),
        CONSTRAINT [FK_TournamentTeams_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TournamentTeams_Tournaments_TournamentId] FOREIGN KEY ([TournamentId]) REFERENCES [Tournaments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE TABLE [Events] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(180) NOT NULL,
        [VenueId] int NOT NULL,
        [OrganizerId] int NOT NULL,
        [StartsAtUtc] datetime2 NOT NULL,
        [EndsAtUtc] datetime2 NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [EventType] nvarchar(20) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [TournamentId] int NULL,
        [HomeTeamId] int NULL,
        [AwayTeamId] int NULL,
        [RoundNumber] int NULL,
        [Leg] int NULL,
        [HomeScore] int NULL,
        [AwayScore] int NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Events] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Event_Dates] CHECK ([EndsAtUtc] > [StartsAtUtc]),
        CONSTRAINT [CK_Event_Specialization] CHECK (([EventType] = 'Match' AND [TournamentId] IS NOT NULL AND [HomeTeamId] IS NOT NULL AND [AwayTeamId] IS NOT NULL AND [RoundNumber] IS NOT NULL AND [Leg] IS NOT NULL AND [Description] IS NULL) OR ([EventType] = 'Community' AND [Description] IS NOT NULL AND [TournamentId] IS NULL AND [HomeTeamId] IS NULL AND [AwayTeamId] IS NULL AND [RoundNumber] IS NULL AND [Leg] IS NULL AND [HomeScore] IS NULL AND [AwayScore] IS NULL)),
        CONSTRAINT [CK_Event_Status] CHECK ([Status] IN ('Scheduled','Finished','Cancelled')),
        CONSTRAINT [CK_Match_Round] CHECK ([RoundNumber] IS NULL OR ([RoundNumber] > 0 AND [Leg] BETWEEN 1 AND 2)),
        CONSTRAINT [CK_Match_Scores] CHECK ([EventType] <> 'Match' OR (([Status] = 'Finished' AND [HomeScore] IS NOT NULL AND [AwayScore] IS NOT NULL AND [HomeScore] BETWEEN 0 AND 99 AND [AwayScore] BETWEEN 0 AND 99) OR ([Status] <> 'Finished' AND [HomeScore] IS NULL AND [AwayScore] IS NULL))),
        CONSTRAINT [CK_Match_Teams] CHECK ([HomeTeamId] IS NULL OR [HomeTeamId] <> [AwayTeamId]),
        CONSTRAINT [FK_Events_TournamentTeams_TournamentId_AwayTeamId] FOREIGN KEY ([TournamentId], [AwayTeamId]) REFERENCES [TournamentTeams] ([TournamentId], [TeamId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Events_TournamentTeams_TournamentId_HomeTeamId] FOREIGN KEY ([TournamentId], [HomeTeamId]) REFERENCES [TournamentTeams] ([TournamentId], [TeamId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Events_Users_OrganizerId] FOREIGN KEY ([OrganizerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Events_Venues_VenueId] FOREIGN KEY ([VenueId]) REFERENCES [Venues] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE TABLE [TournamentRosters] (
        [Id] int NOT NULL IDENTITY,
        [TournamentId] int NOT NULL,
        [TeamId] int NOT NULL,
        [PlayerId] int NOT NULL,
        [ShirtNumber] int NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_TournamentRosters] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_TournamentRosters_TournamentId_TeamId_PlayerId] UNIQUE ([TournamentId], [TeamId], [PlayerId]),
        CONSTRAINT [CK_Roster_Shirt] CHECK ([ShirtNumber] BETWEEN 1 AND 99),
        CONSTRAINT [FK_TournamentRosters_Players_PlayerId] FOREIGN KEY ([PlayerId]) REFERENCES [Players] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TournamentRosters_TournamentTeams_TournamentId_TeamId] FOREIGN KEY ([TournamentId], [TeamId]) REFERENCES [TournamentTeams] ([TournamentId], [TeamId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE TABLE [ResultAudits] (
        [Id] int NOT NULL IDENTITY,
        [MatchId] int NOT NULL,
        [ActorId] int NOT NULL,
        [PreviousHomeScore] int NULL,
        [PreviousAwayScore] int NULL,
        [HomeScore] int NOT NULL,
        [AwayScore] int NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ResultAudits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ResultAudits_Events_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [Events] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ResultAudits_Users_ActorId] FOREIGN KEY ([ActorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE TABLE [TicketOffers] (
        [Id] int NOT NULL IDENTITY,
        [EventId] int NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [Price] decimal(12,2) NOT NULL,
        [Capacity] int NOT NULL,
        [SoldQuantity] int NOT NULL,
        [SalesStartUtc] datetime2 NOT NULL,
        [SalesEndUtc] datetime2 NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_TicketOffers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Offer_Dates] CHECK ([SalesEndUtc] > [SalesStartUtc]),
        CONSTRAINT [CK_Offer_Inventory] CHECK ([Capacity] > 0 AND [SoldQuantity] >= 0 AND [SoldQuantity] <= [Capacity]),
        CONSTRAINT [CK_Offer_Price] CHECK ([Price] >= 0),
        CONSTRAINT [FK_TicketOffers_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE TABLE [MatchPlayerStats] (
        [Id] int NOT NULL IDENTITY,
        [MatchId] int NOT NULL,
        [TournamentId] int NOT NULL,
        [TeamId] int NOT NULL,
        [PlayerId] int NOT NULL,
        [Goals] int NOT NULL,
        [Assists] int NOT NULL,
        [YellowCards] int NOT NULL,
        [RedCards] int NOT NULL,
        [MinutesPlayed] int NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_MatchPlayerStats] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PlayerStats_Range] CHECK ([Goals] BETWEEN 0 AND 99 AND [Assists] BETWEEN 0 AND 99 AND [YellowCards] BETWEEN 0 AND 2 AND [RedCards] BETWEEN 0 AND 1 AND [MinutesPlayed] BETWEEN 0 AND 130),
        CONSTRAINT [FK_MatchPlayerStats_Events_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [Events] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MatchPlayerStats_TournamentRosters_TournamentId_TeamId_PlayerId] FOREIGN KEY ([TournamentId], [TeamId], [PlayerId]) REFERENCES [TournamentRosters] ([TournamentId], [TeamId], [PlayerId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE TABLE [Bookings] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [TicketOfferId] int NOT NULL,
        [IdempotencyKey] nvarchar(64) NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(12,2) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [CancelledUtc] datetime2 NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Bookings] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Booking_Quantity] CHECK ([Quantity] BETWEEN 1 AND 10 AND [UnitPrice] >= 0),
        CONSTRAINT [CK_Booking_Status] CHECK (([Status] = 'Confirmed' AND [CancelledUtc] IS NULL) OR ([Status] = 'Cancelled' AND [CancelledUtc] IS NOT NULL)),
        CONSTRAINT [FK_Bookings_TicketOffers_TicketOfferId] FOREIGN KEY ([TicketOfferId]) REFERENCES [TicketOffers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Bookings_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE TABLE [Tickets] (
        [Id] int NOT NULL IDENTITY,
        [BookingId] int NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [CheckedInUtc] datetime2 NULL,
        [CheckedInById] int NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Tickets] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Ticket_CheckIn] CHECK (([CheckedInUtc] IS NULL AND [CheckedInById] IS NULL) OR ([CheckedInUtc] IS NOT NULL AND [CheckedInById] IS NOT NULL)),
        CONSTRAINT [FK_Tickets_Bookings_BookingId] FOREIGN KEY ([BookingId]) REFERENCES [Bookings] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Tickets_Users_CheckedInById] FOREIGN KEY ([CheckedInById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Bookings_TicketOfferId] ON [Bookings] ([TicketOfferId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Bookings_UserId_IdempotencyKey] ON [Bookings] ([UserId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Events_OrganizerId] ON [Events] ([OrganizerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Events_TournamentId_AwayTeamId] ON [Events] ([TournamentId], [AwayTeamId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Events_TournamentId_HomeTeamId_AwayTeamId_Leg] ON [Events] ([TournamentId], [HomeTeamId], [AwayTeamId], [Leg]) WHERE [EventType] = ''Match''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Events_VenueId_StartsAtUtc] ON [Events] ([VenueId], [StartsAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MatchPlayerStats_MatchId_PlayerId] ON [MatchPlayerStats] ([MatchId], [PlayerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchPlayerStats_TournamentId_TeamId_PlayerId] ON [MatchPlayerStats] ([TournamentId], [TeamId], [PlayerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PlayerMemberships_PlayerId] ON [PlayerMemberships] ([PlayerId]) WHERE [LeftOn] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PlayerMemberships_TeamId] ON [PlayerMemberships] ([TeamId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ResultAudits_ActorId] ON [ResultAudits] ([ActorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ResultAudits_MatchId] ON [ResultAudits] ([MatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Teams_Code] ON [Teams] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Teams_ManagerId] ON [Teams] ([ManagerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TicketOffers_EventId_Name] ON [TicketOffers] ([EventId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_BookingId] ON [Tickets] ([BookingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_CheckedInById] ON [Tickets] ([CheckedInById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tickets_Code] ON [Tickets] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TournamentRosters_PlayerId] ON [TournamentRosters] ([PlayerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TournamentRosters_TournamentId_PlayerId] ON [TournamentRosters] ([TournamentId], [PlayerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TournamentRosters_TournamentId_TeamId_ShirtNumber] ON [TournamentRosters] ([TournamentId], [TeamId], [ShirtNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tournaments_Name_Season] ON [Tournaments] ([Name], [Season]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tournaments_OrganizerId] ON [Tournaments] ([OrganizerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TournamentTeams_TeamId] ON [TournamentTeams] ([TeamId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_NormalizedEmail] ON [Users] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115135602_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261115135602_InitialCreate', N'10.0.15');
END;

COMMIT;
GO

