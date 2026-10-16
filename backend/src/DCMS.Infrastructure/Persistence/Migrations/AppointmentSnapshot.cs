using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence.Migrations;

// Snapshot fragment for the manually reviewed UC-04 migration.
internal static class AppointmentSnapshot
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity("DCMS.Domain.Features.Appointments.Appointment", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<Guid>("ReservationId").HasColumnType("uuid");
            b.Property<string>("Code").IsRequired().HasMaxLength(40).HasColumnType("character varying(40)");
            b.Property<int>("BranchId").HasColumnType("integer");
            b.Property<int?>("BookerAccountId").HasColumnType("integer");
            b.Property<string>("BookerName").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<string>("BookerPhone").HasMaxLength(24).HasColumnType("character varying(24)");
            b.Property<string>("BookerEmail").HasMaxLength(254).HasColumnType("character varying(254)");
            b.Property<int?>("PatientId").HasColumnType("integer");
            b.Property<string>("AttendeeName").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<DateOnly?>("AttendeeBirthDate").HasColumnType("date");
            b.Property<string>("AttendeeRelation").IsRequired().HasMaxLength(16).HasColumnType("character varying(16)");
            b.Property<string>("Kind").IsRequired().HasMaxLength(16).HasColumnType("character varying(16)");
            b.Property<string>("Source").IsRequired().HasMaxLength(24).HasColumnType("character varying(24)");
            b.Property<int?>("ServiceId").HasColumnType("integer");
            b.Property<string>("Reason").HasMaxLength(1000).HasColumnType("character varying(1000)");
            b.Property<int>("DentistId").HasColumnType("integer");
            b.Property<int?>("AssistantId").HasColumnType("integer");
            b.Property<int>("ChairId").HasColumnType("integer");
            b.Property<DateTimeOffset>("StartAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("EndAt").HasColumnType("timestamp with time zone");
            b.Property<Guid?>("FollowUpId").HasColumnType("uuid");
            b.Property<string>("ExceptionReason").HasMaxLength(500).HasColumnType("character varying(500)");
            b.Property<string>("Status").IsRequired().HasMaxLength(24).HasColumnType("character varying(24)");
            b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("Code").IsUnique();
            b.HasIndex("ReservationId").IsUnique();
            b.HasIndex("BookerAccountId");
            b.HasIndex("PatientId");
            b.HasIndex("BranchId", "StartAt");
            b.HasIndex("FollowUpId").IsUnique().HasFilter("\"FollowUpId\" IS NOT NULL AND \"Status\" IN ('Confirmed','CheckedIn')");
            b.ToTable("APPOINTMENT", (string?)null, t => t.HasCheckConstraint("CK_APPOINTMENT_RANGE", "\"StartAt\" < \"EndAt\""));
        });
        modelBuilder.Entity("DCMS.Domain.Features.Appointments.AppointmentReceipt", b =>
        {
            b.Property<Guid>("RequestKey").HasColumnType("uuid");
            b.Property<string>("ActorScope").IsRequired().HasMaxLength(96).HasColumnType("character varying(96)");
            b.Property<string>("Fingerprint").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            b.Property<string>("ResultJson").IsRequired().HasColumnType("jsonb");
            b.Property<Guid>("AppointmentId").HasColumnType("uuid");
            b.HasKey("RequestKey");
            b.HasIndex("AppointmentId");
            b.ToTable("APPOINTMENT_RECEIPT", (string?)null);
        });
        modelBuilder.Entity("DCMS.Domain.Features.Appointments.AppointmentOutboxEvent", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<Guid>("AggregateId").HasColumnType("uuid");
            b.Property<string>("EventType").IsRequired().HasMaxLength(80).HasColumnType("character varying(80)");
            b.Property<string>("PayloadJson").IsRequired().HasColumnType("jsonb");
            b.Property<DateTimeOffset>("OccurredAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset?>("DispatchedAt").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("AggregateId");
            b.HasIndex("DispatchedAt", "OccurredAt");
            b.ToTable("APPOINTMENT_OUTBOX", (string?)null);
        });
        modelBuilder.Entity("DCMS.Domain.Features.Appointments.Appointment", b =>
        {
            b.HasOne("DCMS.Domain.Features.Scheduling.ResourceReservation", null).WithOne()
                .HasForeignKey("DCMS.Domain.Features.Appointments.Appointment", "ReservationId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("DCMS.Infrastructure.Features.Accounts.ApplicationUser", null).WithMany()
                .HasForeignKey("BookerAccountId").OnDelete(DeleteBehavior.Restrict);
            b.HasOne("DCMS.Domain.Features.Patients.PatientProfile", null).WithMany()
                .HasForeignKey("PatientId").OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity("DCMS.Domain.Features.Appointments.AppointmentReceipt", b =>
            b.HasOne("DCMS.Domain.Features.Appointments.Appointment", null).WithMany()
                .HasForeignKey("AppointmentId").OnDelete(DeleteBehavior.Restrict).IsRequired());
        modelBuilder.Entity("DCMS.Domain.Features.Appointments.AppointmentOutboxEvent", b =>
            b.HasOne("DCMS.Domain.Features.Appointments.Appointment", null).WithMany()
                .HasForeignKey("AggregateId").OnDelete(DeleteBehavior.Restrict).IsRequired());
    }
}
