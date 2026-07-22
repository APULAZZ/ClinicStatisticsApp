SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.WorkTaskNotifications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkTaskNotifications
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkTaskNotifications PRIMARY KEY,
        UserId INT NOT NULL, WorkTaskId INT NOT NULL,
        Type NVARCHAR(40) NOT NULL, Message NVARCHAR(500) NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WorkTaskNotifications_CreatedAt DEFAULT SYSUTCDATETIME(),
        ReadAt DATETIME2 NULL,
        CONSTRAINT FK_WorkTaskNotifications_User FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
        CONSTRAINT FK_WorkTaskNotifications_WorkTask FOREIGN KEY (WorkTaskId) REFERENCES dbo.WorkTasks(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_WorkTaskNotifications_User_ReadAt_CreatedAt ON dbo.WorkTaskNotifications(UserId, ReadAt, CreatedAt DESC);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WorkTaskNotifications') AND name = N'UX_WorkTaskNotifications_Overdue')
    CREATE UNIQUE INDEX UX_WorkTaskNotifications_Overdue ON dbo.WorkTaskNotifications(UserId, WorkTaskId, Type) WHERE Type = N'Overdue';
