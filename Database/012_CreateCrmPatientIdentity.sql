/*
    CRM identity layer.
    Creates SQL Server-only tables and does not read from or write to Firebird.
    Run after scripts 001-011 in ClinicStatisticsDb.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.CrmPersons', N'U') IS NULL
CREATE TABLE dbo.CrmPersons
(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CrmPersons PRIMARY KEY,
    LastName NVARCHAR(100) NOT NULL,
    FirstName NVARCHAR(100) NOT NULL,
    MiddleName NVARCHAR(100) NULL,
    DateOfBirth DATE NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_CrmPersons_Status DEFAULT N'Active',
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_CrmPersons_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_CrmPersons_UpdatedAt DEFAULT SYSUTCDATETIME()
);

IF OBJECT_ID(N'dbo.ExternalPatientCards', N'U') IS NULL
CREATE TABLE dbo.ExternalPatientCards
(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ExternalPatientCards PRIMARY KEY,
    BranchId INT NOT NULL,
    CrmPersonId INT NULL,
    SourcePatientId BIGINT NOT NULL,
    SourceCardNumber NVARCHAR(100) NULL,
    LastName NVARCHAR(100) NOT NULL,
    FirstName NVARCHAR(100) NOT NULL,
    MiddleName NVARCHAR(100) NULL,
    DateOfBirth DATE NULL,
    MobilePhone NVARCHAR(50) NULL,
    NormalizedMobilePhone NVARCHAR(32) NULL,
    Email NVARCHAR(320) NULL,
    NormalizedEmail NVARCHAR(320) NULL,
    SourceCreatedAt DATETIME2 NULL,
    LastVisitAt DATETIME2 NULL,
    NextAppointmentAt DATETIME2 NULL,
    LeadingDoctorName NVARCHAR(200) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_ExternalPatientCards_IsActive DEFAULT 1,
    LastSyncedAt DATETIME2 NOT NULL CONSTRAINT DF_ExternalPatientCards_LastSyncedAt DEFAULT SYSUTCDATETIME(),
    SourceUpdatedAt DATETIME2 NULL,
    SourceFingerprint NVARCHAR(128) NULL,
    CONSTRAINT FK_ExternalPatientCards_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches(Id),
    CONSTRAINT FK_ExternalPatientCards_CrmPersons FOREIGN KEY (CrmPersonId) REFERENCES dbo.CrmPersons(Id)
);

IF OBJECT_ID(N'dbo.PatientMatchCandidates', N'U') IS NULL
CREATE TABLE dbo.PatientMatchCandidates
(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PatientMatchCandidates PRIMARY KEY,
    ExternalPatientCardId INT NOT NULL,
    ProposedCrmPersonId INT NOT NULL,
    ConfidenceScore DECIMAL(5,2) NOT NULL,
    EvidenceJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_PatientMatchCandidates_EvidenceJson DEFAULT N'{}',
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_PatientMatchCandidates_Status DEFAULT N'Pending',
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_PatientMatchCandidates_CreatedAt DEFAULT SYSUTCDATETIME(),
    DecidedAt DATETIME2 NULL,
    DecidedByUserId INT NULL,
    DecisionComment NVARCHAR(1000) NULL,
    CONSTRAINT FK_PatientMatchCandidates_Card FOREIGN KEY (ExternalPatientCardId) REFERENCES dbo.ExternalPatientCards(Id) ON DELETE CASCADE,
    CONSTRAINT FK_PatientMatchCandidates_Person FOREIGN KEY (ProposedCrmPersonId) REFERENCES dbo.CrmPersons(Id),
    CONSTRAINT FK_PatientMatchCandidates_User FOREIGN KEY (DecidedByUserId) REFERENCES dbo.Users(Id)
);

IF OBJECT_ID(N'dbo.PatientIdentityAuditEntries', N'U') IS NULL
CREATE TABLE dbo.PatientIdentityAuditEntries
(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PatientIdentityAuditEntries PRIMARY KEY,
    ExternalPatientCardId INT NOT NULL,
    PreviousCrmPersonId INT NULL,
    CurrentCrmPersonId INT NULL,
    Action NVARCHAR(40) NOT NULL,
    Comment NVARCHAR(1000) NULL,
    PerformedByUserId INT NOT NULL,
    PerformedAt DATETIME2 NOT NULL CONSTRAINT DF_PatientIdentityAuditEntries_PerformedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_PatientIdentityAuditEntries_Card FOREIGN KEY (ExternalPatientCardId) REFERENCES dbo.ExternalPatientCards(Id),
    CONSTRAINT FK_PatientIdentityAuditEntries_User FOREIGN KEY (PerformedByUserId) REFERENCES dbo.Users(Id)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CrmPersons') AND name = N'IX_CrmPersons_NameBirthDate')
    CREATE INDEX IX_CrmPersons_NameBirthDate ON dbo.CrmPersons(LastName, FirstName, DateOfBirth);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ExternalPatientCards') AND name = N'UX_ExternalPatientCards_Branch_SourcePatient')
    CREATE UNIQUE INDEX UX_ExternalPatientCards_Branch_SourcePatient ON dbo.ExternalPatientCards(BranchId, SourcePatientId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ExternalPatientCards') AND name = N'IX_ExternalPatientCards_NormalizedMobilePhone')
    CREATE INDEX IX_ExternalPatientCards_NormalizedMobilePhone ON dbo.ExternalPatientCards(NormalizedMobilePhone);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ExternalPatientCards') AND name = N'IX_ExternalPatientCards_NormalizedEmail')
    CREATE INDEX IX_ExternalPatientCards_NormalizedEmail ON dbo.ExternalPatientCards(NormalizedEmail);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ExternalPatientCards') AND name = N'IX_ExternalPatientCards_CrmPersonId')
    CREATE INDEX IX_ExternalPatientCards_CrmPersonId ON dbo.ExternalPatientCards(CrmPersonId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PatientMatchCandidates') AND name = N'UX_PatientMatchCandidates_Card_Person')
    CREATE UNIQUE INDEX UX_PatientMatchCandidates_Card_Person ON dbo.PatientMatchCandidates(ExternalPatientCardId, ProposedCrmPersonId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PatientMatchCandidates') AND name = N'IX_PatientMatchCandidates_Status_CreatedAt')
    CREATE INDEX IX_PatientMatchCandidates_Status_CreatedAt ON dbo.PatientMatchCandidates(Status, CreatedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PatientIdentityAuditEntries') AND name = N'IX_PatientIdentityAuditEntries_Card_PerformedAt')
    CREATE INDEX IX_PatientIdentityAuditEntries_Card_PerformedAt ON dbo.PatientIdentityAuditEntries(ExternalPatientCardId, PerformedAt);

COMMIT TRANSACTION;
