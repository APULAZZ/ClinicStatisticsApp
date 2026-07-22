/*
    Patient workspace: links a CRM person with operational activity.
    It changes only ClinicStatisticsDb and never modifies Firebird sources.
*/
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.WorkTasks', N'CrmPersonId') IS NULL
    ALTER TABLE dbo.WorkTasks ADD CrmPersonId INT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_WorkTasks_CrmPersons')
    ALTER TABLE dbo.WorkTasks ADD CONSTRAINT FK_WorkTasks_CrmPersons
        FOREIGN KEY (CrmPersonId) REFERENCES dbo.CrmPersons(Id);

IF OBJECT_ID(N'dbo.CrmActivityLinks', N'U') IS NULL
CREATE TABLE dbo.CrmActivityLinks
(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CrmActivityLinks PRIMARY KEY,
    CrmPersonId INT NOT NULL,
    ActivityType NVARCHAR(30) NOT NULL,
    ExternalId NVARCHAR(100) NOT NULL,
    Title NVARCHAR(300) NOT NULL,
    ContactValue NVARCHAR(320) NULL,
    OccurredAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_CrmActivityLinks_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_CrmActivityLinks_CrmPersons FOREIGN KEY (CrmPersonId) REFERENCES dbo.CrmPersons(Id) ON DELETE CASCADE
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WorkTasks') AND name = N'IX_WorkTasks_CrmPersonId')
    CREATE INDEX IX_WorkTasks_CrmPersonId ON dbo.WorkTasks(CrmPersonId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CrmActivityLinks') AND name = N'UX_CrmActivityLinks_Person_Type_External')
    CREATE UNIQUE INDEX UX_CrmActivityLinks_Person_Type_External ON dbo.CrmActivityLinks(CrmPersonId, ActivityType, ExternalId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CrmActivityLinks') AND name = N'IX_CrmActivityLinks_Person_Occurred')
    CREATE INDEX IX_CrmActivityLinks_Person_Occurred ON dbo.CrmActivityLinks(CrmPersonId, OccurredAt DESC);

COMMIT TRANSACTION;
