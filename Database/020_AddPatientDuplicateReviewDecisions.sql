SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.PatientDuplicateReviewDecisions', N'U') IS NULL
CREATE TABLE dbo.PatientDuplicateReviewDecisions
(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PatientDuplicateReviewDecisions PRIMARY KEY,
    FirstExternalPatientCardId INT NOT NULL,
    SecondExternalPatientCardId INT NOT NULL,
    Status NVARCHAR(20) NOT NULL,
    DecidedByUserId INT NOT NULL,
    DecidedAt DATETIME2 NOT NULL CONSTRAINT DF_PatientDuplicateReviewDecisions_DecidedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_PatientDuplicateReviewDecisions_Order CHECK (FirstExternalPatientCardId < SecondExternalPatientCardId),
    CONSTRAINT FK_PatientDuplicateReviewDecisions_First FOREIGN KEY (FirstExternalPatientCardId) REFERENCES dbo.ExternalPatientCards(Id),
    CONSTRAINT FK_PatientDuplicateReviewDecisions_Second FOREIGN KEY (SecondExternalPatientCardId) REFERENCES dbo.ExternalPatientCards(Id),
    CONSTRAINT FK_PatientDuplicateReviewDecisions_User FOREIGN KEY (DecidedByUserId) REFERENCES dbo.Users(Id)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PatientDuplicateReviewDecisions') AND name = N'UX_PatientDuplicateReviewDecisions_Pair')
    CREATE UNIQUE INDEX UX_PatientDuplicateReviewDecisions_Pair ON dbo.PatientDuplicateReviewDecisions(FirstExternalPatientCardId, SecondExternalPatientCardId);

COMMIT TRANSACTION;
