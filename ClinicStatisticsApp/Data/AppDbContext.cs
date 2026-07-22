using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.CallCenter.Models;
using ClinicStatisticsApp.Chat;
using Microsoft.EntityFrameworkCore;

namespace ClinicStatisticsApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<ProfiEntry> ProfiEntries => Set<ProfiEntry>();
        public DbSet<ProDoctorQrEntry> ProDoctorQrEntries => Set<ProDoctorQrEntry>();
        public DbSet<ProfoCategory> ProfoCategories => Set<ProfoCategory>();
        public DbSet<SummaryProfoManualEntry> SummaryProfoManualEntries => Set<SummaryProfoManualEntry>();
        public DbSet<NaradEntry> NaradEntries => Set<NaradEntry>();
        public DbSet<HoursEntry> HoursEntries => Set<HoursEntry>();
        public DbSet<ReviewEntry> ReviewEntries => Set<ReviewEntry>();
        public DbSet<Branch> Branches => Set<Branch>();
        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<User> Users => Set<User>();
        public DbSet<BranchReport> BranchReports => Set<BranchReport>();
        public DbSet<PerkEntry> PerkEntries => Set<PerkEntry>();
        public DbSet<CallCenterEmployee> CallCenterEmployees => Set<CallCenterEmployee>();
        public DbSet<CallCenterGroup> CallCenterGroups => Set<CallCenterGroup>();
        public DbSet<CallCenterTopic> CallCenterTopics => Set<CallCenterTopic>();
        public DbSet<CallCenterEmployeeGroup> CallCenterEmployeeGroups => Set<CallCenterEmployeeGroup>();
        public DbSet<CallCenterCallRecord> CallCenterCallRecords => Set<CallCenterCallRecord>();
        public DbSet<CallCenterSyncLog> CallCenterSyncLogs => Set<CallCenterSyncLog>();
        public DbSet<CallCenterSetting> CallCenterSettings => Set<CallCenterSetting>();
        public DbSet<CallCenterStatusRule> CallCenterStatusRules => Set<CallCenterStatusRule>();
        public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
        public DbSet<ChatParticipant> ChatParticipants => Set<ChatParticipant>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
        public DbSet<ChatAttachment> ChatAttachments => Set<ChatAttachment>();
        public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
        public DbSet<CalendarEventParticipant> CalendarEventParticipants => Set<CalendarEventParticipant>();
        public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
        public DbSet<WorkTaskChecklistItem> WorkTaskChecklistItems => Set<WorkTaskChecklistItem>();
        public DbSet<WorkTaskComment> WorkTaskComments => Set<WorkTaskComment>();
        public DbSet<WorkTaskStatusHistory> WorkTaskStatusHistory => Set<WorkTaskStatusHistory>();
        public DbSet<WorkTaskNotification> WorkTaskNotifications => Set<WorkTaskNotification>();
        public DbSet<CrmPerson> CrmPersons => Set<CrmPerson>();
        public DbSet<ClinicDataSource> ClinicDataSources => Set<ClinicDataSource>();
        public DbSet<ExternalPatientCard> ExternalPatientCards => Set<ExternalPatientCard>();
        public DbSet<PatientMatchCandidate> PatientMatchCandidates => Set<PatientMatchCandidate>();
        public DbSet<PatientIdentityAuditEntry> PatientIdentityAuditEntries => Set<PatientIdentityAuditEntry>();
        public DbSet<CrmActivityLink> CrmActivityLinks => Set<CrmActivityLink>();
        public DbSet<CrmPatientNote> CrmPatientNotes => Set<CrmPatientNote>();
        public DbSet<CrmAnalyticsPayment> CrmAnalyticsPayments => Set<CrmAnalyticsPayment>();
        public DbSet<CrmAnalyticsAppointment> CrmAnalyticsAppointments => Set<CrmAnalyticsAppointment>();
        public DbSet<FirebirdImportRun> FirebirdImportRuns => Set<FirebirdImportRun>();
        public DbSet<PatientDuplicateReviewDecision> PatientDuplicateReviewDecisions => Set<PatientDuplicateReviewDecision>();
        public DbSet<PatientDossierSnapshot> PatientDossierSnapshots => Set<PatientDossierSnapshot>();

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("Roles");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
                entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            });

            modelBuilder.Entity<ProDoctorQrEntry>(entity =>
            {
                entity.ToTable("ProDoctorQrEntries");
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => new { x.Year, x.Month, x.BranchId }).IsUnique();

                entity.HasOne(x => x.Branch)
                    .WithMany(x => x.ProDoctorQrEntries)
                    .HasForeignKey(x => x.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SummaryProfoManualEntry>(entity =>
            {
                entity.ToTable("SummaryProfoManualEntries");
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => new { x.Year, x.Month, x.BranchId, x.EmployeeId }).IsUnique();

                entity.Property(x => x.Rate).HasColumnType("decimal(3,1)");

                entity.HasOne(x => x.Branch)
                    .WithMany(x => x.SummaryProfoManualEntries)
                    .HasForeignKey(x => x.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Employee)
                    .WithMany(x => x.SummaryProfoManualEntries)
                    .HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.ProfoCategory)
                    .WithMany(x => x.SummaryProfoManualEntries)
                    .HasForeignKey(x => x.ProfoCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ProfoCategory>(entity =>
            {
                entity.ToTable("ProfoCategories");
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => x.Name).IsUnique();

                entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
                entity.Property(x => x.SalaryRub).HasColumnType("decimal(10,2)");
                entity.Property(x => x.BasePaymentPerPatient).HasColumnType("decimal(10,2)");
                entity.Property(x => x.ExtraPaymentPerPatient).HasColumnType("decimal(10,2)");
            });

            modelBuilder.Entity<NaradEntry>(entity =>
            {
                entity.ToTable("NaradEntries");
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => new { x.BranchReportId, x.EmployeeId }).IsUnique();

                entity.Property(x => x.PaymentPerReview).HasColumnType("decimal(10,2)");

                entity.HasOne(x => x.BranchReport)
                    .WithMany(x => x.NaradEntries)
                    .HasForeignKey(x => x.BranchReportId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Employee)
                    .WithMany(x => x.NaradEntries)
                    .HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ReviewEntry>(entity =>
            {
                entity.ToTable("ReviewEntries");
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => new { x.BranchReportId, x.EmployeeId }).IsUnique();

                entity.HasOne(x => x.BranchReport)
                    .WithMany(x => x.ReviewEntries)
                    .HasForeignKey(x => x.BranchReportId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Employee)
                    .WithMany(x => x.ReviewEntries)
                    .HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<HoursEntry>(entity =>
            {
                entity.ToTable("HoursEntries");
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => new { x.BranchReportId, x.EmployeeId }).IsUnique();

                entity.Property(x => x.WorkedHours).HasColumnType("decimal(10,2)");

                entity.HasOne(x => x.BranchReport)
                    .WithMany(x => x.HoursEntries)
                    .HasForeignKey(x => x.BranchReportId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Employee)
                    .WithMany(x => x.HoursEntries)
                    .HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Branch>(entity =>
            {
                entity.ToTable("Branches");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
                entity.Property(x => x.ShortName).HasMaxLength(50).IsRequired();
            });

            modelBuilder.Entity<Employee>(entity =>
            {
                entity.ToTable("Employees");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
                entity.Property(x => x.Comment).HasMaxLength(500);

                entity.Property(x => x.DefaultReviewPaymentRate).HasColumnType("decimal(10,2)");
                entity.Property(x => x.DefaultProfoRate).HasColumnType("decimal(3,1)");

                entity.HasOne(x => x.DefaultProfoCategory)
                    .WithMany()
                    .HasForeignKey(x => x.DefaultProfoCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Login).HasMaxLength(100).IsRequired();
                entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
                entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();

                entity.HasOne(x => x.Role)
                    .WithMany()
                    .HasForeignKey(x => x.RoleId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Branch)
                    .WithMany()
                    .HasForeignKey(x => x.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<BranchReport>(entity =>
            {
                entity.ToTable("BranchReports");
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => new { x.BranchId, x.Year, x.Month }).IsUnique();

                entity.Property(x => x.Status).HasMaxLength(20).IsRequired();

                entity.HasOne(x => x.Branch)
                    .WithMany(x => x.BranchReports)
                    .HasForeignKey(x => x.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.CreatedByUser)
                    .WithMany(x => x.CreatedBranchReports)
                    .HasForeignKey(x => x.CreatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PerkEntry>(entity =>
            {
                entity.ToTable("PerkEntries");
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => new { x.BranchReportId, x.EmployeeId }).IsUnique();

                entity.HasOne(x => x.BranchReport)
                    .WithMany(x => x.PerkEntries)
                    .HasForeignKey(x => x.BranchReportId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Employee)
                    .WithMany(x => x.PerkEntries)
                    .HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ProfiEntry>(entity =>
            {
                entity.ToTable("ProfiEntries");
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => new { x.BranchReportId, x.EmployeeId }).IsUnique();

                entity.HasOne(x => x.BranchReport)
                    .WithMany(x => x.ProfiEntries)
                    .HasForeignKey(x => x.BranchReportId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Employee)
                    .WithMany(x => x.ProfiEntries)
                    .HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CallCenterEmployee>(entity =>
            {
                entity.ToTable("CallCenterEmployees");
                entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
                entity.Property(x => x.Extension).HasMaxLength(50);
                entity.Property(x => x.MangoUserId).HasMaxLength(100);
                entity.Property(x => x.MangoUserKey).HasMaxLength(100);
                entity.HasIndex(x => x.MangoUserId);
            });

            modelBuilder.Entity<CallCenterGroup>(entity =>
            {
                entity.ToTable("CallCenterGroups");
                entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
                entity.Property(x => x.MangoGroupId).HasMaxLength(100);
                entity.HasIndex(x => x.MangoGroupId);
            });

            modelBuilder.Entity<CallCenterTopic>(entity =>
            {
                entity.ToTable("CallCenterTopics");
                entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
                entity.Property(x => x.MangoTopicId).HasMaxLength(100);
                entity.HasIndex(x => x.MangoTopicId);
            });

            modelBuilder.Entity<CallCenterEmployeeGroup>(entity =>
            {
                entity.ToTable("CallCenterEmployeeGroups");
                entity.HasOne(x => x.Employee).WithMany(x => x.EmployeeGroups).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Group).WithMany(x => x.EmployeeGroups).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CallCenterCallRecord>(entity =>
            {
                entity.ToTable("CallCenterCalls");
                entity.Property(x => x.MangoCallId).HasMaxLength(100).IsRequired();
                entity.Property(x => x.ExternalPhoneNumber).HasMaxLength(50);
                entity.Property(x => x.Direction).HasMaxLength(50).IsRequired();
                entity.Property(x => x.StatusCode).HasMaxLength(100);
                entity.Property(x => x.StatusText).HasMaxLength(200);
                entity.HasIndex(x => x.MangoCallId).IsUnique();
                entity.HasIndex(x => x.CallDateTime);
                entity.HasOne(x => x.Employee).WithMany(x => x.CallRecords).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(x => x.Group).WithMany(x => x.CallRecords).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(x => x.Topic).WithMany(x => x.CallRecords).HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<CallCenterSyncLog>(entity => entity.ToTable("CallCenterSyncLogs"));
            modelBuilder.Entity<CallCenterSetting>(entity =>
            {
                entity.ToTable("CallCenterSettings");
                entity.Property(x => x.Key).HasMaxLength(100).IsRequired();
                entity.HasIndex(x => x.Key).IsUnique();
            });
            modelBuilder.Entity<CallCenterStatusRule>(entity =>
            {
                entity.ToTable("CallCenterStatusRules");
                entity.Property(x => x.StatusCode).HasMaxLength(100).IsRequired();
                entity.Property(x => x.StatusText).HasMaxLength(200);
                entity.HasIndex(x => x.StatusCode);
            });
            modelBuilder.Entity<ChatConversation>(entity => { entity.ToTable("ChatConversations"); entity.Property(x => x.Title).HasMaxLength(200); });
            modelBuilder.Entity<ChatParticipant>(entity =>
            {
                entity.ToTable("ChatParticipants"); entity.HasIndex(x => new { x.ConversationId, x.UserId }).IsUnique();
                entity.HasOne(x => x.Conversation).WithMany(x => x.Participants).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.ToTable("ChatMessages"); entity.Property(x => x.Text).HasMaxLength(4000); entity.HasIndex(x => new { x.ConversationId, x.SentAt });
                entity.HasOne(x => x.Conversation).WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<ChatAttachment>(entity =>
            {
                entity.ToTable("ChatAttachments"); entity.Property(x => x.FileName).HasMaxLength(260); entity.Property(x => x.StoredName).HasMaxLength(80); entity.Property(x => x.ContentType).HasMaxLength(100);
                entity.HasOne(x => x.Message).WithMany(x => x.Attachments).HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<CalendarEvent>(entity =>
            {
                entity.ToTable("CalendarEvents");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
                entity.Property(x => x.Description).HasMaxLength(2000);
                entity.Property(x => x.Color).HasMaxLength(20).IsRequired();
                entity.Property(x => x.RecurrenceType).HasMaxLength(20).IsRequired();
                entity.HasIndex(x => new { x.StartsAt, x.EndsAt });
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<CalendarEventParticipant>(entity =>
            {
                entity.ToTable("CalendarEventParticipants");
                entity.HasKey(x => new { x.CalendarEventId, x.UserId });
                entity.HasOne(x => x.CalendarEvent).WithMany(x => x.Participants).HasForeignKey(x => x.CalendarEventId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<WorkTask>(entity =>
            {
                entity.ToTable("WorkTasks"); entity.HasKey(x => x.Id);
                entity.Property(x => x.Title).HasMaxLength(250).IsRequired();
                entity.Property(x => x.Description).HasColumnType("nvarchar(max)");
                entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
                entity.Property(x => x.Priority).HasMaxLength(20).IsRequired();
                entity.Property(x => x.CompletionResult).HasMaxLength(2000);
                entity.HasIndex(x => new { x.ResponsibleUserId, x.Status, x.DueAt });
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.ResponsibleUserId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<WorkTaskChecklistItem>(entity =>
            {
                entity.ToTable("WorkTaskChecklistItems"); entity.HasKey(x => x.Id);
                entity.Property(x => x.Text).HasMaxLength(500).IsRequired();
                entity.HasOne(x => x.WorkTask).WithMany(x => x.ChecklistItems).HasForeignKey(x => x.WorkTaskId).OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<WorkTaskComment>(entity =>
            {
                entity.ToTable("WorkTaskComments"); entity.HasKey(x => x.Id); entity.Property(x => x.Text).HasMaxLength(2000).IsRequired();
                entity.HasIndex(x => new { x.WorkTaskId, x.CreatedAt });
                entity.HasOne(x => x.WorkTask).WithMany(x => x.Comments).HasForeignKey(x => x.WorkTaskId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<WorkTaskStatusHistory>(entity =>
            {
                entity.ToTable("WorkTaskStatusHistory"); entity.HasKey(x => x.Id); entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
                entity.HasIndex(x => new { x.WorkTaskId, x.StartedAt });
                entity.HasOne(x => x.WorkTask).WithMany(x => x.StatusHistory).HasForeignKey(x => x.WorkTaskId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<WorkTaskNotification>(entity =>
            {
                entity.ToTable("WorkTaskNotifications"); entity.HasKey(x => x.Id);
                entity.Property(x => x.Type).HasMaxLength(40).IsRequired(); entity.Property(x => x.Message).HasMaxLength(500).IsRequired();
                entity.HasIndex(x => new { x.UserId, x.ReadAt, x.CreatedAt });
                entity.HasOne(x => x.WorkTask).WithMany().HasForeignKey(x => x.WorkTaskId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<CrmPerson>(entity =>
            {
                entity.ToTable("CrmPersons"); entity.HasKey(x => x.Id);
                entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
                entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
                entity.Property(x => x.MiddleName).HasMaxLength(100);
                entity.Property(x => x.PrimaryCardNumber).HasMaxLength(100);
                entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
                entity.HasIndex(x => new { x.LastName, x.FirstName, x.DateOfBirth });
                entity.HasIndex(x => x.PrimaryCardNumber);
            });
            modelBuilder.Entity<ExternalPatientCard>(entity =>
            {
                entity.ToTable("ExternalPatientCards"); entity.HasKey(x => x.Id);
                entity.Property(x => x.SourceCardNumber).HasMaxLength(100);
                entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
                entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
                entity.Property(x => x.MiddleName).HasMaxLength(100);
                entity.Property(x => x.MobilePhone).HasMaxLength(50);
                entity.Property(x => x.NormalizedMobilePhone).HasMaxLength(32);
                entity.Property(x => x.Email).HasMaxLength(320);
                entity.Property(x => x.NormalizedEmail).HasMaxLength(320);
                entity.Property(x => x.LeadingDoctorName).HasMaxLength(200);
                entity.Property(x => x.SourceFingerprint).HasMaxLength(128);
                entity.HasIndex(x => new { x.BranchId, x.SourcePatientId }).IsUnique();
                entity.HasIndex(x => x.NormalizedMobilePhone);
                entity.HasIndex(x => x.NormalizedEmail);
                entity.HasIndex(x => x.CrmPersonId);
                entity.HasIndex(x => x.ClinicDataSourceId);
                entity.HasOne(x => x.Branch).WithMany(x => x.ExternalPatientCards).HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.ClinicDataSource).WithMany(x => x.ExternalPatientCards).HasForeignKey(x => x.ClinicDataSourceId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.CrmPerson).WithMany(x => x.ExternalPatientCards).HasForeignKey(x => x.CrmPersonId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<ClinicDataSource>(entity =>
            {
                entity.ToTable("ClinicDataSources"); entity.HasKey(x => x.Id);
                entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
                entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
                entity.HasIndex(x => x.Code).IsUnique();
                entity.HasIndex(x => new { x.BranchId, x.IsActive });
                entity.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<PatientMatchCandidate>(entity =>
            {
                entity.ToTable("PatientMatchCandidates"); entity.HasKey(x => x.Id);
                entity.Property(x => x.ConfidenceScore).HasColumnType("decimal(5,2)");
                entity.Property(x => x.EvidenceJson).HasColumnType("nvarchar(max)").IsRequired();
                entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
                entity.Property(x => x.DecisionComment).HasMaxLength(1000);
                entity.HasIndex(x => new { x.ExternalPatientCardId, x.ProposedCrmPersonId }).IsUnique();
                entity.HasIndex(x => new { x.Status, x.CreatedAt });
                entity.HasOne(x => x.ExternalPatientCard).WithMany(x => x.MatchCandidates).HasForeignKey(x => x.ExternalPatientCardId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.ProposedCrmPerson).WithMany().HasForeignKey(x => x.ProposedCrmPersonId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.DecidedByUser).WithMany().HasForeignKey(x => x.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<PatientIdentityAuditEntry>(entity =>
            {
                entity.ToTable("PatientIdentityAuditEntries"); entity.HasKey(x => x.Id);
                entity.Property(x => x.Action).HasMaxLength(40).IsRequired();
                entity.Property(x => x.Comment).HasMaxLength(1000);
                entity.HasIndex(x => new { x.ExternalPatientCardId, x.PerformedAt });
                entity.HasOne(x => x.ExternalPatientCard).WithMany().HasForeignKey(x => x.ExternalPatientCardId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.PerformedByUser).WithMany().HasForeignKey(x => x.PerformedByUserId).OnDelete(DeleteBehavior.Restrict);
              });
            modelBuilder.Entity<CrmActivityLink>(entity =>
            {
                entity.ToTable("CrmActivityLinks"); entity.HasKey(x => x.Id);
                entity.Property(x => x.ActivityType).HasMaxLength(30).IsRequired();
                entity.Property(x => x.ExternalId).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
                entity.Property(x => x.ContactValue).HasMaxLength(320);
                entity.HasIndex(x => new { x.CrmPersonId, x.ActivityType, x.ExternalId }).IsUnique();
                entity.HasIndex(x => new { x.CrmPersonId, x.OccurredAt });
                entity.HasOne(x => x.CrmPerson).WithMany().HasForeignKey(x => x.CrmPersonId).OnDelete(DeleteBehavior.Cascade);
              });
            modelBuilder.Entity<FirebirdImportRun>(entity =>
            {
                entity.ToTable("FirebirdImportRuns"); entity.HasKey(x => x.Id);
                entity.Property(x => x.ErrorText).HasMaxLength(2000);
                entity.HasIndex(x => new { x.ClinicDataSourceId, x.FinishedAt });
                entity.HasOne(x => x.ClinicDataSource).WithMany().HasForeignKey(x => x.ClinicDataSourceId).OnDelete(DeleteBehavior.Restrict);
              });
            modelBuilder.Entity<PatientDossierSnapshot>(entity =>
            {
                entity.ToTable("PatientDossierSnapshots"); entity.HasKey(x => x.Id);
                entity.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
                entity.Property(x => x.ErrorText).HasMaxLength(2000);
                entity.HasIndex(x => x.ExternalPatientCardId).IsUnique();
                entity.HasOne(x => x.ExternalPatientCard).WithMany().HasForeignKey(x => x.ExternalPatientCardId).OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<CrmPatientNote>(entity =>
            {
                entity.ToTable("CrmPatientNotes"); entity.HasKey(x => x.Id);
                entity.Property(x => x.Text).HasMaxLength(4000).IsRequired();
                entity.HasIndex(x => new { x.CrmPersonId, x.CreatedAt });
                entity.HasOne(x => x.CrmPerson).WithMany().HasForeignKey(x => x.CrmPersonId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<CrmAnalyticsPayment>(entity =>
            {
                entity.ToTable("CrmAnalyticsPayments"); entity.HasKey(x => x.Id);
                entity.Property(x => x.Amount).HasColumnType("decimal(18,2)"); entity.Property(x => x.Description).HasMaxLength(1000); entity.Property(x => x.CashDesk).HasMaxLength(100);
                entity.HasIndex(x => new { x.ClinicDataSourceId, x.SourcePaymentId }).IsUnique(); entity.HasIndex(x => x.PaymentDate);
            });
            modelBuilder.Entity<CrmAnalyticsAppointment>(entity =>
            {
                entity.ToTable("CrmAnalyticsAppointments"); entity.HasKey(x => x.Id);
                entity.Property(x => x.DoctorName).HasMaxLength(200); entity.Property(x => x.Room).HasMaxLength(100); entity.Property(x => x.Info).HasMaxLength(2000);
                entity.HasIndex(x => new { x.ClinicDataSourceId, x.SourceAppointmentId }).IsUnique(); entity.HasIndex(x => x.AppointmentDate);
            });
            modelBuilder.Entity<PatientDuplicateReviewDecision>(entity =>
            {
                entity.ToTable("PatientDuplicateReviewDecisions"); entity.HasKey(x => x.Id);
                entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
                entity.HasIndex(x => new { x.FirstExternalPatientCardId, x.SecondExternalPatientCardId }).IsUnique();
            });
        }
    }
}
