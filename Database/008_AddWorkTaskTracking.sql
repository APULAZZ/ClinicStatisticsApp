IF COL_LENGTH(N'dbo.WorkTasks', N'Id') IS NULL
    THROW 50001, N'Сначала выполните 007_CreateWorkTasks.sql.', 1;

IF OBJECT_ID(N'dbo.WorkTaskComments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkTaskComments
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkTaskComments PRIMARY KEY,
        WorkTaskId INT NOT NULL, AuthorUserId INT NOT NULL, Text NVARCHAR(2000) NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WorkTaskComments_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_WorkTaskComments_WorkTask FOREIGN KEY (WorkTaskId) REFERENCES dbo.WorkTasks(Id) ON DELETE CASCADE,
        CONSTRAINT FK_WorkTaskComments_Author FOREIGN KEY (AuthorUserId) REFERENCES dbo.Users(Id)
    );
    CREATE INDEX IX_WorkTaskComments_WorkTaskId_CreatedAt ON dbo.WorkTaskComments(WorkTaskId, CreatedAt);
END

IF OBJECT_ID(N'dbo.WorkTaskStatusHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkTaskStatusHistory
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkTaskStatusHistory PRIMARY KEY,
        WorkTaskId INT NOT NULL, Status NVARCHAR(20) NOT NULL,
        StartedAt DATETIME2 NOT NULL, EndedAt DATETIME2 NULL, ChangedByUserId INT NOT NULL,
        CONSTRAINT FK_WorkTaskStatusHistory_WorkTask FOREIGN KEY (WorkTaskId) REFERENCES dbo.WorkTasks(Id) ON DELETE CASCADE,
        CONSTRAINT FK_WorkTaskStatusHistory_User FOREIGN KEY (ChangedByUserId) REFERENCES dbo.Users(Id)
    );
    CREATE INDEX IX_WorkTaskStatusHistory_WorkTaskId_StartedAt ON dbo.WorkTaskStatusHistory(WorkTaskId, StartedAt);
    INSERT INTO dbo.WorkTaskStatusHistory (WorkTaskId, Status, StartedAt, EndedAt, ChangedByUserId)
    SELECT t.Id, t.Status, t.CreatedAt, CASE WHEN t.Status = N'Done' THEN t.CompletedAt ELSE NULL END, t.CreatedByUserId
    FROM dbo.WorkTasks t;
END
