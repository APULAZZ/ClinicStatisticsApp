SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.FirebirdImportRuns', N'U') IS NULL
CREATE TABLE dbo.FirebirdImportRuns
(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FirebirdImportRuns PRIMARY KEY,
    ClinicDataSourceId INT NOT NULL,
    StartedAt DATETIME2 NOT NULL,
    FinishedAt DATETIME2 NOT NULL,
    IsSuccess BIT NOT NULL,
    SourceCount INT NULL,
    CreatedCount INT NULL,
    UpdatedCount INT NULL,
    ErrorText NVARCHAR(2000) NULL,
    CONSTRAINT FK_FirebirdImportRuns_ClinicDataSources FOREIGN KEY (ClinicDataSourceId) REFERENCES dbo.ClinicDataSources(Id)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FirebirdImportRuns') AND name = N'IX_FirebirdImportRuns_Source_Finished')
    CREATE INDEX IX_FirebirdImportRuns_Source_Finished ON dbo.FirebirdImportRuns(ClinicDataSourceId, FinishedAt DESC);

COMMIT TRANSACTION;
