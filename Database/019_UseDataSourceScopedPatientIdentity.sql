/* A local patient ID is unique only within one physical Firebird source.
   Test and production copies of the same branch may legitimately overlap. */
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ExternalPatientCards') AND name = N'UX_ExternalPatientCards_Branch_SourcePatient')
    DROP INDEX UX_ExternalPatientCards_Branch_SourcePatient ON dbo.ExternalPatientCards;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ExternalPatientCards') AND name = N'UX_ExternalPatientCards_Source_Patient')
    CREATE UNIQUE INDEX UX_ExternalPatientCards_Source_Patient ON dbo.ExternalPatientCards(ClinicDataSourceId, SourcePatientId)
    WHERE ClinicDataSourceId IS NOT NULL;

COMMIT TRANSACTION;
