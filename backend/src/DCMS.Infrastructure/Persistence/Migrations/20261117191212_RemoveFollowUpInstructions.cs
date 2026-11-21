using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFollowUpInstructions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CLINICAL_ACTIVITY_ActiveAppointmentId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropIndex(
                name: "IX_CLINICAL_ACTIVITY_PatientId_Status",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropIndex(
                name: "IX_CLINICAL_ACTIVITY_ProcessKind_RecordId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropIndex(
                name: "IX_FOLLOW_UP_INSTRUCTION_ActiveItemOnly",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropIndex(
                name: "IX_FOLLOW_UP_INSTRUCTION_ActiveStep",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropIndex(
                name: "IX_FOLLOW_UP_INSTRUCTION_Step_Completion",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CLINICAL_ACTIVITY_VALUES",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FOLLOW_UP_DURATION",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FOLLOW_UP_WINDOW",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropIndex(
                name: "IX_APPOINTMENT_FollowUpId",
                table: "APPOINTMENT");

            // Activities must already use one id for both columns before RecordId is dropped.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "CLINICAL_ACTIVITY" WHERE "ProcessKind" = 'ClinicalActivity' AND "Id" <> "RecordId") THEN
                        RAISE EXCEPTION 'CLINICAL_ACTIVITY has activities whose Id differs from RecordId';
                    END IF;
                END $$;
                """);
            // A follow-up appointment now points at the instruction's treatment dossier; one without a dossier becomes Standard.
            migrationBuilder.Sql("""
                UPDATE "APPOINTMENT" AS a SET "FollowUpId" = (
                    SELECT f."DossierId" FROM "CLINICAL_ACTIVITY" AS f
                    WHERE f."ProcessKind" = 'FollowUpInstruction' AND f."RecordId" = a."FollowUpId")
                WHERE a."FollowUpId" IS NOT NULL;
                UPDATE "APPOINTMENT" SET "Kind" = 'Standard' WHERE "Kind" = 'FollowUp' AND "FollowUpId" IS NULL;
                DELETE FROM "CLINICAL_ACTIVITY" WHERE "ProcessKind" <> 'ClinicalActivity';
                """);

            migrationBuilder.DropColumn(
                name: "ActiveAppointmentId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "Capability",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "DentistId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "EarliestOn",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "Guidance",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "LatestOn",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "ProcessKind",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "RecordId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "SourceActivityId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "SourceCompletionEventId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "SourceVisitId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "StorageStamp",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "TargetOn",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "InstructionId",
                table: "CARE_TASK");

            migrationBuilder.RenameColumn(
                name: "FollowUpId",
                table: "APPOINTMENT",
                newName: "DossierId");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "CLINICAL_ACTIVITY",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Unit",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "State",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ServiceId",
                table: "CLINICAL_ACTIVITY",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "CLINICAL_ACTIVITY",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,3)",
                oldPrecision: 12,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PriceSnapshotVnd",
                table: "CLINICAL_ACTIVITY",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,0)",
                oldPrecision: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PerformerId",
                table: "CLINICAL_ACTIVITY",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "PerformedAt",
                table: "CLINICAL_ACTIVITY",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PatientId",
                table: "CLINICAL_ACTIVITY",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Outcome",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsPlanTransfer",
                table: "CLINICAL_ACTIVITY",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FeeStatus",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Unavailable",
                oldClrType: typeof(string),
                oldType: "character varying(24)",
                oldMaxLength: 24,
                oldNullable: true,
                oldDefaultValue: "Unavailable");

            migrationBuilder.AlterColumn<long>(
                name: "DiscountSnapshotVnd",
                table: "CLINICAL_ACTIVITY",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "CLINICAL_ACTIVITY",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ChargingBasis",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BranchId",
                table: "CLINICAL_ACTIVITY",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CLINICAL_ACTIVITY_VALUES",
                table: "CLINICAL_ACTIVITY",
                sql: "\"Quantity\" > 0 AND \"PriceSnapshotVnd\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_DossierId",
                table: "APPOINTMENT",
                column: "DossierId");

            migrationBuilder.AddForeignKey(
                name: "FK_APPOINTMENT_TREATMENT_DOSSIER_DossierId",
                table: "APPOINTMENT",
                column: "DossierId",
                principalTable: "TREATMENT_DOSSIER",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_APPOINTMENT_TREATMENT_DOSSIER_DossierId",
                table: "APPOINTMENT");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CLINICAL_ACTIVITY_VALUES",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropIndex(
                name: "IX_APPOINTMENT_DossierId",
                table: "APPOINTMENT");

            migrationBuilder.RenameColumn(
                name: "DossierId",
                table: "APPOINTMENT",
                newName: "FollowUpId");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "CLINICAL_ACTIVITY",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Unit",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "State",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<int>(
                name: "ServiceId",
                table: "CLINICAL_ACTIVITY",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "CLINICAL_ACTIVITY",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,3)",
                oldPrecision: 12,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "PriceSnapshotVnd",
                table: "CLINICAL_ACTIVITY",
                type: "numeric(18,0)",
                precision: 18,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,0)",
                oldPrecision: 18,
                oldScale: 0);

            migrationBuilder.AlterColumn<int>(
                name: "PerformerId",
                table: "CLINICAL_ACTIVITY",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "PerformedAt",
                table: "CLINICAL_ACTIVITY",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<int>(
                name: "PatientId",
                table: "CLINICAL_ACTIVITY",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Outcome",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<bool>(
                name: "IsPlanTransfer",
                table: "CLINICAL_ACTIVITY",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "FeeStatus",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true,
                defaultValue: "Unavailable",
                oldClrType: typeof(string),
                oldType: "character varying(24)",
                oldMaxLength: 24,
                oldDefaultValue: "Unavailable");

            migrationBuilder.AlterColumn<long>(
                name: "DiscountSnapshotVnd",
                table: "CLINICAL_ACTIVITY",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "CLINICAL_ACTIVITY",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "ChargingBasis",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<int>(
                name: "BranchId",
                table: "CLINICAL_ACTIVITY",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<Guid>(
                name: "ActiveAppointmentId",
                table: "CLINICAL_ACTIVITY",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Capability",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "CLINICAL_ACTIVITY",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DentistId",
                table: "CLINICAL_ACTIVITY",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "CLINICAL_ACTIVITY",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EarliestOn",
                table: "CLINICAL_ACTIVITY",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Guidance",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LatestOn",
                table: "CLINICAL_ACTIVITY",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessKind",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecordId",
                table: "CLINICAL_ACTIVITY",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SourceActivityId",
                table: "CLINICAL_ACTIVITY",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceCompletionEventId",
                table: "CLINICAL_ACTIVITY",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceVisitId",
                table: "CLINICAL_ACTIVITY",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StorageStamp",
                table: "CLINICAL_ACTIVITY",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateOnly>(
                name: "TargetOn",
                table: "CLINICAL_ACTIVITY",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "CLINICAL_ACTIVITY",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InstructionId",
                table: "CARE_TASK",
                type: "uuid",
                nullable: true);

            // Deleted follow-up instructions cannot be restored; their appointments stay Standard.
            migrationBuilder.Sql("""
                UPDATE "CLINICAL_ACTIVITY" SET "ProcessKind" = 'ClinicalActivity', "RecordId" = "Id", "StorageStamp" = gen_random_uuid();
                UPDATE "APPOINTMENT" SET "Kind" = 'Standard', "FollowUpId" = NULL WHERE "FollowUpId" IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CLINICAL_ACTIVITY_ActiveAppointmentId",
                table: "CLINICAL_ACTIVITY",
                column: "ActiveAppointmentId",
                unique: true,
                filter: "\"ActiveAppointmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CLINICAL_ACTIVITY_PatientId_Status",
                table: "CLINICAL_ACTIVITY",
                columns: new[] { "PatientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CLINICAL_ACTIVITY_ProcessKind_RecordId",
                table: "CLINICAL_ACTIVITY",
                columns: new[] { "ProcessKind", "RecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FOLLOW_UP_INSTRUCTION_ActiveItemOnly",
                table: "CLINICAL_ACTIVITY",
                columns: new[] { "DossierId", "ItemId" },
                unique: true,
                filter: "\"DossierId\" IS NOT NULL AND \"ItemId\" IS NOT NULL AND \"StepId\" IS NULL AND \"Status\" IN ('NeedsBooking','Booked')");

            migrationBuilder.CreateIndex(
                name: "IX_FOLLOW_UP_INSTRUCTION_ActiveStep",
                table: "CLINICAL_ACTIVITY",
                column: "StepId",
                unique: true,
                filter: "\"StepId\" IS NOT NULL AND \"Status\" IN ('NeedsBooking','Booked')");

            migrationBuilder.CreateIndex(
                name: "IX_FOLLOW_UP_INSTRUCTION_Step_Completion",
                table: "CLINICAL_ACTIVITY",
                columns: new[] { "StepId", "SourceCompletionEventId" },
                unique: true,
                filter: "\"StepId\" IS NOT NULL AND \"SourceCompletionEventId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CLINICAL_ACTIVITY_VALUES",
                table: "CLINICAL_ACTIVITY",
                sql: "\"ProcessKind\" <> 'ClinicalActivity' OR (\"Quantity\" > 0 AND \"PriceSnapshotVnd\" >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FOLLOW_UP_DURATION",
                table: "CLINICAL_ACTIVITY",
                sql: "\"ProcessKind\" <> 'FollowUpInstruction' OR \"DurationMinutes\" BETWEEN 15 AND 480 AND \"DurationMinutes\" % 15 = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FOLLOW_UP_WINDOW",
                table: "CLINICAL_ACTIVITY",
                sql: "\"ProcessKind\" <> 'FollowUpInstruction' OR (\"EarliestOn\" <= \"TargetOn\" AND \"TargetOn\" <= \"LatestOn\")");

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_FollowUpId",
                table: "APPOINTMENT",
                column: "FollowUpId",
                unique: true,
                filter: "\"FollowUpId\" IS NOT NULL AND \"Status\" IN ('Confirmed','CheckedIn')");
        }
    }
}
