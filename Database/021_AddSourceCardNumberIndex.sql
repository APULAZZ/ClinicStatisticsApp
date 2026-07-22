SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ExternalPatientCards') AND name = N'IX_ExternalPatientCards_SourceCardNumber')
    CREATE INDEX IX_ExternalPatientCards_SourceCardNumber ON dbo.ExternalPatientCards(SourceCardNumber)
    WHERE SourceCardNumber IS NOT NULL;

COMMIT TRANSACTION;
