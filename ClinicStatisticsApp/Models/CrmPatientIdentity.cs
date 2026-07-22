using System;
using System.Collections.Generic;

namespace ClinicStatisticsApp.Models;

/// <summary>
/// Central CRM identity. This is not a medical record and may be linked to
/// several independent patient cards from different clinic branches.
/// </summary>
public class CrmPerson
{
    public int Id { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? PrimaryCardNumber { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<ExternalPatientCard> ExternalPatientCards { get; set; } = new List<ExternalPatientCard>();
}

/// <summary>
/// A physical Firebird source. Test copies are separate sources and are never
/// represented as additional business branches.
/// </summary>
public class ClinicDataSource
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsTest { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Branch? Branch { get; set; }
    public ICollection<ExternalPatientCard> ExternalPatientCards { get; set; } = new List<ExternalPatientCard>();
}

/// <summary>
/// Read-only CRM projection of one patient card from a particular Firebird database.
/// SourcePatientId is local to a branch and must never be treated as a global ID.
/// </summary>
public class ExternalPatientCard
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int? ClinicDataSourceId { get; set; }
    public int? CrmPersonId { get; set; }
    public long SourcePatientId { get; set; }
    public string? SourceCardNumber { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? MobilePhone { get; set; }
    public string? NormalizedMobilePhone { get; set; }
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public DateTime? SourceCreatedAt { get; set; }
    public DateTime? LastVisitAt { get; set; }
    public DateTime? NextAppointmentAt { get; set; }
    public string? LeadingDoctorName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime LastSyncedAt { get; set; }
    public DateTime? SourceUpdatedAt { get; set; }
    public string? SourceFingerprint { get; set; }

    public Branch? Branch { get; set; }
    public ClinicDataSource? ClinicDataSource { get; set; }
    public CrmPerson? CrmPerson { get; set; }
    public ICollection<PatientMatchCandidate> MatchCandidates { get; set; } = new List<PatientMatchCandidate>();
}

/// <summary>
/// A proposed, never silent, relation between a branch card and a CRM person.
/// An administrator must approve or reject non-deterministic matches.
/// </summary>
public class PatientMatchCandidate
{
    public int Id { get; set; }
    public int ExternalPatientCardId { get; set; }
    public int ProposedCrmPersonId { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string EvidenceJson { get; set; } = "{}";
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public int? DecidedByUserId { get; set; }
    public string? DecisionComment { get; set; }

    public ExternalPatientCard? ExternalPatientCard { get; set; }
    public CrmPerson? ProposedCrmPerson { get; set; }
    public User? DecidedByUser { get; set; }
}

/// <summary>
/// Immutable audit trail for manual identity decisions, including merge reversal.
/// </summary>
public class PatientIdentityAuditEntry
{
    public int Id { get; set; }
    public int ExternalPatientCardId { get; set; }
    public int? PreviousCrmPersonId { get; set; }
    public int? CurrentCrmPersonId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public int PerformedByUserId { get; set; }
    public DateTime PerformedAt { get; set; }

    public ExternalPatientCard? ExternalPatientCard { get; set; }
    public User? PerformedByUser { get; set; }
}

/// <summary>
/// A CRM-owned link to an operational event. The original event remains in its
/// source module; this table only makes the connection visible in the patient card.
/// </summary>
public class CrmActivityLink
{
    public int Id { get; set; }
    public int CrmPersonId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ContactValue { get; set; }
    public DateTime? OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public CrmPerson? CrmPerson { get; set; }
}

/// <summary>
/// Internal CRM note. It belongs only to the central CRM database and never
/// changes any Firebird patient record.
/// </summary>
public class CrmPatientNote
{
    public int Id { get; set; }
    public int CrmPersonId { get; set; }
    public int AuthorUserId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public CrmPerson? CrmPerson { get; set; }
    public User? Author { get; set; }
}

public class CrmAnalyticsPayment
{
    public int Id { get; set; }
    public int ClinicDataSourceId { get; set; }
    public long SourcePaymentId { get; set; }
    public long SourcePatientId { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? CashDesk { get; set; }
    public DateTime SyncedAt { get; set; }
}

public class CrmAnalyticsAppointment
{
    public int Id { get; set; }
    public int ClinicDataSourceId { get; set; }
    public long SourceAppointmentId { get; set; }
    public long SourcePatientId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string? DoctorName { get; set; }
    public string? Room { get; set; }
    public bool IsNoShow { get; set; }
    public string? Info { get; set; }
    public DateTime SyncedAt { get; set; }
}

public class FirebirdImportRun
{
    public int Id { get; set; }
    public int ClinicDataSourceId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public bool IsSuccess { get; set; }
    public int? SourceCount { get; set; }
    public int? CreatedCount { get; set; }
    public int? UpdatedCount { get; set; }
    public string? ErrorText { get; set; }
    public ClinicDataSource? ClinicDataSource { get; set; }
}

public class PatientDuplicateReviewDecision
{
    public int Id { get; set; }
    public int FirstExternalPatientCardId { get; set; }
    public int SecondExternalPatientCardId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int DecidedByUserId { get; set; }
    public DateTime DecidedAt { get; set; }
}
