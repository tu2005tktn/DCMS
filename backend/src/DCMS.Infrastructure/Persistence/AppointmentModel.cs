using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

internal static class AppointmentModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<Appointment>(b =>
        {
            b.ToTable("APPOINTMENT", t => t.HasCheckConstraint("CK_APPOINTMENT_RANGE", "\"StartAt\" < \"EndAt\""));
            b.Property(x => x.Code).HasMaxLength(40);
            b.Property(x => x.BookerName).HasMaxLength(100);
            b.Property(x => x.BookerPhone).HasMaxLength(24);
            b.Property(x => x.BookerEmail).HasMaxLength(254);
            b.Property(x => x.AttendeeName).HasMaxLength(100);
            b.Property(x => x.AttendeeRelation).HasMaxLength(16);
            b.Property(x => x.Kind).HasMaxLength(16);
            b.Property(x => x.Source).HasMaxLength(24);
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.ExceptionReason).HasMaxLength(500);
            b.Property(x => x.Status).HasMaxLength(24);
            b.Property(x => x.ConfirmedToAttend).HasMaxLength(16).HasDefaultValue("Pending");
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.CancelReason).HasMaxLength(500);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CancelledBy).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => x.ReservationId).IsUnique();
            b.HasIndex(x => new { x.BranchId, x.StartAt });
            b.HasIndex(x => x.FollowUpId).IsUnique().HasFilter("\"FollowUpId\" IS NOT NULL AND \"Status\" IN ('Confirmed','CheckedIn')");
            b.HasOne<ResourceReservation>().WithOne().HasForeignKey<Appointment>(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.BookerAccountId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AppointmentChange>(b =>
        {
            b.ToTable("APPOINTMENT_CHANGE");
            b.Property(x => x.ActorKind).HasMaxLength(8);
            b.Property(x => x.Action).HasMaxLength(16);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.RequesterVerification).HasMaxLength(500);
            b.HasIndex(x => new { x.ActorAccountId, x.RequestKey }).IsUnique();
            b.HasIndex(x => new { x.AppointmentId, x.OccurredAt });
            b.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AppointmentChangeReceipt>(b =>
        {
            b.ToTable("APPOINTMENT_CHANGE_RECEIPT");
            b.HasKey(x => new { x.ActorAccountId, x.RequestKey });
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.ResultJson).HasColumnType("jsonb");
            b.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AppointmentReceipt>(b =>
        {
            b.ToTable("APPOINTMENT_RECEIPT");
            b.HasKey(x => x.RequestKey);
            b.Property(x => x.ActorScope).HasMaxLength(96);
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.ResultJson).HasColumnType("jsonb");
            b.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AppointmentOutboxEvent>(b =>
        {
            b.ToTable("APPOINTMENT_OUTBOX");
            b.Property(x => x.EventType).HasMaxLength(80);
            b.Property(x => x.PayloadJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.DispatchedAt, x.OccurredAt });
            b.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AggregateId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AppointmentSupportRequest>(b =>
        {
            b.ToTable("APPOINTMENT_SUPPORT_REQUEST", t => t.HasCheckConstraint("CK_SUPPORT_REQUEST_STATUS",
                "\"Status\" IN ('Pending','Resolved','Rejected','Withdrawn')"));
            b.Property(x => x.Code).HasMaxLength(32);
            b.Property(x => x.Kind).HasMaxLength(24);
            b.Property(x => x.Status).HasMaxLength(16);
            b.Property(x => x.RequesterName).HasMaxLength(160);
            b.Property(x => x.RequesterPhone).HasMaxLength(24);
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.ProposedReason).HasMaxLength(40);
            b.Property(x => x.RequesterVerification).HasMaxLength(500);
            b.Property(x => x.DecisionReason).HasMaxLength(500);
            b.Property(x => x.PublicNote).HasMaxLength(500);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => x.RequestKey).IsUnique();
            b.HasIndex(x => new { x.Status, x.UpdatedAt });
            b.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RequesterAccountId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RequesterVerifiedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.DecidedBy).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AppointmentSupportHistory>(b =>
        {
            b.ToTable("APPOINTMENT_SUPPORT_HISTORY");
            b.Property(x => x.ActorKind).HasMaxLength(16);
            b.Property(x => x.Action).HasMaxLength(40);
            b.Property(x => x.Notes).HasMaxLength(500);
            b.HasIndex(x => new { x.RequestId, x.OccurredAt });
            b.HasOne<AppointmentSupportRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SupportRequestReceipt>(b =>
        {
            b.ToTable("SUPPORT_REQUEST_RECEIPT");
            b.HasKey(x => new { x.ActorAccountId, x.RequestKey });
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.ResultJson).HasColumnType("jsonb");
            b.HasOne<AppointmentSupportRequest>().WithMany().HasForeignKey(x => x.SupportRequestId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<InAppNotification>(b =>
        {
            b.ToTable("IN_APP_NOTIFICATION");
            b.Property(x => x.PublicId).HasMaxLength(40);
            b.Property(x => x.Kind).HasMaxLength(40);
            b.Property(x => x.Title).HasMaxLength(160);
            b.Property(x => x.Body).HasMaxLength(1000);
            b.Property(x => x.DeepLink).HasMaxLength(300);
            b.Property(x => x.DedupeKey).HasMaxLength(160);
            b.HasIndex(x => x.PublicId).IsUnique();
            b.HasIndex(x => x.DedupeKey).IsUnique();
            b.HasIndex(x => new { x.AccountId, x.CreatedAt });
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<AppointmentSupportRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<NotificationDelivery>(b =>
        {
            b.ToTable("NOTIFICATION_DELIVERY");
            b.Property(x => x.Channel).HasMaxLength(16);
            b.Property(x => x.Status).HasMaxLength(16);
            b.Property(x => x.LastError).HasMaxLength(1000);
            b.HasIndex(x => new { x.Status, x.NextRetryAt });
            b.HasIndex(x => new { x.NotificationId, x.Channel }).IsUnique();
            b.HasOne<InAppNotification>().WithMany(x => x.Deliveries).HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<AppointmentReminder>(b =>
        {
            b.ToTable("APPOINTMENT_REMINDER");
            b.Property(x => x.Status).HasMaxLength(16);
            b.HasIndex(x => new { x.Status, x.DueAt });
            b.HasIndex(x => new { x.AppointmentId, x.AppointmentVersion, x.LeadMinutes }).IsUnique();
            b.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<StaffHandlingItem>(b =>
        {
            b.ToTable("STAFF_HANDLING_ITEM");
            b.Property(x => x.Kind).HasMaxLength(40);
            b.Property(x => x.Error).HasMaxLength(1000);
            b.Property(x => x.Status).HasMaxLength(16);
            b.HasIndex(x => new { x.Status, x.CreatedAt });
            b.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<InAppNotification>().WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
