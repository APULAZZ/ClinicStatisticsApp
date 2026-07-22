IF OBJECT_ID(N'dbo.CalendarEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CalendarEvents
    (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CalendarEvents PRIMARY KEY,
        Title           NVARCHAR(200) NOT NULL,
        Description     NVARCHAR(2000) NULL,
        StartsAt        DATETIME2 NOT NULL,
        EndsAt          DATETIME2 NOT NULL,
        IsAllDay        BIT NOT NULL CONSTRAINT DF_CalendarEvents_IsAllDay DEFAULT (0),
        Color           NVARCHAR(20) NOT NULL CONSTRAINT DF_CalendarEvents_Color DEFAULT (N'#2563EB'),
        CreatedByUserId INT NOT NULL,
        CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_CalendarEvents_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_CalendarEvents_Users_CreatedByUserId FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(Id),
        CONSTRAINT CK_CalendarEvents_TimeRange CHECK (EndsAt > StartsAt)
    );
    CREATE INDEX IX_CalendarEvents_StartsAt_EndsAt ON dbo.CalendarEvents(StartsAt, EndsAt);
END
