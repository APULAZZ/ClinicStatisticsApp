IF OBJECT_ID(N'dbo.WorkTasks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkTasks
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkTasks PRIMARY KEY,
        Title NVARCHAR(250) NOT NULL, Description NVARCHAR(MAX) NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_WorkTasks_Status DEFAULT N'New',
        Priority NVARCHAR(20) NOT NULL CONSTRAINT DF_WorkTasks_Priority DEFAULT N'Normal',
        DueAt DATETIME2 NULL, CreatedByUserId INT NOT NULL, ResponsibleUserId INT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WorkTasks_CreatedAt DEFAULT SYSUTCDATETIME(), CompletedAt DATETIME2 NULL,
        CONSTRAINT FK_WorkTasks_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(Id),
        CONSTRAINT FK_WorkTasks_Responsible FOREIGN KEY (ResponsibleUserId) REFERENCES dbo.Users(Id)
    );
    CREATE INDEX IX_WorkTasks_Responsible_Status_DueAt ON dbo.WorkTasks(ResponsibleUserId, Status, DueAt);
END
IF OBJECT_ID(N'dbo.WorkTaskChecklistItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkTaskChecklistItems
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkTaskChecklistItems PRIMARY KEY,
        WorkTaskId INT NOT NULL, Text NVARCHAR(500) NOT NULL,
        IsCompleted BIT NOT NULL CONSTRAINT DF_WorkTaskChecklistItems_IsCompleted DEFAULT 0,
        SortOrder INT NOT NULL CONSTRAINT DF_WorkTaskChecklistItems_SortOrder DEFAULT 0,
        CONSTRAINT FK_WorkTaskChecklistItems_WorkTasks FOREIGN KEY (WorkTaskId) REFERENCES dbo.WorkTasks(Id) ON DELETE CASCADE
    );
END
