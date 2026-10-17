using System;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DcmsDbContext))]
[Migration("20261016120000_Appointments")]
public partial class Appointments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(name: "ServiceId", table: "RESOURCE_RESERVATION", type: "integer",
            nullable: true, oldClrType: typeof(int), oldType: "integer");

        migrationBuilder.CreateTable(name: "APPOINTMENT", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
            Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
            BranchId = table.Column<int>(type: "integer", nullable: false),
            BookerAccountId = table.Column<int>(type: "integer", nullable: true),
            BookerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            BookerPhone = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
            BookerEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
            PatientId = table.Column<int>(type: "integer", nullable: true),
            AttendeeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            AttendeeBirthDate = table.Column<DateOnly>(type: "date", nullable: true),
            AttendeeRelation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            Source = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
            ServiceId = table.Column<int>(type: "integer", nullable: true),
            Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
            DentistId = table.Column<int>(type: "integer", nullable: false),
            AssistantId = table.Column<int>(type: "integer", nullable: true),
            ChairId = table.Column<int>(type: "integer", nullable: false),
            StartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            EndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            FollowUpId = table.Column<Guid>(type: "uuid", nullable: true),
            ExceptionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
            Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
            Version = table.Column<int>(type: "integer", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_APPOINTMENT", x => x.Id);
            table.CheckConstraint("CK_APPOINTMENT_RANGE", "\"StartAt\" < \"EndAt\"");
            table.ForeignKey("FK_APPOINTMENT_RESOURCE_RESERVATION_ReservationId", x => x.ReservationId,
                "RESOURCE_RESERVATION", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_APPOINTMENT_USER_ACCOUNT_BookerAccountId", x => x.BookerAccountId,
                "USER_ACCOUNT", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_APPOINTMENT_PATIENT_PROFILE_PatientId", x => x.PatientId,
                "PATIENT_PROFILE", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_APPOINTMENT_Code", table: "APPOINTMENT", column: "Code", unique: true);
        migrationBuilder.CreateIndex(name: "IX_APPOINTMENT_ReservationId", table: "APPOINTMENT", column: "ReservationId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_APPOINTMENT_BranchId_StartAt", table: "APPOINTMENT", columns: new[] { "BranchId", "StartAt" });
        migrationBuilder.CreateIndex(name: "IX_APPOINTMENT_BookerAccountId", table: "APPOINTMENT", column: "BookerAccountId");
        migrationBuilder.CreateIndex(name: "IX_APPOINTMENT_PatientId", table: "APPOINTMENT", column: "PatientId");
        migrationBuilder.CreateIndex(name: "IX_APPOINTMENT_FollowUpId", table: "APPOINTMENT", column: "FollowUpId", unique: true,
            filter: "\"FollowUpId\" IS NOT NULL AND \"Status\" IN ('Confirmed','CheckedIn')");

        migrationBuilder.CreateTable(name: "APPOINTMENT_RECEIPT", columns: table => new
        {
            RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
            ActorScope = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
            Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            ResultJson = table.Column<string>(type: "jsonb", nullable: false),
            AppointmentId = table.Column<Guid>(type: "uuid", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_APPOINTMENT_RECEIPT", x => x.RequestKey);
            table.ForeignKey("FK_APPOINTMENT_RECEIPT_APPOINTMENT_AppointmentId", x => x.AppointmentId,
                "APPOINTMENT", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_APPOINTMENT_RECEIPT_AppointmentId", table: "APPOINTMENT_RECEIPT", column: "AppointmentId");

        migrationBuilder.CreateTable(name: "APPOINTMENT_OUTBOX", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
            EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
            PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
            OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            DispatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_APPOINTMENT_OUTBOX", x => x.Id);
            table.ForeignKey("FK_APPOINTMENT_OUTBOX_APPOINTMENT_AggregateId", x => x.AggregateId,
                "APPOINTMENT", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_APPOINTMENT_OUTBOX_AggregateId", table: "APPOINTMENT_OUTBOX", column: "AggregateId");
        migrationBuilder.CreateIndex(name: "IX_APPOINTMENT_OUTBOX_DispatchedAt_OccurredAt", table: "APPOINTMENT_OUTBOX",
            columns: new[] { "DispatchedAt", "OccurredAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("APPOINTMENT_OUTBOX");
        migrationBuilder.DropTable("APPOINTMENT_RECEIPT");
        migrationBuilder.DropTable("APPOINTMENT");
        migrationBuilder.AlterColumn<int>(name: "ServiceId", table: "RESOURCE_RESERVATION", type: "integer",
            nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
    }
}
