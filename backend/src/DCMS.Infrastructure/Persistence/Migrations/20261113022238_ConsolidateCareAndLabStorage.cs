using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations;

public partial class ConsolidateCareAndLabStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET LOCAL TimeZone = 'UTC';

            ALTER TABLE "CARE_TASK"
                ADD COLUMN "StorageStamp" uuid NOT NULL DEFAULT gen_random_uuid(),
                ADD COLUMN "ContactAttempts" jsonb NOT NULL DEFAULT '[]'::jsonb,
                ADD COLUMN "Escalations" jsonb NOT NULL DEFAULT '[]'::jsonb;
            ALTER TABLE "LAB_ORDER"
                ADD COLUMN "StorageStamp" uuid NOT NULL DEFAULT gen_random_uuid(),
                ADD COLUMN "StatusLogs" jsonb NOT NULL DEFAULT '[]'::jsonb;
            UPDATE "CARE_TASK" p SET "ContactAttempts" = COALESCE((SELECT jsonb_agg(to_jsonb(s) ORDER BY s."OccurredAt", s."Id")
                FROM "CARE_CONTACT_ATTEMPT" s WHERE s."TaskId" = p."Id"), '[]'::jsonb);
            UPDATE "CARE_TASK" p SET "Escalations" = COALESCE((SELECT jsonb_agg(to_jsonb(s) ORDER BY s."CreatedAt", s."Id")
                FROM "CARE_ESCALATION" s WHERE s."TaskId" = p."Id"), '[]'::jsonb);
            UPDATE "LAB_ORDER" p SET "StatusLogs" = COALESCE((SELECT jsonb_agg(to_jsonb(s) ORDER BY s."ChangedAt", s."Id")
                FROM "LAB_ORDER_STATUS_LOG" s WHERE s."LabOrderId" = p."Id"), '[]'::jsonb);

            DO $verify$
            BEGIN
                IF (SELECT count(*) FROM "CARE_CONTACT_ATTEMPT") <> (SELECT COALESCE(sum(jsonb_array_length("ContactAttempts")), 0) FROM "CARE_TASK")
                    OR (SELECT count(*) FROM "CARE_ESCALATION") <> (SELECT COALESCE(sum(jsonb_array_length("Escalations")), 0) FROM "CARE_TASK")
                    OR (SELECT count(*) FROM "LAB_ORDER_STATUS_LOG") <> (SELECT COALESCE(sum(jsonb_array_length("StatusLogs")), 0) FROM "LAB_ORDER") THEN
                    RAISE EXCEPTION 'Care and lab storage conversion row counts do not match';
                END IF;
            END $verify$;

            DROP TABLE "CARE_CONTACT_ATTEMPT";
            DROP TABLE "CARE_ESCALATION";
            DROP TABLE "LAB_ORDER_STATUS_LOG";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET LOCAL TimeZone = 'UTC';

            CREATE TABLE "CARE_CONTACT_ATTEMPT" (
                "Id" uuid NOT NULL,
                "ActorId" integer NOT NULL,
                "Channel" character varying(32) NOT NULL,
                "NextAction" character varying(1000),
                "OccurredAt" timestamp with time zone NOT NULL,
                "RebookedAppointmentId" uuid,
                "Result" character varying(40) NOT NULL,
                "TaskId" uuid NOT NULL,
                CONSTRAINT "PK_CARE_CONTACT_ATTEMPT" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_CARE_CONTACT_ATTEMPT_CARE_TASK_TaskId" FOREIGN KEY ("TaskId") REFERENCES "CARE_TASK" ("Id") ON DELETE RESTRICT
            );
            CREATE TABLE "CARE_ESCALATION" (
                "Id" uuid NOT NULL,
                "AcknowledgedAt" timestamp with time zone,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" integer NOT NULL,
                "Reason" character varying(1000) NOT NULL,
                "Resolution" character varying(4000),
                "ResolvedAt" timestamp with time zone,
                "Status" character varying(16) NOT NULL,
                "TargetClinicianAccountId" integer NOT NULL,
                "TaskId" uuid NOT NULL,
                "Version" integer NOT NULL,
                CONSTRAINT "PK_CARE_ESCALATION" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_CARE_ESCALATION_CARE_TASK_TaskId" FOREIGN KEY ("TaskId") REFERENCES "CARE_TASK" ("Id") ON DELETE RESTRICT
            );
            CREATE TABLE "LAB_ORDER_STATUS_LOG" (
                "Id" uuid NOT NULL,
                "ChangedAt" timestamp with time zone NOT NULL,
                "ChangedBy" integer NOT NULL,
                "FromStatus" character varying(20),
                "LabOrderId" uuid NOT NULL,
                "Note" character varying(300),
                "ToStatus" character varying(20) NOT NULL,
                CONSTRAINT "PK_LAB_ORDER_STATUS_LOG" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_LAB_ORDER_STATUS_LOG_LAB_ORDER_LabOrderId" FOREIGN KEY ("LabOrderId") REFERENCES "LAB_ORDER" ("Id") ON DELETE CASCADE
            );

            INSERT INTO "CARE_CONTACT_ATTEMPT"
            SELECT restored.* FROM "CARE_TASK" p CROSS JOIN LATERAL jsonb_populate_recordset(NULL::"CARE_CONTACT_ATTEMPT", p."ContactAttempts") restored;
            INSERT INTO "CARE_ESCALATION"
            SELECT restored.* FROM "CARE_TASK" p CROSS JOIN LATERAL jsonb_populate_recordset(NULL::"CARE_ESCALATION", p."Escalations") restored;
            INSERT INTO "LAB_ORDER_STATUS_LOG"
            SELECT restored.* FROM "LAB_ORDER" p CROSS JOIN LATERAL jsonb_populate_recordset(NULL::"LAB_ORDER_STATUS_LOG", p."StatusLogs") restored;
            ALTER TABLE "CARE_TASK" DROP COLUMN "StorageStamp", DROP COLUMN "ContactAttempts", DROP COLUMN "Escalations";
            ALTER TABLE "LAB_ORDER" DROP COLUMN "StorageStamp", DROP COLUMN "StatusLogs";
            CREATE INDEX "IX_CARE_CONTACT_ATTEMPT_TaskId_OccurredAt" ON "CARE_CONTACT_ATTEMPT" ("TaskId", "OccurredAt");
            CREATE INDEX "IX_CARE_ESCALATION_TaskId_CreatedAt" ON "CARE_ESCALATION" ("TaskId", "CreatedAt");
            CREATE INDEX "IX_LAB_ORDER_STATUS_LOG_LabOrderId_ChangedAt" ON "LAB_ORDER_STATUS_LOG" ("LabOrderId", "ChangedAt");
            """);
    }
}
