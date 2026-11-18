using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Settings;
using DCMS.Infrastructure.Features.Accounts;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

internal static class SchedulingStorageModel
{
    internal static void Configure(ModelBuilder model)
    {
        foreach (var type in new[] { typeof(SchedulingMember), typeof(SchedulingChair), typeof(BranchChair) }) model.Ignore(type);
        model.HasSequence<int>("scheduling_chair_seq");
        model.Entity<SchedulingBranch>(b =>
        {
            b.ComplexCollection(x => x.Members).ToJson();
            b.ComplexCollection(x => x.Chairs).ToJson();
        });
        model.Entity<WorkingScheduleRecord>(b =>
        {
            b.ToTable("WORKING_SCHEDULE", t => t.HasCheckConstraint("CK_WORKING_SCHEDULE_KIND",
                "\"ScheduleKind\" <> 'WorkingScheduleRevision' OR (\"Kind\" IN ('Template','Exception') AND \"Version\" > 0)"));
            b.HasKey(x => x.Id);
            b.HasDiscriminator<string>("ScheduleKind");
            b.Property(x => x.Version).IsConcurrencyToken();
            b.ComplexCollection(x => x.Periods).ToJson();
            b.HasOne<EmployeeProfile>().WithMany().HasForeignKey(x => x.EmployeeId).HasPrincipalKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PublishedBy).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<WorkingScheduleRevision>().HasIndex(x => new { x.EmployeeId, x.Version })
            .IsUnique().HasFilter("\"ScheduleKind\" = 'WorkingScheduleRevision'");
        model.Entity<WorkingWeek>().HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);

        model.Entity<PushDevice>(b =>
        {
            b.ToTable("PUSH_DEVICE");
            b.HasKey(x => x.Id);
            b.Property(x => x.Token).HasMaxLength(512);
            b.Property(x => x.Platform).HasMaxLength(16);
            b.HasIndex(x => x.AccountId);
            b.HasIndex(x => x.Token).IsUnique();
        });
        model.Ignore<NotificationData>();
        model.Entity<NotificationRecord>(b =>
        {
            b.ToTable("IN_APP_NOTIFICATION");
            b.HasKey(x => x.Id);
            b.HasDiscriminator<string>("NotificationRecordKind");
            b.Property(x => x.Data).HasColumnName("Data").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb")
                .HasConversion(SharedStorageModel.JsonConv<NotificationData>(), SharedStorageModel.JsonCompare<NotificationData>());
        });
    }
}
