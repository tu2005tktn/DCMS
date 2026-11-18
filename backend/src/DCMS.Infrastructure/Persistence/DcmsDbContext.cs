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

public sealed partial class DcmsDbContext(DbContextOptions<DcmsDbContext> options)
: IdentityDbContext<ApplicationUser, IdentityRole<int>, int>(options)
{
    public DbSet<DCMS.Domain.Features.SystemSettings.MaterialCategory> MaterialCategories => Set<DCMS.Domain.Features.SystemSettings.MaterialCategory>();
    public DbSet<DCMS.Domain.Features.SystemSettings.MaterialCatalogItem> MaterialCatalogItems => Set<DCMS.Domain.Features.SystemSettings.MaterialCatalogItem>();
    public DbSet<DCMS.Domain.Features.SystemSettings.MaterialUsage> MaterialUsages => Set<DCMS.Domain.Features.SystemSettings.MaterialUsage>();
    public DbSet<DCMS.Domain.Features.SystemSettings.MaterialPurchase> MaterialPurchases => Set<DCMS.Domain.Features.SystemSettings.MaterialPurchase>();
    public DbSet<DCMS.Domain.Features.SystemSettings.SystemCatalogChange> SystemCatalogChanges => Set<DCMS.Domain.Features.SystemSettings.SystemCatalogChange>();
    public DbSet<DCMS.Domain.Features.SystemSettings.ClinicSettingsVersion> ClinicSettingsVersions => Set<DCMS.Domain.Features.SystemSettings.ClinicSettingsVersion>();
    public DbSet<DCMS.Domain.Features.SystemSettings.ReferenceEntry> ReferenceEntries => Set<DCMS.Domain.Features.SystemSettings.ReferenceEntry>();
    public DbSet<DCMS.Domain.Features.SystemSettings.CatalogProvider> CatalogProviders => Set<DCMS.Domain.Features.SystemSettings.CatalogProvider>();
    public DbSet<DCMS.Domain.Features.SystemSettings.LabWorkType> LabWorkTypes => Set<DCMS.Domain.Features.SystemSettings.LabWorkType>();
    public DbSet<DCMS.Domain.Features.SystemSettings.CatalogMedicine> CatalogMedicines => Set<DCMS.Domain.Features.SystemSettings.CatalogMedicine>();
    public DbSet<DCMS.Domain.Features.Prescriptions.PatientPrescription> PatientPrescriptions => Set<DCMS.Domain.Features.Prescriptions.PatientPrescription>();
    public DbSet<DCMS.Domain.Features.Prescriptions.PrescriptionAudit> PrescriptionAudits => Set<DCMS.Domain.Features.Prescriptions.PrescriptionAudit>();
    public DbSet<EmployeeProfile> Employees => Set<EmployeeProfile>();
    public DbSet<StaffActivityCommandReceipt> StaffActivityCommandReceipts => Set<StaffActivityCommandReceipt>();
    public DbSet<AccountAudit> AccountAudits => Set<AccountAudit>();
    public DbSet<PatientProfile> Patients => Set<PatientProfile>();
    public DbSet<PatientAudit> PatientAudits => Set<PatientAudit>();
    public DbSet<PatientAccountLink> PatientAccountLinks => Set<PatientAccountLink>();
    public DbSet<PatientLinkAudit> PatientLinkAudits => Set<PatientLinkAudit>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<DCMS.Domain.Features.Visits.Visit> Visits => Set<DCMS.Domain.Features.Visits.Visit>();
    public DbSet<AppointmentChange> AppointmentChanges => Set<AppointmentChange>();
    public DbSet<AppointmentChangeReceipt> AppointmentChangeReceipts => Set<AppointmentChangeReceipt>();
    public DbSet<AppointmentReceipt> AppointmentReceipts => Set<AppointmentReceipt>();
    public DbSet<AppointmentOutboxEvent> AppointmentOutboxEvents => Set<AppointmentOutboxEvent>();
    public IQueryable<AppointmentSupportRequest> AppointmentSupportRequests => Appointments.AsNoTracking().SelectMany(x => x.SupportRequests);
    public DbSet<AppointmentSupportHistory> AppointmentSupportHistories => Set<AppointmentSupportHistory>();
    public DbSet<SupportRequestReceipt> SupportRequestReceipts => Set<SupportRequestReceipt>();
    public DbSet<InAppNotification> InAppNotifications => Set<InAppNotification>();
    public DbSet<PushDevice> PushDevices => Set<PushDevice>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public IQueryable<AppointmentReminder> AppointmentReminders => Appointments.AsNoTracking().SelectMany(x => x.Reminders);
    public DbSet<StaffHandlingItem> StaffHandlingItems => Set<StaffHandlingItem>();
    public DbSet<RecoveryAudit> RecoveryAudits => Set<RecoveryAudit>();
    public DbSet<TreatmentDossier> TreatmentDossiers => Set<TreatmentDossier>();
    public DbSet<TreatmentItem> TreatmentItems => Set<TreatmentItem>();
    public DbSet<TreatmentEvent> TreatmentEvents => Set<TreatmentEvent>();
    public DbSet<TreatmentReceipt> TreatmentReceipts => Set<TreatmentReceipt>();
    public IQueryable<TreatmentSheetDraft> TreatmentSheetDrafts => TreatmentDossiers.AsNoTracking().SelectMany(x => x.SheetDrafts);
    public DbSet<LabOrder> LabOrders => Set<LabOrder>();
    public IQueryable<LabOrderStatusLog> LabOrderStatusLogs => LabOrders.AsNoTracking().SelectMany(x => x.StatusLogs);
    public DbSet<TreatmentTemplate> TreatmentTemplates => Set<TreatmentTemplate>();
    public DbSet<TreatmentProgressionReceipt> TreatmentProgressionReceipts => Set<TreatmentProgressionReceipt>();
    public DbSet<TreatmentProgressionEvent> TreatmentProgressionEvents => Set<TreatmentProgressionEvent>();
    public DbSet<ClinicalActivity> ClinicalActivities => Set<ClinicalActivity>();
    public DbSet<ClinicalCommandReceipt> ClinicalCommandReceipts => Set<ClinicalCommandReceipt>();
    public DbSet<CareTask> CareTasks => Set<CareTask>();
    public IQueryable<CareContactAttempt> CareContactAttempts => CareTasks.AsNoTracking().SelectMany(x => x.ContactAttempts);

    public new IQueryable<IdentityRole<int>> Roles => Set<DCMS.Domain.Features.Settings.AccessGroup>()
        .IgnoreQueryFilters().Where(x => x.RecordKind == "IdentityRole")
        .Select(x => new IdentityRole<int> { Id = x.IdentityRoleId!.Value, Name = x.Name,
            NormalizedName = x.Name.ToUpper(), ConcurrencyStamp = x.Version.ToString() });
    public new IQueryable<IdentityUserRole<int>> UserRoles => Users.SelectMany(user => Roles
        .Where(role => user.AssignedRoles.Contains(role.Name!)), (user, role) => new IdentityUserRole<int> { UserId = user.Id, RoleId = role.Id });

    public Task<long> NextAccountEntryIdAsync(CancellationToken ct) => Database
        .SqlQueryRaw<long>("SELECT nextval('account_entry_seq') AS \"Value\"").SingleAsync(ct);

    public async Task<ApplicationUser?> LockAccountAsync(int id, CancellationToken ct)
    {
        await Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM \"USER_ACCOUNT\" WHERE \"Id\" = {id} FOR UPDATE", ct);
        var tracked = Users.Local.SingleOrDefault(x => x.Id == id);
        if (tracked != null) Entry(tracked).State = EntityState.Detached;
        var fresh = await Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (fresh != null)
        {
            var xmin = Entry(fresh).Property<uint>("xmin").CurrentValue;
            foreach (var sibling in ChangeTracker.Entries<EmployeeProfile>().Where(x => x.Entity.AccountId == id).Cast<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry>()
                .Concat(ChangeTracker.Entries<DCMS.Domain.Features.Settings.StaffAccessAssignment>().Where(x => x.Entity.AccountId == id)))
            {
                sibling.Property("xmin").OriginalValue = xmin;
                sibling.Property("xmin").CurrentValue = xmin;
            }
        }
        return fresh;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PrepareSchedulingChanges();
        await PrepareExaminationChangesAsync(cancellationToken);
        PrepareTreatmentChanges();
        var employees = ChangeTracker.Entries<EmployeeProfile>().ToArray();
        var assignments = ChangeTracker.Entries<DCMS.Domain.Features.Settings.StaffAccessAssignment>().ToArray();
        foreach (var id in employees.Where(x => x.State is EntityState.Added or EntityState.Modified).Select(x => x.Entity.AccountId)
            .Concat(assignments.Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).Select(x => x.Entity.AccountId)).Distinct())
            _ = await Users.FindAsync([id], cancellationToken);
        foreach (var entry in employees.Where(x => x.State == EntityState.Added))
            entry.Entity.Id = Users.Local.Single(x => x.Id == entry.Entity.AccountId).EmployeeId;
        ChangeTracker.DetectChanges();
        foreach (var owner in ChangeTracker.Entries<ApplicationUser>().ToArray())
        {
            var relatedEmployees = employees.Where(x => x.Entity.AccountId == owner.Entity.Id).ToArray();
            var relatedAssignments = assignments.Where(x => x.Entity.AccountId == owner.Entity.Id).ToArray();
            if (relatedAssignments.Any(x => x.State == EntityState.Deleted)) owner.Property(x => x.Version).IsModified = true;
            foreach (var relatedEmployee in relatedEmployees)
            {
                if (relatedEmployee.State == EntityState.Added)
                {
                    relatedEmployee.Entity.FullName = owner.Entity.FullName;
                    relatedEmployee.Property(x => x.FullName).OriginalValue = owner.Property(x => x.FullName).OriginalValue;
                }
                else if (owner.Property(x => x.FullName).IsModified && !relatedEmployee.Property(x => x.FullName).IsModified)
                {
                    relatedEmployee.Entity.FullName = owner.Entity.FullName;
                    relatedEmployee.Property(x => x.FullName).CurrentValue = owner.Entity.FullName;
                }
                else if (relatedEmployee.Property(x => x.FullName).IsModified && !owner.Property(x => x.FullName).IsModified)
                {
                    owner.Entity.FullName = relatedEmployee.Entity.FullName;
                    owner.Property(x => x.FullName).CurrentValue = relatedEmployee.Entity.FullName;
                }
            }
            foreach (var related in relatedEmployees.Cast<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry>().Concat(relatedAssignments))
            {
                if (related.State == EntityState.Added)
                {
                    related.Property("xmin").CurrentValue = owner.Property("xmin").CurrentValue;
                    related.Property("xmin").OriginalValue = owner.Property("xmin").OriginalValue;
                }
            }
        }
        var saved = await base.SaveChangesAsync(cancellationToken);
        // Keep shared xmin and FullName synchronized across sibling tracked table-splitting entries after save.
        foreach (var owner in ChangeTracker.Entries<ApplicationUser>().ToArray())
        {
            var siblingEntries = ChangeTracker.Entries<EmployeeProfile>().Where(x => x.Entity.AccountId == owner.Entity.Id)
                .Cast<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry>()
                .Concat(ChangeTracker.Entries<DCMS.Domain.Features.Settings.StaffAccessAssignment>().Where(x => x.Entity.AccountId == owner.Entity.Id))
                .ToArray();
            var latestXmin = new[] { (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry)owner }
                .Concat(siblingEntries)
                .Select(x => (uint)(x.Property("xmin").CurrentValue ?? 0u))
                .DefaultIfEmpty(0u)
                .Max();
            if (latestXmin != 0u)
            {
                owner.Property("xmin").OriginalValue = latestXmin;
                owner.Property("xmin").CurrentValue = latestXmin;
                foreach (var sibling in siblingEntries)
                {
                    sibling.Property("xmin").OriginalValue = latestXmin;
                    sibling.Property("xmin").CurrentValue = latestXmin;
                }
            }
            foreach (var employee in siblingEntries.OfType<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<EmployeeProfile>>())
            {
                employee.Entity.FullName = owner.Entity.FullName;
                employee.Property(x => x.FullName).OriginalValue = owner.Entity.FullName;
                employee.Property(x => x.FullName).CurrentValue = owner.Entity.FullName;
            }
        }
        return saved;
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        SharedStorageModel.Configure(model);
        FamilyMemberModel.Configure(model);
        FamilyRecordModel.Configure(model);
        base.OnModelCreating(model);
        var userType = model.Entity<ApplicationUser>().Metadata;
        foreach (var index in userType.GetIndexes().Where(i => i.Properties.Any(p =>
            p.Name is nameof(ApplicationUser.NormalizedUserName) or nameof(ApplicationUser.NormalizedEmail))).ToArray())
            userType.RemoveIndex(index);
        SchedulingModel.Configure(model);
        AppointmentModel.Configure(model);
        VisitModel.Configure(model);
        ExaminationModel.Configure(model);
        SettingsModel.Configure(model);
        PrescriptionModel.Configure(model);
        TreatmentModel.Configure(model);
        SystemSettingsModel.Configure(model);
        model.Entity<ApplicationUser>(b =>
        {
            b.ToTable("USER_ACCOUNT");
            b.Property(x => x.FullName).HasMaxLength(160);
            b.Property(x => x.AccountType).HasMaxLength(16);
            b.Property(x => x.Version).HasColumnName("Version").IsConcurrencyToken(false);
        });



        model.Entity<StaffActivityCommandReceipt>(b =>
        {
            b.HasIndex(new[] { "ReceiptType", "Id" }, "UX_StaffActivityCommandReceipt_Scope").IsUnique().HasDatabaseName("UX_RECEIPT_Id");
            b.HasIndex(x => new { x.SessionId, x.RequestKey }).IsUnique();
            b.Property(x => x.ResultJson).HasColumnType("jsonb");

        });

        model.Entity<AccountAudit>(b =>
        {
            b.Property(x => x.Action).HasMaxLength(40);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.BeforeJson).HasColumnType("jsonb");
            b.Property(x => x.AfterJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.TargetId, x.OccurredAt });
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.TargetId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Ignore<PatientProfileDetails>();
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
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Ignore(x => x.GuardianName); b.Ignore(x => x.GuardianRelationship); b.Ignore(x => x.GuardianPhone);
            b.Ignore(x => x.Note); b.Ignore(x => x.ProfileDate); b.Ignore(x => x.IntakeHistoryIds);
            b.Ignore(x => x.IntakeHistoryOther); b.Ignore(x => x.IntakeRecordedAt); b.Ignore(x => x.IntakeRecordedBy);
            b.Ignore(x => x.ArchivedAt); b.Ignore(x => x.ArchiveReason);
            b.Property(x => x.Details).HasColumnName("Details").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb")
                .HasConversion(SharedStorageModel.JsonConv<PatientProfileDetails>(), SharedStorageModel.JsonCompare<PatientProfileDetails>());
            b.HasOne<DCMS.Domain.Features.SystemSettings.ReferenceEntry>().WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<DCMS.Domain.Features.SystemSettings.ReferenceEntry>().WithMany().HasForeignKey(x => x.CustomerGroupId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<DCMS.Domain.Features.SystemSettings.ReferenceEntry>().WithMany().HasForeignKey(x => x.OccupationId).OnDelete(DeleteBehavior.Restrict);
        });
        // UC-38: automatic patient codes BN000001... [PROPOSED format].
        model.HasSequence<long>("patient_code_seq");
        model.Entity<PatientCreateReceipt>(b =>
        {
            b.HasIndex(new[] { "ReceiptType", "ActorId", "RequestKey" }, "UX_PatientCreateReceipt_Scope").IsUnique().HasDatabaseName("UX_RECEIPT_ActorId_RequestKey");
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PatientAudit>(b =>
        {
            b.Property(x => x.Action).HasMaxLength(16);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.ChangesJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.PatientId, x.OccurredAt });
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Ignore<PatientAccountLinkDetails>();
        model.Entity<PatientAccountLink>(b =>
        {
            b.ToTable("PATIENT_ACCOUNT_LINK", t => t.HasCheckConstraint("CK_PATIENT_ACCOUNT_LINK_Status",
                "\"Status\" IN ('PENDING','VERIFIED','REJECTED','REVOKED','WITHDRAWN') AND (\"Status\" <> 'VERIFIED' OR \"PatientId\" IS NOT NULL)"));
            b.Property(x => x.RequestCode).HasMaxLength(32);
            b.Property(x => x.Status).HasMaxLength(16);
            b.Property(x => x.DeclaredFullName).HasMaxLength(160);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Ignore(x => x.DeclaredBirthDate); b.Ignore(x => x.DeclaredPatientCode); b.Ignore(x => x.RequestNote);
            b.Ignore(x => x.VerificationNote); b.Ignore(x => x.DecisionReason); b.Ignore(x => x.PublicNote);
            b.Ignore(x => x.ReviewedAt); b.Ignore(x => x.RevokedAt); b.Ignore(x => x.WithdrawnAt);
            b.ComplexProperty(x => x.Details, d => d.ToJson("Details"));
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
            b.Property(x => x.Action).HasMaxLength(40);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.BeforeJson).HasColumnType("jsonb");
            b.Property(x => x.AfterJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.LinkId, x.OccurredAt });
            b.HasOne(x => x.Link).WithMany().HasForeignKey(x => x.LinkId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        });


        model.Entity<RecoveryAudit>(b =>
        {

            b.Property(x => x.Action).HasMaxLength(40).IsRequired();
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.ChannelType).HasMaxLength(16);
            b.Property(x => x.Result).HasMaxLength(20);
            b.HasIndex(x => new { x.TargetId, x.OccurredAt });
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.TargetId).OnDelete(DeleteBehavior.Restrict);
        });

        AccountStorageModel.Configure(model);
        SchedulingStorageModel.Configure(model);
        TreatmentStorageModel.Configure(model);
        SharedStorageModel.ShareColumns(model);
    }
}
