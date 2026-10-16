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
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => x.ReservationId).IsUnique();
            b.HasIndex(x => new { x.BranchId, x.StartAt });
            b.HasIndex(x => x.FollowUpId).IsUnique().HasFilter("\"FollowUpId\" IS NOT NULL AND \"Status\" IN ('Confirmed','CheckedIn')");
            b.HasOne<ResourceReservation>().WithOne().HasForeignKey<Appointment>(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.BookerAccountId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
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
    }
}
