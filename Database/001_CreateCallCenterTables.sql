/*
    ClinicStatisticsDb — модуль коллцентра.
    Скрипт идемпотентный: создаёт только отсутствующие таблицы, индексы и связи.
    Перед запуском убедитесь, что выбрана база ClinicStatisticsDb.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.CallCenterEmployees', N'U') IS NULL
CREATE TABLE dbo.CallCenterEmployees
(
    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CallCenterEmployees PRIMARY KEY,
    FullName nvarchar(200) NOT NULL,
    Extension nvarchar(50) NULL,
    MangoUserId nvarchar(100) NULL,
    MangoUserKey nvarchar(100) NULL,
    IsActive bit NOT NULL CONSTRAINT DF_CallCenterEmployees_IsActive DEFAULT (1)
);

IF OBJECT_ID(N'dbo.CallCenterGroups', N'U') IS NULL
CREATE TABLE dbo.CallCenterGroups
(
    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CallCenterGroups PRIMARY KEY,
    Name nvarchar(200) NOT NULL,
    MangoGroupId nvarchar(100) NULL,
    IsActive bit NOT NULL CONSTRAINT DF_CallCenterGroups_IsActive DEFAULT (1)
);

IF OBJECT_ID(N'dbo.CallCenterTopics', N'U') IS NULL
CREATE TABLE dbo.CallCenterTopics
(
    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CallCenterTopics PRIMARY KEY,
    Name nvarchar(200) NOT NULL,
    MangoTopicId nvarchar(100) NULL,
    IsActive bit NOT NULL CONSTRAINT DF_CallCenterTopics_IsActive DEFAULT (1)
);

IF OBJECT_ID(N'dbo.CallCenterEmployeeGroups', N'U') IS NULL
CREATE TABLE dbo.CallCenterEmployeeGroups
(
    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CallCenterEmployeeGroups PRIMARY KEY,
    EmployeeId int NOT NULL,
    GroupId int NOT NULL,
    DateFrom datetime2 NULL,
    DateTo datetime2 NULL
);

IF OBJECT_ID(N'dbo.CallCenterCalls', N'U') IS NULL
CREATE TABLE dbo.CallCenterCalls
(
    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CallCenterCalls PRIMARY KEY,
    MangoCallId nvarchar(100) NOT NULL,
    CallDateTime datetime2 NOT NULL,
    EmployeeId int NULL,
    GroupId int NULL,
    TopicId int NULL,
    RecordingId nvarchar(max) NULL,
    ExternalPhoneNumber nvarchar(50) NULL,
    Direction nvarchar(50) NOT NULL,
    StatusCode nvarchar(100) NULL,
    StatusText nvarchar(200) NULL,
    DurationSeconds int NULL,
    TalkDurationSeconds int NULL,
    WaitDurationSeconds int NULL,
    IsIncoming bit NOT NULL,
    IsOutgoing bit NOT NULL,
    IsAnswered bit NOT NULL,
    IsMissedIncoming bit NOT NULL,
    IsOutgoingNoAnswer bit NOT NULL,
    RawJson nvarchar(max) NULL,
    ImportedAt datetime2 NOT NULL
);

IF OBJECT_ID(N'dbo.CallCenterSyncLogs', N'U') IS NULL
CREATE TABLE dbo.CallCenterSyncLogs
(
    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CallCenterSyncLogs PRIMARY KEY,
    SyncType nvarchar(max) NOT NULL,
    StartedAt datetime2 NOT NULL,
    FinishedAt datetime2 NULL,
    PeriodFrom datetime2 NULL,
    PeriodTo datetime2 NULL,
    ImportedCount int NOT NULL,
    UpdatedCount int NOT NULL,
    SkippedCount int NOT NULL,
    IsSuccess bit NOT NULL,
    ErrorText nvarchar(max) NULL
);

IF OBJECT_ID(N'dbo.CallCenterSettings', N'U') IS NULL
CREATE TABLE dbo.CallCenterSettings
(
    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CallCenterSettings PRIMARY KEY,
    [Key] nvarchar(100) NOT NULL,
    [Value] nvarchar(max) NOT NULL
);

IF OBJECT_ID(N'dbo.CallCenterStatusRules', N'U') IS NULL
CREATE TABLE dbo.CallCenterStatusRules
(
    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CallCenterStatusRules PRIMARY KEY,
    StatusCode nvarchar(100) NOT NULL,
    StatusText nvarchar(200) NULL,
    CountAsAnswered bit NOT NULL,
    CountAsMissedIncoming bit NOT NULL,
    CountAsOutgoingNoAnswer bit NOT NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CallCenterEmployees') AND name = N'IX_CallCenterEmployees_MangoUserId')
    CREATE INDEX IX_CallCenterEmployees_MangoUserId ON dbo.CallCenterEmployees(MangoUserId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CallCenterGroups') AND name = N'IX_CallCenterGroups_MangoGroupId')
    CREATE INDEX IX_CallCenterGroups_MangoGroupId ON dbo.CallCenterGroups(MangoGroupId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CallCenterTopics') AND name = N'IX_CallCenterTopics_MangoTopicId')
    CREATE INDEX IX_CallCenterTopics_MangoTopicId ON dbo.CallCenterTopics(MangoTopicId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CallCenterCalls') AND name = N'UX_CallCenterCalls_MangoCallId')
    CREATE UNIQUE INDEX UX_CallCenterCalls_MangoCallId ON dbo.CallCenterCalls(MangoCallId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CallCenterCalls') AND name = N'IX_CallCenterCalls_CallDateTime')
    CREATE INDEX IX_CallCenterCalls_CallDateTime ON dbo.CallCenterCalls(CallDateTime);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CallCenterSettings') AND name = N'UX_CallCenterSettings_Key')
    CREATE UNIQUE INDEX UX_CallCenterSettings_Key ON dbo.CallCenterSettings([Key]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CallCenterStatusRules') AND name = N'IX_CallCenterStatusRules_StatusCode')
    CREATE INDEX IX_CallCenterStatusRules_StatusCode ON dbo.CallCenterStatusRules(StatusCode);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CallCenterEmployeeGroups_Employee')
    ALTER TABLE dbo.CallCenterEmployeeGroups ADD CONSTRAINT FK_CallCenterEmployeeGroups_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.CallCenterEmployees(Id) ON DELETE CASCADE;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CallCenterEmployeeGroups_Group')
    ALTER TABLE dbo.CallCenterEmployeeGroups ADD CONSTRAINT FK_CallCenterEmployeeGroups_Group FOREIGN KEY (GroupId) REFERENCES dbo.CallCenterGroups(Id) ON DELETE CASCADE;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CallCenterCalls_Employee')
    ALTER TABLE dbo.CallCenterCalls ADD CONSTRAINT FK_CallCenterCalls_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.CallCenterEmployees(Id) ON DELETE SET NULL;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CallCenterCalls_Group')
    ALTER TABLE dbo.CallCenterCalls ADD CONSTRAINT FK_CallCenterCalls_Group FOREIGN KEY (GroupId) REFERENCES dbo.CallCenterGroups(Id) ON DELETE SET NULL;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CallCenterCalls_Topic')
    ALTER TABLE dbo.CallCenterCalls ADD CONSTRAINT FK_CallCenterCalls_Topic FOREIGN KEY (TopicId) REFERENCES dbo.CallCenterTopics(Id) ON DELETE SET NULL;

COMMIT TRANSACTION;
