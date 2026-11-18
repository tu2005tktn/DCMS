using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Simplifies APPOINTMENT from 40 to 22 columns (Option B): removes ResourceReservation table-splitting
    /// and unused/redundant columns, consolidates BookerName/BookerContact/AttendeeBirthDate/AttendeeRelation/ExceptionReason
    /// into BookingInfo JSONB, uses Version as the sole concurrency token, and re-targets referencing FKs to APPOINTMENT(Id).
    /// See docs/specs/APPOINTMENT-TABLE-SIMPLIFICATION.vi.md.
    /// </summary>
    public partial class SimplifyAppointmentTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "AUDIT_LOG" DROP CONSTRAINT IF EXISTS "FK_AUDIT_LOG_APPOINTMENT_AppointmentId";
                ALTER TABLE "COMMAND_RECEIPT" DROP CONSTRAINT IF EXISTS "FK_COMMAND_RECEIPT_APPOINTMENT_AppointmentId";
                ALTER TABLE "IN_APP_NOTIFICATION" DROP CONSTRAINT IF EXISTS "FK_IN_APP_NOTIFICATION_APPOINTMENT_AggregateId";
                ALTER TABLE "IN_APP_NOTIFICATION" DROP CONSTRAINT IF EXISTS "FK_IN_APP_NOTIFICATION_APPOINTMENT_AppointmentId";
                ALTER TABLE "VISIT" DROP CONSTRAINT IF EXISTS "FK_VISIT_APPOINTMENT_AppointmentId";

                -- Remove standalone reservation rows with no Appointment record and align Id to AppointmentId.
                DELETE FROM "APPOINTMENT" WHERE "Code" IS NULL;
                UPDATE "APPOINTMENT" SET "Id" = "AppointmentId" WHERE "Id" IS DISTINCT FROM "AppointmentId";

                -- Ensure every cancelled appointment has an AppointmentChange CANCEL row in AUDIT_LOG before dropping cancel columns.
                INSERT INTO "AUDIT_LOG" ("LegacyId", "Action", "ActorAccountId", "ActorKind", "AppointmentId", "OccurredAt", "OldChairId", "OldDentistId", "OldEnd", "OldStart", "Reason", "RequestKey", "StorageId", "AuditType")
                SELECT gen_random_uuid(), 'CANCEL',
                       COALESCE(a."CancelledBy", a."BookerAccountId", (SELECT "Id" FROM "USER_ACCOUNT" ORDER BY "Id" LIMIT 1)),
                       'STAFF', a."Id", COALESCE(a."CancelledAt", a."CreatedAt", now()),
                       a."ChairId", a."DentistId", a."EndAt", a."StartAt",
                       COALESCE(a."CancelReason", 'Đã hủy lịch hẹn'), gen_random_uuid(), gen_random_uuid(), 'AppointmentChange'
                FROM "APPOINTMENT" a
                WHERE a."Status" = 'Cancelled'
                  AND NOT EXISTS (
                      SELECT 1 FROM "AUDIT_LOG" l
                      WHERE l."AuditType" = 'AppointmentChange' AND l."AppointmentId" = a."Id" AND l."Action" = 'CANCEL'
                  );

                ALTER TABLE "APPOINTMENT" ADD COLUMN "BookingInfo" jsonb NOT NULL DEFAULT '{}'::jsonb;

                UPDATE "APPOINTMENT" SET "BookingInfo" = jsonb_build_object(
                    'BookerName', COALESCE("BookerName", ''),
                    'BookerContact', COALESCE(NULLIF("BookerPhone", ''), NULLIF("BookerEmail", '')),
                    'AttendeeBirthDate', to_char("AttendeeBirthDate", 'YYYY-MM-DD'),
                    'AttendeeRelation', COALESCE(NULLIF("AttendeeRelation", ''), 'Self'),
                    'ExceptionReason', "ExceptionReason"
                );

                ALTER TABLE "APPOINTMENT" ALTER COLUMN "BookingInfo" DROP DEFAULT;

                UPDATE "APPOINTMENT" SET
                    "AttendeeName" = COALESCE("AttendeeName", COALESCE("AttendeeLabel", '')),
                    "Kind" = COALESCE(NULLIF("Kind", ''), 'Standard'),
                    "Source" = COALESCE(NULLIF("Source", ''), 'App'),
                    "Status" = COALESCE(NULLIF("Status", ''), 'Confirmed'),
                    "Version" = COALESCE("Version", 1),
                    "CreatedAt" = COALESCE("CreatedAt", "StartAt");

                ALTER TABLE "APPOINTMENT"
                    ALTER COLUMN "Code" SET NOT NULL,
                    ALTER COLUMN "AttendeeName" SET NOT NULL,
                    ALTER COLUMN "Kind" SET NOT NULL,
                    ALTER COLUMN "Source" SET NOT NULL,
                    ALTER COLUMN "Status" SET NOT NULL,
                    ALTER COLUMN "Version" SET NOT NULL,
                    ALTER COLUMN "CreatedAt" SET NOT NULL;

                ALTER TABLE "APPOINTMENT" DROP CONSTRAINT IF EXISTS "FK_APPOINTMENT_USER_ACCOUNT_AssistantId";
                ALTER TABLE "APPOINTMENT" DROP CONSTRAINT IF EXISTS "FK_APPOINTMENT_USER_ACCOUNT_CancelledBy";
                ALTER TABLE "APPOINTMENT" DROP CONSTRAINT IF EXISTS "FK_APPOINTMENT_USER_ACCOUNT_PatientConsentConfirmedBy";
                ALTER TABLE "APPOINTMENT" DROP CONSTRAINT IF EXISTS "AK_APPOINTMENT_AppointmentId";
                ALTER TABLE "APPOINTMENT" DROP CONSTRAINT IF EXISTS "CK_APPOINTMENT_RANGE";

                DROP INDEX IF EXISTS "IX_APPOINTMENT_AssistantId";
                DROP INDEX IF EXISTS "IX_APPOINTMENT_BranchId";
                DROP INDEX IF EXISTS "IX_APPOINTMENT_CancelledBy";
                DROP INDEX IF EXISTS "IX_APPOINTMENT_PatientConsentConfirmedBy";
                DROP INDEX IF EXISTS "IX_APPOINTMENT_ReservationCode";

                ALTER TABLE "APPOINTMENT"
                    DROP COLUMN "AppointmentId",
                    DROP COLUMN "AssistantId",
                    DROP COLUMN "AttendeeBirthDate",
                    DROP COLUMN "AttendeeKey",
                    DROP COLUMN "AttendeeLabel",
                    DROP COLUMN "AttendeeRelation",
                    DROP COLUMN "BookerEmail",
                    DROP COLUMN "BookerName",
                    DROP COLUMN "BookerPhone",
                    DROP COLUMN "CancelReason",
                    DROP COLUMN "CancelledAt",
                    DROP COLUMN "CancelledBy",
                    DROP COLUMN "ConfirmedToAttend",
                    DROP COLUMN "ExceptionReason",
                    DROP COLUMN "PatientConsentConfirmedAt",
                    DROP COLUMN "PatientConsentConfirmedBy",
                    DROP COLUMN "ReservationCode",
                    DROP COLUMN "ReservationStatus",
                    DROP COLUMN "StorageStamp";

                ALTER TABLE "APPOINTMENT" ADD CONSTRAINT "CK_APPOINTMENT_RANGE" CHECK ("StartAt" < "EndAt");
                ALTER TABLE "APPOINTMENT" ADD CONSTRAINT "CK_APPOINTMENT_Status" CHECK ("Status" IN ('Confirmed','CheckedIn','InProgress','Completed','Cancelled','NoShow'));

                ALTER TABLE "AUDIT_LOG" ADD CONSTRAINT "FK_AUDIT_LOG_APPOINTMENT_AppointmentId"
                    FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("Id") ON DELETE RESTRICT;
                ALTER TABLE "COMMAND_RECEIPT" ADD CONSTRAINT "FK_COMMAND_RECEIPT_APPOINTMENT_AppointmentId"
                    FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("Id") ON DELETE RESTRICT;
                ALTER TABLE "IN_APP_NOTIFICATION" ADD CONSTRAINT "FK_IN_APP_NOTIFICATION_APPOINTMENT_AggregateId"
                    FOREIGN KEY ("AggregateId") REFERENCES "APPOINTMENT" ("Id") ON DELETE RESTRICT;
                ALTER TABLE "IN_APP_NOTIFICATION" ADD CONSTRAINT "FK_IN_APP_NOTIFICATION_APPOINTMENT_AppointmentId"
                    FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("Id") ON DELETE RESTRICT;
                ALTER TABLE "VISIT" ADD CONSTRAINT "FK_VISIT_APPOINTMENT_AppointmentId"
                    FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("Id") ON DELETE RESTRICT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "AUDIT_LOG" DROP CONSTRAINT IF EXISTS "FK_AUDIT_LOG_APPOINTMENT_AppointmentId";
                ALTER TABLE "COMMAND_RECEIPT" DROP CONSTRAINT IF EXISTS "FK_COMMAND_RECEIPT_APPOINTMENT_AppointmentId";
                ALTER TABLE "IN_APP_NOTIFICATION" DROP CONSTRAINT IF EXISTS "FK_IN_APP_NOTIFICATION_APPOINTMENT_AggregateId";
                ALTER TABLE "IN_APP_NOTIFICATION" DROP CONSTRAINT IF EXISTS "FK_IN_APP_NOTIFICATION_APPOINTMENT_AppointmentId";
                ALTER TABLE "VISIT" DROP CONSTRAINT IF EXISTS "FK_VISIT_APPOINTMENT_AppointmentId";

                ALTER TABLE "APPOINTMENT" DROP CONSTRAINT IF EXISTS "CK_APPOINTMENT_RANGE";
                ALTER TABLE "APPOINTMENT" DROP CONSTRAINT IF EXISTS "CK_APPOINTMENT_Status";

                ALTER TABLE "APPOINTMENT"
                    ALTER COLUMN "Code" DROP NOT NULL,
                    ALTER COLUMN "AttendeeName" DROP NOT NULL,
                    ALTER COLUMN "Kind" DROP NOT NULL,
                    ALTER COLUMN "Source" DROP NOT NULL,
                    ALTER COLUMN "Status" DROP NOT NULL,
                    ALTER COLUMN "Version" DROP NOT NULL,
                    ALTER COLUMN "CreatedAt" DROP NOT NULL;

                ALTER TABLE "APPOINTMENT"
                    ADD COLUMN "AppointmentId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                    ADD COLUMN "AssistantId" integer,
                    ADD COLUMN "AttendeeBirthDate" date,
                    ADD COLUMN "AttendeeKey" character varying(160),
                    ADD COLUMN "AttendeeLabel" character varying(160) NOT NULL DEFAULT '',
                    ADD COLUMN "AttendeeRelation" character varying(16),
                    ADD COLUMN "BookerEmail" character varying(254),
                    ADD COLUMN "BookerName" character varying(100),
                    ADD COLUMN "BookerPhone" character varying(24),
                    ADD COLUMN "CancelReason" character varying(500),
                    ADD COLUMN "CancelledAt" timestamp with time zone,
                    ADD COLUMN "CancelledBy" integer,
                    ADD COLUMN "ConfirmedToAttend" character varying(16) DEFAULT 'Pending',
                    ADD COLUMN "ExceptionReason" character varying(500),
                    ADD COLUMN "PatientConsentConfirmedAt" timestamp with time zone,
                    ADD COLUMN "PatientConsentConfirmedBy" integer,
                    ADD COLUMN "ReservationCode" character varying(40) NOT NULL DEFAULT '',
                    ADD COLUMN "ReservationStatus" character varying(16) NOT NULL DEFAULT '',
                    ADD COLUMN "StorageStamp" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

                UPDATE "APPOINTMENT" a SET
                    "AppointmentId" = a."Id",
                    "StorageStamp" = gen_random_uuid(),
                    "ReservationCode" = a."Code",
                    "ReservationStatus" = CASE WHEN a."Status" IN ('CheckedIn', 'InProgress', 'Completed') THEN 'Confirmed' ELSE a."Status" END,
                    "AttendeeKey" = CASE WHEN a."PatientId" IS NOT NULL THEN 'patient:' || a."PatientId"::text ELSE NULL END,
                    "AttendeeLabel" = a."AttendeeName",
                    "BookerName" = a."BookingInfo"->>'BookerName',
                    "BookerPhone" = CASE WHEN COALESCE(a."BookingInfo"->>'BookerContact', '') <> '' AND position('@' in (a."BookingInfo"->>'BookerContact')) = 0 THEN a."BookingInfo"->>'BookerContact' ELSE NULL END,
                    "BookerEmail" = CASE WHEN position('@' in COALESCE(a."BookingInfo"->>'BookerContact', '')) > 0 THEN a."BookingInfo"->>'BookerContact' ELSE NULL END,
                    "AttendeeBirthDate" = NULLIF(a."BookingInfo"->>'AttendeeBirthDate', '')::date,
                    "AttendeeRelation" = COALESCE(NULLIF(a."BookingInfo"->>'AttendeeRelation', ''), 'Self'),
                    "ExceptionReason" = a."BookingInfo"->>'ExceptionReason',
                    "ConfirmedToAttend" = 'Pending',
                    "CancelledAt" = (SELECT l."OccurredAt" FROM "AUDIT_LOG" l WHERE l."AuditType" = 'AppointmentChange' AND l."Action" = 'CANCEL' AND l."AppointmentId" = a."Id" ORDER BY l."OccurredAt" DESC LIMIT 1),
                    "CancelledBy" = (SELECT l."ActorAccountId" FROM "AUDIT_LOG" l WHERE l."AuditType" = 'AppointmentChange' AND l."Action" = 'CANCEL' AND l."AppointmentId" = a."Id" ORDER BY l."OccurredAt" DESC LIMIT 1),
                    "CancelReason" = (SELECT l."Reason" FROM "AUDIT_LOG" l WHERE l."AuditType" = 'AppointmentChange' AND l."Action" = 'CANCEL' AND l."AppointmentId" = a."Id" ORDER BY l."OccurredAt" DESC LIMIT 1);

                ALTER TABLE "APPOINTMENT"
                    ALTER COLUMN "AppointmentId" DROP DEFAULT,
                    ALTER COLUMN "AttendeeLabel" DROP DEFAULT,
                    ALTER COLUMN "ReservationCode" DROP DEFAULT,
                    ALTER COLUMN "ReservationStatus" DROP DEFAULT,
                    ALTER COLUMN "StorageStamp" DROP DEFAULT;

                ALTER TABLE "APPOINTMENT" DROP COLUMN "BookingInfo";

                ALTER TABLE "APPOINTMENT" ADD CONSTRAINT "AK_APPOINTMENT_AppointmentId" UNIQUE ("AppointmentId");
                ALTER TABLE "APPOINTMENT" ADD CONSTRAINT "CK_APPOINTMENT_RANGE" CHECK ("StartAt" < "EndAt" AND ("AssistantId" IS NULL OR "AssistantId" <> "DentistId"));

                CREATE INDEX "IX_APPOINTMENT_AssistantId" ON "APPOINTMENT" ("AssistantId");
                CREATE INDEX "IX_APPOINTMENT_BranchId" ON "APPOINTMENT" ("BranchId");
                CREATE INDEX "IX_APPOINTMENT_CancelledBy" ON "APPOINTMENT" ("CancelledBy");
                CREATE INDEX "IX_APPOINTMENT_PatientConsentConfirmedBy" ON "APPOINTMENT" ("PatientConsentConfirmedBy");
                CREATE UNIQUE INDEX "IX_APPOINTMENT_ReservationCode" ON "APPOINTMENT" ("ReservationCode");

                ALTER TABLE "APPOINTMENT" ADD CONSTRAINT "FK_APPOINTMENT_USER_ACCOUNT_AssistantId"
                    FOREIGN KEY ("AssistantId") REFERENCES "USER_ACCOUNT" ("EmployeeId") ON DELETE RESTRICT;
                ALTER TABLE "APPOINTMENT" ADD CONSTRAINT "FK_APPOINTMENT_USER_ACCOUNT_CancelledBy"
                    FOREIGN KEY ("CancelledBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT;
                ALTER TABLE "APPOINTMENT" ADD CONSTRAINT "FK_APPOINTMENT_USER_ACCOUNT_PatientConsentConfirmedBy"
                    FOREIGN KEY ("PatientConsentConfirmedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT;

                ALTER TABLE "AUDIT_LOG" ADD CONSTRAINT "FK_AUDIT_LOG_APPOINTMENT_AppointmentId"
                    FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("AppointmentId") ON DELETE RESTRICT;
                ALTER TABLE "COMMAND_RECEIPT" ADD CONSTRAINT "FK_COMMAND_RECEIPT_APPOINTMENT_AppointmentId"
                    FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("AppointmentId") ON DELETE RESTRICT;
                ALTER TABLE "IN_APP_NOTIFICATION" ADD CONSTRAINT "FK_IN_APP_NOTIFICATION_APPOINTMENT_AggregateId"
                    FOREIGN KEY ("AggregateId") REFERENCES "APPOINTMENT" ("AppointmentId") ON DELETE RESTRICT;
                ALTER TABLE "IN_APP_NOTIFICATION" ADD CONSTRAINT "FK_IN_APP_NOTIFICATION_APPOINTMENT_AppointmentId"
                    FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("AppointmentId") ON DELETE RESTRICT;
                ALTER TABLE "VISIT" ADD CONSTRAINT "FK_VISIT_APPOINTMENT_AppointmentId"
                    FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("AppointmentId") ON DELETE RESTRICT;
                """);
        }
    }
}
