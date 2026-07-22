IF OBJECT_ID(N'dbo.CalendarEventParticipants', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CalendarEventParticipants
    (
        CalendarEventId INT NOT NULL,
        UserId          INT NOT NULL,
        CONSTRAINT PK_CalendarEventParticipants PRIMARY KEY (CalendarEventId, UserId),
        CONSTRAINT FK_CalendarEventParticipants_CalendarEvents FOREIGN KEY (CalendarEventId) REFERENCES dbo.CalendarEvents(Id) ON DELETE CASCADE,
        CONSTRAINT FK_CalendarEventParticipants_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
    );
    CREATE INDEX IX_CalendarEventParticipants_UserId ON dbo.CalendarEventParticipants(UserId);
END
