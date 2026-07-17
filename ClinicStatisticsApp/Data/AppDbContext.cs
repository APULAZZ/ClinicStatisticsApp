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
        }
    }
}
