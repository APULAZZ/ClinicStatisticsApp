/* Creates CRM-side source registrations for every currently registered branch.
   Connection paths and credentials remain local in firebird.Local.json. */
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.ClinicDataSources WHERE Code = N'CENTRAL_PRODUCTION')
    INSERT dbo.ClinicDataSources (BranchId, Code, Name, IsTest, IsActive)
    VALUES (1, N'CENTRAL_PRODUCTION', N'ЦК — рабочая база', 0, 1);

INSERT dbo.ClinicDataSources (BranchId, Code, Name, IsTest, IsActive)
SELECT b.Id, CONCAT(N'BRANCH_', RIGHT(N'00' + CONVERT(NVARCHAR(10), b.Id), 2)), CONCAT(b.Name, N' — рабочая база'), 0, 1
FROM dbo.Branches b
WHERE b.Id <> 1
  AND NOT EXISTS (SELECT 1 FROM dbo.ClinicDataSources s WHERE s.BranchId = b.Id AND s.IsTest = 0);

COMMIT TRANSACTION;
