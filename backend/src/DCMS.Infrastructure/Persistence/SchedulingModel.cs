using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

internal static class SchedulingModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<WorkingScheduleRevision>(b =>
        {
            b.ToTable("WORKING_SCHEDULE_REVISION", t => t.HasCheckConstraint("CK_REVISION_KIND", "\"Kind\" IN ('Template','Exception') AND \"Version\" > 0"));
            b.Property(x => x.Kind).HasMaxLength(16); b.Property(x => x.Reason).HasMaxLength(500);
            b.HasIndex(x => new { x.EmployeeId, x.Version }).IsUnique();
            b.HasIndex(x => new { x.EmployeeId, x.Date });
            b.HasOne<EmployeeProfile>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PublishedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Periods).WithOne().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<RevisionPeriod>(b =>
        {
            b.ToTable("WORKING_REVISION_PERIOD", t => t.HasCheckConstraint("CK_REVISION_PERIOD_RANGE", "\"Day\" BETWEEN 0 AND 6 AND \"StartMinute\" >= 0 AND \"EndMinute\" <= 1440 AND \"StartMinute\" < \"EndMinute\" AND \"StartMinute\" % 30 = 0 AND \"EndMinute\" % 30 = 0 AND \"Kind\" IN ('Work','Break','Block')"));
            b.Property(x => x.Kind).HasMaxLength(16); b.Property(x => x.Reason).HasMaxLength(500);
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SchedulingBranch>(b => {
            b.ToTable("SCHEDULING_BRANCH", t => t.HasCheckConstraint("CK_BRANCH_SLOT_INTERVAL", "\"SlotIntervalMinutes\" BETWEEN 1 AND 120 AND \"BookingSettingsVersion\" > 0"));
            b.Property(x => x.Name).HasMaxLength(160);
            b.Property(x => x.SlotIntervalMinutes).HasDefaultValue(15);
            b.Property(x => x.BookingSettingsVersion).HasDefaultValue(1).IsConcurrencyToken();
        });
        model.Entity<SchedulingMember>(b =>
        {
            b.ToTable("SCHEDULING_MEMBER"); b.HasKey(x => new { x.BranchId, x.EmployeeId });
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<EmployeeProfile>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SchedulingChair>(b =>
        {
            b.ToTable("SCHEDULING_CHAIR", t => t.HasCheckConstraint("CK_CHAIR_HOURS", "\"OpenMinute\" >= 0 AND \"CloseMinute\" <= 1440 AND \"OpenMinute\" < \"CloseMinute\" AND \"OpenMinute\" % 15 = 0 AND \"CloseMinute\" % 15 = 0"));
            b.Property(x => x.Name).HasMaxLength(160);
            b.HasAlternateKey(x => new { x.Id, x.BranchId });
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SchedulingService>(b =>
        {
            b.Property(x => x.Unit).HasMaxLength(40).HasDefaultValue("lần");
            b.HasIndex(x => x.BranchId).IsUnique().HasFilter("\"IsInitialExamination\" = TRUE");
            b.Property(x => x.PublicBookable).HasDefaultValue(true);
            b.ToTable("SCHEDULING_SERVICE", t => t.HasCheckConstraint("CK_SERVICE_DURATION", "\"PublicDurationMinutes\" BETWEEN 1 AND 480"));
            b.Property(x => x.Name).HasMaxLength(160);
            b.Property(x => x.PriceVersion).IsConcurrencyToken().HasDefaultValue(1);
            b.ToTable(t => t.HasCheckConstraint("CK_SERVICE_REFERENCE_PRICE", "\"ReferenceUnitPriceVnd\" IS NULL OR \"ReferenceUnitPriceVnd\" >= 0"));
            b.HasAlternateKey(x => new { x.Id, x.BranchId });
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<WorkingWeek>(b =>
        {
            b.ToTable("WORKING_WEEK"); b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.BranchId, x.EmployeeId, x.WeekStart }).IsUnique();
            b.HasOne<SchedulingMember>().WithMany().HasForeignKey(x => new { x.BranchId, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PublishedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Periods).WithOne().HasForeignKey(x => x.WeekId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<WorkingPeriod>(b =>
        {
            b.ToTable("WORKING_PERIOD", t => t.HasCheckConstraint("CK_PERIOD_RANGE", "\"Day\" BETWEEN 0 AND 6 AND \"StartMinute\" >= 0 AND \"EndMinute\" <= 1440 AND \"StartMinute\" < \"EndMinute\" AND \"StartMinute\" % 15 = 0 AND \"EndMinute\" % 15 = 0 AND \"Kind\" IN ('Work','Break','Block')"));
            b.Property(x => x.Kind).HasMaxLength(16); b.Property(x => x.Reason).HasMaxLength(500);
        });
        model.Entity<ResourceReservation>(b =>
        {
            b.ToTable("RESOURCE_RESERVATION", t => t.HasCheckConstraint("CK_RESERVATION_RANGE", "\"Start\" < \"End\" AND (\"AssistantId\" IS NULL OR \"AssistantId\" <> \"DentistId\")"));
            b.Property(x => x.Code).HasMaxLength(40); b.Property(x => x.Status).HasMaxLength(16);
            b.Property(x => x.AttendeeKey).HasMaxLength(160); b.Property(x => x.AttendeeLabel).HasMaxLength(160);
            b.HasIndex(x => new { x.Start, x.End }); b.HasIndex(x => x.Code).IsUnique();
            b.HasOne<SchedulingMember>().WithMany().HasForeignKey(x => new { x.BranchId, EmployeeId = x.DentistId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<EmployeeProfile>().WithMany().HasForeignKey(x => x.AssistantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingChair>().WithMany().HasForeignKey(x => new { x.ChairId, x.BranchId }).HasPrincipalKey(x => new { x.Id, x.BranchId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingService>().WithMany().HasForeignKey(x => new { x.ServiceId, x.BranchId }).HasPrincipalKey(x => new { x.Id, x.BranchId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ScheduleReceipt>(b =>
        {
            b.ToTable("SCHEDULE_RECEIPT"); b.HasKey(x => new { x.ActorId, x.RequestKey });
            b.Property(x => x.Fingerprint).HasMaxLength(64); b.Property(x => x.ResultJson).HasColumnType("jsonb");
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
