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
            b.ToTable("APPOINTMENT", t => t.HasCheckConstraint("CK_APPOINTMENT_RANGE", "\"StartAt\" < \"EndAt\" AND (\"AssistantId\" IS NULL OR \"AssistantId\" <> \"DentistId\")"));
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
            b.Property(x => x.Version).IsConcurrencyToken(false);
            b.Property(x => x.CancelReason).HasMaxLength(500);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CancelledBy).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PatientConsentConfirmedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.Code).IsUnique();

            b.HasIndex(x => new { x.BranchId, x.StartAt });
            b.HasIndex(x => x.FollowUpId).IsUnique().HasFilter("\"FollowUpId\" IS NOT NULL AND \"Status\" IN ('Confirmed','CheckedIn')");

            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.BookerAccountId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<DCMS.Domain.Features.FamilyMembers.FamilyMember>().WithMany().HasForeignKey(x => x.FamilyMemberId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.BookerAccountId, x.FamilyMemberId, x.StartAt });
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AppointmentChange>(b =>
        {
            b.Property(x => x.ActorKind).HasMaxLength(8);
            b.Property(x => x.Action).HasMaxLength(16);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.RequesterVerification).HasMaxLength(500);
            b.HasIndex(x => new { x.ActorAccountId, x.RequestKey }).IsUnique().HasFilter("\"AuditType\" = 'AppointmentChange'");
            b.HasIndex(x => new { x.AppointmentId, x.OccurredAt });
            b.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AppointmentChangeReceipt>(b =>
        {
            b.HasIndex(new[] { "ReceiptType", "ActorAccountId", "RequestKey" }, "UX_AppointmentChangeReceipt_Scope").IsUnique().HasDatabaseName("UX_RECEIPT_ActorAccountId_RequestKey");
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.ResultJson).HasColumnType("jsonb");
            b.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AppointmentReceipt>(b =>
        {
            b.HasIndex(new[] { "ReceiptType", "RequestKey" }, "UX_AppointmentReceipt_Scope").IsUnique().HasDatabaseName("UX_RECEIPT_RequestKey");
            b.Property(x => x.ActorScope).HasMaxLength(96);
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.ResultJson).HasColumnType("jsonb");
            b.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AppointmentOutboxEvent>(b =>
        {
            b.Property(x => x.EventType).HasMaxLength(80);
            b.Property(x => x.PayloadJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.DispatchedAt, x.OccurredAt });
            b.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AggregateId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<AppointmentSupportHistory>(b =>
        {
            b.Property(x => x.ActorKind).HasMaxLength(16);
            b.Property(x => x.Action).HasMaxLength(40);
            b.Property(x => x.Notes).HasMaxLength(500);
            b.HasIndex(x => new { x.RequestId, x.OccurredAt });

            b.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SupportRequestReceipt>(b =>
        {
            b.HasIndex(new[] { "ReceiptType", "ActorAccountId", "RequestKey" }, "UX_SupportRequestReceipt_Scope").IsUnique().HasDatabaseName("UX_RECEIPT_ActorAccountId_RequestKey");
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.ResultJson).HasColumnType("jsonb");

            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<InAppNotification>(b =>
        {
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
            // Columns shared with StaffHandlingItem rows of the same table.
            b.Property(x => x.Kind).HasColumnName("Kind");
            b.Property(x => x.BranchId).HasColumnName("BranchId");
            b.Property(x => x.AppointmentId).HasColumnName("AppointmentId");
            b.Property(x => x.CreatedAt).HasColumnName("CreatedAt");
        });
        // Deliveries and staff handling items are row kinds of IN_APP_NOTIFICATION (docs/specs/NOTIFICATION-STORAGE.vi.md).
        model.Entity<NotificationDelivery>(b =>
        {
            b.Property(x => x.NotificationId).HasColumnName("NotificationId");
            b.Property(x => x.Channel).HasMaxLength(16);
            b.Property(x => x.Status).HasMaxLength(16).HasColumnName("Status");
            b.Property(x => x.AttemptCount).HasColumnName("AttemptCount");
            b.Property(x => x.LastError).HasMaxLength(1000);
            b.HasIndex(x => new { x.Status, x.NextRetryAt }).HasFilter("\"NotificationRecordKind\" = 'NotificationDelivery'");
            b.HasIndex(x => new { x.NotificationId, x.Channel }).IsUnique().HasFilter("\"NotificationRecordKind\" = 'NotificationDelivery'");
            b.HasOne<InAppNotification>().WithMany(x => x.Deliveries).HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<StaffHandlingItem>(b =>
        {
            b.Property(x => x.Kind).HasMaxLength(40).HasColumnName("Kind");
            b.Property(x => x.BranchId).HasColumnName("BranchId");
            b.Property(x => x.AppointmentId).HasColumnName("AppointmentId");
            b.Property(x => x.NotificationId).HasColumnName("NotificationId");
            b.Property(x => x.AttemptCount).HasColumnName("AttemptCount");
            b.Property(x => x.Error).HasMaxLength(1000);
            b.Property(x => x.Status).HasMaxLength(16).HasColumnName("Status");
            b.Property(x => x.CreatedAt).HasColumnName("CreatedAt");
            b.HasIndex(x => new { x.Status, x.CreatedAt }).HasFilter("\"NotificationRecordKind\" = 'StaffHandlingItem'");
            b.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<InAppNotification>().WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
