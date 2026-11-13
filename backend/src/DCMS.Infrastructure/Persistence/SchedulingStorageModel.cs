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
        foreach (var type in new[] { typeof(SchedulingMember), typeof(SchedulingChair), typeof(BranchChair), typeof(BranchAsset),
            typeof(AppointmentSupportRequest), typeof(AppointmentReminder) }) model.Ignore(type);
        model.HasSequence<int>("scheduling_chair_seq");
        model.Entity<SchedulingBranch>(b =>
        {
            b.Property(x => x.StorageStamp).IsConcurrencyToken();
            b.ComplexCollection(x => x.Members).ToJson();
            b.ComplexCollection(x => x.Chairs).ToJson();
            b.ComplexCollection(x => x.Assets).ToJson();
        });
        model.Entity<BranchProfile>().Property<Guid>("StorageStamp").IsConcurrencyToken();
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

        model.Entity<ResourceReservation>(b =>
        {
            b.ToTable("APPOINTMENT");
            b.Property(x => x.AppointmentKey).HasColumnName("AppointmentId");
            b.HasAlternateKey(x => x.AppointmentKey).HasName("AK_APPOINTMENT_AppointmentId");
            b.Property(x => x.StorageStamp).IsConcurrencyToken();
            b.Property(x => x.Code).HasColumnName("ReservationCode");
            b.Property(x => x.Status).HasColumnName("ReservationStatus");
            b.Property(x => x.Start).HasColumnName("StartAt");
            b.Property(x => x.End).HasColumnName("EndAt");
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<EmployeeProfile>().WithMany().HasForeignKey(x => x.DentistId).HasPrincipalKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Appointment>(b =>
        {
            b.HasKey(x => x.ReservationId);
            b.Property(x => x.ReservationId).HasColumnName("Id").ValueGeneratedNever();
            b.Property(x => x.Id).HasColumnName("AppointmentId").ValueGeneratedNever();
            b.HasAlternateKey(x => x.Id).HasName("AK_APPOINTMENT_AppointmentId");
            b.Property<Guid>("StorageStamp").IsConcurrencyToken();
            b.HasOne<ResourceReservation>().WithOne().HasForeignKey<Appointment>(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
            b.ComplexCollection(x => x.SupportRequests).ToJson();
            b.ComplexCollection(x => x.Reminders).ToJson();
        });
        foreach (var field in new[] { "BranchId", "DentistId", "AssistantId", "ChairId", "ServiceId" })
        {
            model.Entity<Appointment>().Property(field).HasColumnName(field);
            model.Entity<ResourceReservation>().Property(field).HasColumnName(field);
        }
        model.Entity<Appointment>().Property(x => x.StartAt).HasColumnName("StartAt");
        model.Entity<Appointment>().Property(x => x.EndAt).HasColumnName("EndAt");
        var appointment = model.Entity<Appointment>().Metadata;
        foreach (var fk in model.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()).Where(x => x.PrincipalEntityType == appointment).ToArray())
            model.Entity(fk.DeclaringEntityType.ClrType).HasOne(typeof(Appointment)).WithMany()
                .HasForeignKey(fk.Properties.Select(x => x.Name).ToArray()).HasPrincipalKey(nameof(Appointment.Id));
        model.Entity<NotificationRecord>(b =>
        {
            b.ToTable("IN_APP_NOTIFICATION");
            b.HasKey(x => x.Id);
            b.HasDiscriminator<string>("NotificationRecordKind");
        });
    }
}
