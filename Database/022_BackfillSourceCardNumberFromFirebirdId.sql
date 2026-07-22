/* Backfills CRM display/search card number from the already imported Firebird ID.
   Does not access any Firebird source. */
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

UPDATE dbo.ExternalPatientCards
SET SourceCardNumber = CONVERT(NVARCHAR(100), SourcePatientId)
WHERE SourceCardNumber IS NULL OR LTRIM(RTRIM(SourceCardNumber)) = N'';

COMMIT TRANSACTION;
