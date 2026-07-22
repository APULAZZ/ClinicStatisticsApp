/*
    Separates physical Firebird databases from business branches.
    The Central test copy is a test data source mapped to BranchId = 1 (ЦК),
    not a new branch and not a production source.
*/
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.ClinicDataSources', N'U') IS NULL
CREATE TABLE dbo.ClinicDataSources
(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClinicDataSources PRIMARY KEY,
    BranchId INT NOT NULL,
    Code NVARCHAR(50) NOT NULL,
    Name NVARCHAR(150) NOT NULL,
    IsTest BIT NOT NULL CONSTRAINT DF_ClinicDataSources_IsTest DEFAULT 0,
    IsActive BIT NOT NULL CONSTRAINT DF_ClinicDataSources_IsActive DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ClinicDataSources_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_ClinicDataSources_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_ClinicDataSources_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches(Id)
);

IF COL_LENGTH(N'dbo.ExternalPatientCards', N'ClinicDataSourceId') IS NULL
    ALTER TABLE dbo.ExternalPatientCards ADD ClinicDataSourceId INT NULL;

GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ExternalPatientCards_ClinicDataSources')
    ALTER TABLE dbo.ExternalPatientCards ADD CONSTRAINT FK_ExternalPatientCards_ClinicDataSources
        FOREIGN KEY (ClinicDataSourceId) REFERENCES dbo.ClinicDataSources(Id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ClinicDataSources') AND name = N'UX_ClinicDataSources_Code')
    CREATE UNIQUE INDEX UX_ClinicDataSources_Code ON dbo.ClinicDataSources(Code);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ClinicDataSources') AND name = N'IX_ClinicDataSources_Branch_Active')
    CREATE INDEX IX_ClinicDataSources_Branch_Active ON dbo.ClinicDataSources(BranchId, IsActive);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ExternalPatientCards') AND name = N'IX_ExternalPatientCards_ClinicDataSourceId')
    CREATE INDEX IX_ExternalPatientCards_ClinicDataSourceId ON dbo.ExternalPatientCards(ClinicDataSourceId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ExternalPatientCards') AND name = N'UX_ExternalPatientCards_Source_Patient')
    CREATE UNIQUE INDEX UX_ExternalPatientCards_Source_Patient ON dbo.ExternalPatientCards(ClinicDataSourceId, SourcePatientId)
    WHERE ClinicDataSourceId IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM dbo.ClinicDataSources WHERE Code = N'CENTRAL_COPY_TEST')
    INSERT dbo.ClinicDataSources (BranchId, Code, Name, IsTest, IsActive)
    VALUES (1, N'CENTRAL_COPY_TEST', N'Копия ЦК (тест)', 1, 1);

COMMIT TRANSACTION;
