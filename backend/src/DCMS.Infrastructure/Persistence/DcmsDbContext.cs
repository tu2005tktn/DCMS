using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Authentication;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Treatment;

namespace DCMS.Infrastructure.Persistence;

public sealed class DcmsDbContext(DbContextOptions<DcmsDbContext> options)
: IdentityDbContext<ApplicationUser, IdentityRole<int>, int>(options)
{
    public DbSet<EmployeeProfile> Employees => Set<EmployeeProfile>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<AuthRefreshToken> AuthRefreshTokens => Set<AuthRefreshToken>();
    public DbSet<AccountAudit> AccountAudits => Set<AccountAudit>();
    public DbSet<PatientProfile> Patients => Set<PatientProfile>();
    public DbSet<PatientAudit> PatientAudits => Set<PatientAudit>();
    public DbSet<PatientAccountLink> PatientAccountLinks => Set<PatientAccountLink>();
    public DbSet<PatientLinkAudit> PatientLinkAudits => Set<PatientLinkAudit>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentChange> AppointmentChanges => Set<AppointmentChange>();
    public DbSet<AppointmentChangeReceipt> AppointmentChangeReceipts => Set<AppointmentChangeReceipt>();
    public DbSet<AppointmentReceipt> AppointmentReceipts => Set<AppointmentReceipt>();
    public DbSet<AppointmentOutboxEvent> AppointmentOutboxEvents => Set<AppointmentOutboxEvent>();
    public DbSet<AppointmentSupportRequest> AppointmentSupportRequests => Set<AppointmentSupportRequest>();
    public DbSet<AppointmentSupportHistory> AppointmentSupportHistories => Set<AppointmentSupportHistory>();
    public DbSet<SupportRequestReceipt> SupportRequestReceipts => Set<SupportRequestReceipt>();
    public DbSet<InAppNotification> InAppNotifications => Set<InAppNotification>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<AppointmentReminder> AppointmentReminders => Set<AppointmentReminder>();
    public DbSet<StaffHandlingItem> StaffHandlingItems => Set<StaffHandlingItem>();
    public DbSet<RecoveryToken> RecoveryTokens => Set<RecoveryToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<RecoveryAudit> RecoveryAudits => Set<RecoveryAudit>();
    public DbSet<RecoveryOutbox> RecoveryOutbox => Set<RecoveryOutbox>();
    public DbSet<TreatmentDossier> TreatmentDossiers => Set<TreatmentDossier>();
    public DbSet<TreatmentItem> TreatmentItems => Set<TreatmentItem>();
    public DbSet<TreatmentEvent> TreatmentEvents => Set<TreatmentEvent>();
    public DbSet<TreatmentReceipt> TreatmentReceipts => Set<TreatmentReceipt>();
    public DbSet<TreatmentRevisionDraft> TreatmentRevisionDrafts => Set<TreatmentRevisionDraft>();
    public DbSet<TreatmentTemplate> TreatmentTemplates => Set<TreatmentTemplate>();
    public DbSet<TreatmentTemplateVersion> TreatmentTemplateVersions => Set<TreatmentTemplateVersion>();
    public DbSet<TreatmentStep> TreatmentSteps => Set<TreatmentStep>();
    public DbSet<FollowUpInstruction> FollowUpInstructions => Set<FollowUpInstruction>();
    public DbSet<TreatmentProgressionReceipt> TreatmentProgressionReceipts => Set<TreatmentProgressionReceipt>();
    public DbSet<TreatmentProgressionEvent> TreatmentProgressionEvents => Set<TreatmentProgressionEvent>();
    public DbSet<ClinicalActivity> ClinicalActivities => Set<ClinicalActivity>();
    public DbSet<ClinicalReview> ClinicalReviews => Set<ClinicalReview>();
    public DbSet<ClinicalCareNote> ClinicalCareNotes => Set<ClinicalCareNote>();
    public DbSet<ClinicalCommandReceipt> ClinicalCommandReceipts => Set<ClinicalCommandReceipt>();
    public DbSet<CareTask> CareTasks => Set<CareTask>();
    public DbSet<CareContactAttempt> CareContactAttempts => Set<CareContactAttempt>();
    public DbSet<CareEscalation> CareEscalations => Set<CareEscalation>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        SchedulingModel.Configure(model);
        AppointmentModel.Configure(model);
        TreatmentModel.Configure(model);
        model.Entity<ApplicationUser>(b =>
        {
            b.ToTable("USER_ACCOUNT");
            b.Property(x => x.FullName).HasMaxLength(160);
            b.Property(x => x.AccountType).HasMaxLength(16);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
            b.Property(x => x.NormalizedPhone).HasComputedColumnSql(
                "NULLIF(regexp_replace(btrim(\"PhoneNumber\"), '[ ()-]', '', 'g'), '')", stored: true);
            b.HasIndex(x => x.NormalizedPhone).IsUnique();
        });
        model.Entity<IdentityRole<int>>().ToTable("ROLE");
        model.Entity<IdentityUserRole<int>>().ToTable("USER_ACCOUNT_ROLE");
        model.Entity<EmployeeProfile>(b =>
        {
            b.ToTable("EMPLOYEE_PROFILE");
            b.HasIndex(x => x.AccountId).IsUnique();
            b.HasIndex(x => x.EmployeeCode).IsUnique();
            b.Property(x => x.EmployeeCode).HasMaxLength(40);
            b.Property(x => x.FullName).HasMaxLength(160);
            b.HasOne<ApplicationUser>().WithOne().HasForeignKey<EmployeeProfile>(x => x.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AuthSession>(b =>
        {
            b.ToTable("AUTH_SESSION");
            b.Property(x => x.DeviceLabel).HasMaxLength(120);
            b.HasIndex(x => new { x.AccountId, x.RevokedAt });
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AuthRefreshToken>(b =>
        {
            b.ToTable("AUTH_REFRESH_TOKEN");
            b.Property(x => x.TokenHash).HasMaxLength(64);
            b.Property(x => x.Status).HasMaxLength(16);
            // Chỉ có một ACTIVE refresh per session; lookup nhanh theo hash để xác thực rotation.
            b.HasIndex(x => x.TokenHash).IsUnique();
            b.HasIndex(x => new { x.SessionId, x.Status });
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<AuthSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AccountAudit>(b =>
        {
            b.ToTable("ACCOUNT_AUDIT");
            b.Property(x => x.Action).HasMaxLength(40);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.BeforeJson).HasColumnType("jsonb");
            b.Property(x => x.AfterJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.TargetId, x.OccurredAt });
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.TargetId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PatientProfile>(b =>
        {
            b.ToTable("PATIENT_PROFILE", t => t.HasCheckConstraint("CK_PATIENT_PROFILE_Gender",
                "\"Gender\" IS NULL OR \"Gender\" IN ('MALE','FEMALE','OTHER')"));
            b.HasIndex(x => x.PatientCode).IsUnique();
            b.HasIndex(x => x.NormalizedName);
            b.HasIndex(x => x.Phone);
            b.Property(x => x.PatientCode).HasMaxLength(40);
            b.Property(x => x.FullName).HasMaxLength(160);
            b.Property(x => x.NormalizedName).HasMaxLength(160);
            b.Property(x => x.Gender).HasMaxLength(16);
            b.Property(x => x.Phone).HasMaxLength(24);
            b.Property(x => x.Address).HasMaxLength(255);
            b.Property(x => x.GuardianName).HasMaxLength(160);
            b.Property(x => x.GuardianRelationship).HasMaxLength(40);
            b.Property(x => x.GuardianPhone).HasMaxLength(24);
            b.Property(x => x.Note).HasMaxLength(1000);
            b.Property(x => x.ArchiveReason).HasMaxLength(500);
            b.Property(x => x.Version).IsConcurrencyToken();
        });
        // UC-38: automatic patient codes BN000001... [PROPOSED format].
        model.HasSequence<long>("patient_code_seq");
        model.Entity<PatientAudit>(b =>
        {
            b.ToTable("PATIENT_AUDIT");
            b.Property(x => x.Action).HasMaxLength(16);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.ChangesJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.PatientId, x.OccurredAt });
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PatientAccountLink>(b =>
        {
            b.ToTable("PATIENT_ACCOUNT_LINK", t => t.HasCheckConstraint("CK_PATIENT_ACCOUNT_LINK_Status",
                "\"Status\" IN ('PENDING','VERIFIED','REJECTED','REVOKED','WITHDRAWN') AND (\"Status\" <> 'VERIFIED' OR \"PatientId\" IS NOT NULL)"));
            b.Property(x => x.RequestCode).HasMaxLength(32);
            b.Property(x => x.Status).HasMaxLength(16);
            b.Property(x => x.DeclaredFullName).HasMaxLength(160);
            b.Property(x => x.DeclaredPatientCode).HasMaxLength(40);
            b.Property(x => x.RequestNote).HasMaxLength(1000);
            b.Property(x => x.VerificationNote).HasMaxLength(1000);
            b.Property(x => x.DecisionReason).HasMaxLength(500);
            b.Property(x => x.PublicNote).HasMaxLength(500);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.RequestCode).IsUnique();
            b.HasIndex(x => new { x.Status, x.RequestedAt });
            // One-to-one active self link and no duplicate request being processed (GBR-072).
            // Named indexes: EF would otherwise merge the two filtered indexes on AccountId into one.
            b.HasIndex(x => x.AccountId, "UX_PATIENT_ACCOUNT_LINK_Account_Pending").IsUnique().HasFilter("\"Status\" = 'PENDING'");
            b.HasIndex(x => x.AccountId, "UX_PATIENT_ACCOUNT_LINK_Account_Verified").IsUnique().HasFilter("\"Status\" = 'VERIFIED'");
            b.HasIndex(x => x.PatientId, "UX_PATIENT_ACCOUNT_LINK_Patient_Verified").IsUnique().HasFilter("\"Status\" = 'VERIFIED'");
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<EmployeeProfile>().WithMany().HasForeignKey(x => x.ReviewingEmployeeId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<EmployeeProfile>().WithMany().HasForeignKey(x => x.RevokingEmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PatientLinkAudit>(b =>
        {
            b.ToTable("PATIENT_LINK_AUDIT");
            b.Property(x => x.Action).HasMaxLength(40);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.BeforeJson).HasColumnType("jsonb");
            b.Property(x => x.AfterJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.LinkId, x.OccurredAt });
            b.HasOne(x => x.Link).WithMany().HasForeignKey(x => x.LinkId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<RecoveryToken>(b =>
        {
            b.ToTable("RECOVERY_TOKEN");
            b.HasKey(x => x.Id);
            b.Property(x => x.TokenHash).HasMaxLength(256).IsRequired();
            b.Property(x => x.ResetGrantHash).HasMaxLength(256).IsRequired();
            b.Property(x => x.ChannelType).HasMaxLength(16).IsRequired();
            b.Property(x => x.NormalizedDestination).HasMaxLength(256).IsRequired();
            b.HasIndex(x => new { x.AccountId, x.UsedAt });
            b.HasIndex(x => x.ExpiresAt);
            b.HasIndex(x => new { x.AccountId, x.CreatedAt });
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<EmailVerificationToken>(b =>
        {
            b.ToTable("EMAIL_VERIFICATION_TOKEN");
            b.HasKey(x => x.Id);
            b.Property(x => x.NormalizedEmail).HasMaxLength(256).IsRequired();
            b.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
            b.HasIndex(x => new { x.AccountId, x.CreatedAt });
            b.HasIndex(x => x.ExpiresAt);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<RecoveryAudit>(b =>
        {
            b.ToTable("RECOVERY_AUDIT");
            b.HasKey(x => x.Id);
            b.Property(x => x.Action).HasMaxLength(40).IsRequired();
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.ChannelType).HasMaxLength(16);
            b.Property(x => x.Result).HasMaxLength(20);
            b.HasIndex(x => new { x.TargetId, x.OccurredAt });
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.TargetId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<RecoveryOutbox>(b =>
        {
            b.ToTable("RECOVERY_OUTBOX");
            b.HasKey(x => x.Id);
            b.Property(x => x.ChannelType).HasMaxLength(16).IsRequired();
            b.Property(x => x.Destination).HasMaxLength(256).IsRequired();
            b.Property(x => x.Provider).HasMaxLength(32).IsRequired();
            b.Property(x => x.Status).HasMaxLength(16).IsRequired();
            b.Property(x => x.ProviderMessageId).HasMaxLength(64);
            b.HasIndex(x => new { x.AccountId, x.OccurredAt });
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
