using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations;

public partial class ConsolidateTreatmentStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET LOCAL TimeZone = 'UTC';

            ALTER TABLE "TREATMENT_DOSSIER"
                ADD COLUMN "StorageStamp" uuid NOT NULL DEFAULT gen_random_uuid(),
                ADD COLUMN "RevisionDrafts" jsonb NOT NULL DEFAULT '[]'::jsonb,
                ADD COLUMN "SheetDrafts" jsonb NOT NULL DEFAULT '[]'::jsonb;
            ALTER TABLE "TREATMENT_PLAN_ITEM"
                ADD COLUMN "StorageStamp" uuid NOT NULL DEFAULT gen_random_uuid(),
                ADD COLUMN "Steps" jsonb NOT NULL DEFAULT '[]'::jsonb;
            ALTER TABLE "TREATMENT_TEMPLATE"
                ADD COLUMN "StorageStamp" uuid NOT NULL DEFAULT gen_random_uuid(),
                ADD COLUMN "Items" jsonb NOT NULL DEFAULT '[]'::jsonb,
                ADD COLUMN "Versions" jsonb NOT NULL DEFAULT '[]'::jsonb;
            UPDATE "TREATMENT_DOSSIER" p SET "SheetDrafts" = COALESCE((SELECT jsonb_agg(
                to_jsonb(s) || jsonb_build_object('PayloadJson', s."PayloadJson"::text)
                ORDER BY s."UpdatedAt", s."Id") FROM "TREATMENT_SHEET_DRAFT" s WHERE s."DossierId" = p."Id"), '[]'::jsonb);
            UPDATE "TREATMENT_DOSSIER" p SET "RevisionDrafts" = COALESCE((SELECT jsonb_agg(
                to_jsonb(s) || jsonb_build_object('PayloadJson', s."PayloadJson"::text)
                ORDER BY s."UpdatedAt", s."Id") FROM "TREATMENT_REVISION_DRAFT" s WHERE s."DossierId" = p."Id"), '[]'::jsonb);
            UPDATE "TREATMENT_PLAN_ITEM" p SET "Steps" = COALESCE((SELECT jsonb_agg(
                to_jsonb(s) || jsonb_build_object('PredecessorsJson', s."PredecessorsJson"::text)
                ORDER BY s."PlanVersion", s."Id") FROM "TREATMENT_STEP" s WHERE s."ItemId" = p."Id"), '[]'::jsonb);
            UPDATE "TREATMENT_TEMPLATE" p SET "Items" = COALESCE((SELECT jsonb_agg(
                to_jsonb(s) || jsonb_build_object('TeethJson', s."TeethJson"::text)
                ORDER BY s."Position", s."Id") FROM "TREATMENT_TEMPLATE_ITEM" s WHERE s."TemplateId" = p."Id"), '[]'::jsonb);
            UPDATE "TREATMENT_TEMPLATE" p SET "Versions" = COALESCE((SELECT jsonb_agg(
                to_jsonb(s) || jsonb_build_object('StepsJson', s."StepsJson"::text)
                ORDER BY s."Number", s."Id") FROM "TREATMENT_TEMPLATE_VERSION" s WHERE s."TemplateId" = p."Id"), '[]'::jsonb);

            ALTER TABLE "CLINICAL_ACTIVITY" DROP CONSTRAINT "CK_CLINICAL_ACTIVITY_VALUES";
            ALTER TABLE "CLINICAL_ACTIVITY"
                ADD COLUMN "RecordId" uuid,
                ADD COLUMN "StorageStamp" uuid NOT NULL DEFAULT gen_random_uuid(),
                ADD COLUMN "ProcessKind" character varying(34),
                ADD COLUMN "SourceActivityId" uuid,
                ADD COLUMN "Content" character varying(4000),
                ADD COLUMN "Response" character varying(4000),
                ADD COLUMN "ActorId" integer,
                ADD COLUMN "RecordedAt" timestamp with time zone,
                ADD COLUMN "SourceItemId" uuid,
                ADD COLUMN "Complaint" character varying(4000),
                ADD COLUMN "Findings" character varying(4000),
                ADD COLUMN "Coverage" character varying(24),
                ADD COLUMN "CoveredComponent" character varying(500),
                ADD COLUMN "Exclusions" character varying(1000),
                ADD COLUMN "Policy" character varying(160),
                ADD COLUMN "PolicyVersion" character varying(40),
                ADD COLUMN "Basis" character varying(1000),
                ADD COLUMN "NextAction" character varying(2000),
                ADD COLUMN "DentistAccountId" integer,
                ADD COLUMN "SourceVisitId" uuid,
                ADD COLUMN "SourceCompletionEventId" uuid,
                ADD COLUMN "Purpose" character varying(40),
                ADD COLUMN "Reason" character varying(2000),
                ADD COLUMN "Guidance" character varying(4000),
                ADD COLUMN "EarliestOn" date,
                ADD COLUMN "TargetOn" date,
                ADD COLUMN "LatestOn" date,
                ADD COLUMN "DurationMinutes" integer,
                ADD COLUMN "DentistId" integer,
                ADD COLUMN "Capability" character varying(160),
                ADD COLUMN "Status" character varying(20),
                ADD COLUMN "ActiveAppointmentId" uuid,
                ADD COLUMN "CreatedBy" integer,
                ADD COLUMN "UpdatedAt" timestamp with time zone,
                ALTER COLUMN "BranchId" DROP NOT NULL,
                ALTER COLUMN "ChargingBasis" DROP NOT NULL,
                ALTER COLUMN "CreatedAt" DROP NOT NULL,
                ALTER COLUMN "DiscountSnapshotVnd" DROP NOT NULL,
                ALTER COLUMN "FeeStatus" DROP NOT NULL,
                ALTER COLUMN "IsPlanTransfer" DROP NOT NULL,
                ALTER COLUMN "Outcome" DROP NOT NULL,
                ALTER COLUMN "PatientId" DROP NOT NULL,
                ALTER COLUMN "PerformedAt" DROP NOT NULL,
                ALTER COLUMN "PerformerId" DROP NOT NULL,
                ALTER COLUMN "PriceSnapshotVnd" DROP NOT NULL,
                ALTER COLUMN "Quantity" DROP NOT NULL,
                ALTER COLUMN "ServiceId" DROP NOT NULL,
                ALTER COLUMN "SourceKind" DROP NOT NULL,
                ALTER COLUMN "State" DROP NOT NULL,
                ALTER COLUMN "Unit" DROP NOT NULL,
                ALTER COLUMN "Version" DROP NOT NULL;
            UPDATE "CLINICAL_ACTIVITY" SET "RecordId" = "Id", "ProcessKind" = 'ClinicalActivity';
            INSERT INTO "CLINICAL_ACTIVITY" ("Id", "RecordId", "ProcessKind", "Basis", "BranchId", "Complaint", "Coverage", "CoveredComponent", "DentistAccountId", "Exclusions", "Findings", "NextAction", "Outcome", "PatientId", "Policy", "PolicyVersion", "RecordedAt", "SourceActivityId", "SourceItemId", "Version", "VisitId")
            SELECT gen_random_uuid(), s."Id", 'ClinicalReview', s."Basis", s."BranchId", s."Complaint", s."Coverage", s."CoveredComponent", s."DentistAccountId", s."Exclusions", s."Findings", s."NextAction", s."Outcome", s."PatientId", s."Policy", s."PolicyVersion", s."RecordedAt", s."SourceActivityId", s."SourceItemId", s."Version", s."VisitId" FROM "CLINICAL_REVIEW" s;
            INSERT INTO "CLINICAL_ACTIVITY" ("Id", "RecordId", "ProcessKind", "ActorId", "BranchId", "Content", "PatientId", "RecordedAt", "Response", "ReviewId", "SourceActivityId", "VisitId")
            SELECT gen_random_uuid(), s."Id", 'ClinicalCareNote', s."ActorId", s."BranchId", s."Content", s."PatientId", s."RecordedAt", s."Response", s."ReviewId", s."SourceActivityId", s."VisitId" FROM "CLINICAL_CARE_NOTE" s;
            INSERT INTO "CLINICAL_ACTIVITY" ("Id", "RecordId", "ProcessKind", "ActiveAppointmentId", "BranchId", "Capability", "CreatedAt", "CreatedBy", "DentistId", "DossierId", "DurationMinutes", "EarliestOn", "Guidance", "ItemId", "LatestOn", "PatientId", "PlanVersion", "Purpose", "Reason", "SourceActivityId", "SourceCompletionEventId", "SourceVisitId", "Status", "StepId", "TargetOn", "UpdatedAt", "Version")
            SELECT gen_random_uuid(), s."Id", 'FollowUpInstruction', s."ActiveAppointmentId", s."BranchId", s."Capability", s."CreatedAt", s."CreatedBy", s."DentistId", s."DossierId", s."DurationMinutes", s."EarliestOn", s."Guidance", s."ItemId", s."LatestOn", s."PatientId", s."PlanVersion", s."Purpose", s."Reason", s."SourceActivityId", s."SourceCompletionEventId", s."SourceVisitId", s."Status", s."StepId", s."TargetOn", s."UpdatedAt", s."Version" FROM "FOLLOW_UP_INSTRUCTION" s;
            ALTER TABLE "CLINICAL_ACTIVITY" ALTER COLUMN "RecordId" SET NOT NULL, ALTER COLUMN "ProcessKind" SET NOT NULL;

            ALTER TABLE "AUDIT_LOG"
                ADD COLUMN "DossierId" uuid,
                ADD COLUMN "Kind" character varying(80),
                ADD COLUMN "EventActorId" integer,
                ADD COLUMN "DossierVersion" integer,
                ADD COLUMN "AggregateId" uuid,
                ADD COLUMN "StableKey" character varying(160);
            INSERT INTO "AUDIT_LOG" ("StorageId", "AuditType", "LegacyId", "EventActorId", "DossierId", "DossierVersion", "Kind", "OccurredAt", "PayloadJson")
            SELECT gen_random_uuid(), 'TreatmentEvent', s."Id", s."ActorId", s."DossierId", s."DossierVersion", s."Kind", s."OccurredAt", s."PayloadJson" FROM "TREATMENT_EVENT" s ORDER BY s."OccurredAt", s."Id";
            INSERT INTO "AUDIT_LOG" ("StorageId", "AuditType", "LegacyId", "EventActorId", "AggregateId", "Kind", "OccurredAt", "PayloadJson", "StableKey")
            SELECT gen_random_uuid(), 'TreatmentProgressionEvent', s."Id", s."ActorId", s."AggregateId", s."Kind", s."OccurredAt", s."PayloadJson", s."StableKey" FROM "TREATMENT_PROGRESSION_EVENT" s ORDER BY s."OccurredAt", s."Id";

            DO $verify$
            BEGIN
                IF (SELECT count(*) FROM "TREATMENT_SHEET_DRAFT") <> (SELECT COALESCE(sum(jsonb_array_length("SheetDrafts")), 0) FROM "TREATMENT_DOSSIER")
                    OR (SELECT count(*) FROM "TREATMENT_REVISION_DRAFT") <> (SELECT COALESCE(sum(jsonb_array_length("RevisionDrafts")), 0) FROM "TREATMENT_DOSSIER")
                    OR (SELECT count(*) FROM "TREATMENT_STEP") <> (SELECT COALESCE(sum(jsonb_array_length("Steps")), 0) FROM "TREATMENT_PLAN_ITEM")
                    OR (SELECT count(*) FROM "TREATMENT_TEMPLATE_ITEM") <> (SELECT COALESCE(sum(jsonb_array_length("Items")), 0) FROM "TREATMENT_TEMPLATE")
                    OR (SELECT count(*) FROM "TREATMENT_TEMPLATE_VERSION") <> (SELECT COALESCE(sum(jsonb_array_length("Versions")), 0) FROM "TREATMENT_TEMPLATE")
                    OR (SELECT count(*) FROM "CLINICAL_REVIEW") <> (SELECT count(*) FROM "CLINICAL_ACTIVITY" WHERE "ProcessKind" = 'ClinicalReview')
                    OR (SELECT count(*) FROM "CLINICAL_CARE_NOTE") <> (SELECT count(*) FROM "CLINICAL_ACTIVITY" WHERE "ProcessKind" = 'ClinicalCareNote')
                    OR (SELECT count(*) FROM "FOLLOW_UP_INSTRUCTION") <> (SELECT count(*) FROM "CLINICAL_ACTIVITY" WHERE "ProcessKind" = 'FollowUpInstruction')
                    OR (SELECT count(*) FROM "TREATMENT_EVENT") <> (SELECT count(*) FROM "AUDIT_LOG" WHERE "AuditType" = 'TreatmentEvent')
                    OR (SELECT count(*) FROM "TREATMENT_PROGRESSION_EVENT") <> (SELECT count(*) FROM "AUDIT_LOG" WHERE "AuditType" = 'TreatmentProgressionEvent') THEN
                    RAISE EXCEPTION 'Treatment storage conversion row counts do not match';
                END IF;
            END $verify$;

            DROP TABLE "CLINICAL_CARE_NOTE";
            DROP TABLE "CLINICAL_REVIEW";
            DROP TABLE "FOLLOW_UP_INSTRUCTION";
            DROP TABLE "TREATMENT_EVENT";
            DROP TABLE "TREATMENT_PROGRESSION_EVENT";
            DROP TABLE "TREATMENT_REVISION_DRAFT";
            DROP TABLE "TREATMENT_SHEET_DRAFT";
            DROP TABLE "TREATMENT_STEP";
            DROP TABLE "TREATMENT_TEMPLATE_ITEM";
            DROP TABLE "TREATMENT_TEMPLATE_VERSION";

            ALTER TABLE "CLINICAL_ACTIVITY" ADD CONSTRAINT "CK_CLINICAL_ACTIVITY_VALUES" CHECK ("ProcessKind" <> 'ClinicalActivity' OR ("Quantity" > 0 AND "PriceSnapshotVnd" >= 0));
            ALTER TABLE "CLINICAL_ACTIVITY" ADD CONSTRAINT "CK_FOLLOW_UP_DURATION" CHECK ("ProcessKind" <> 'FollowUpInstruction' OR "DurationMinutes" BETWEEN 15 AND 480 AND "DurationMinutes" % 15 = 0);
            ALTER TABLE "CLINICAL_ACTIVITY" ADD CONSTRAINT "CK_FOLLOW_UP_WINDOW" CHECK ("ProcessKind" <> 'FollowUpInstruction' OR ("EarliestOn" <= "TargetOn" AND "TargetOn" <= "LatestOn"));
            ALTER TABLE "AUDIT_LOG" ADD CONSTRAINT "FK_AUDIT_LOG_TREATMENT_DOSSIER_DossierId" FOREIGN KEY ("DossierId") REFERENCES "TREATMENT_DOSSIER" ("Id") ON DELETE RESTRICT;
            CREATE INDEX "IX_AUDIT_LOG_AggregateId_OccurredAt" ON "AUDIT_LOG" ("AggregateId", "OccurredAt");
            CREATE INDEX "IX_AUDIT_LOG_DossierId_OccurredAt" ON "AUDIT_LOG" ("DossierId", "OccurredAt");
            CREATE UNIQUE INDEX "IX_AUDIT_LOG_StableKey" ON "AUDIT_LOG" ("StableKey") WHERE "AuditType" = 'TreatmentProgressionEvent' AND "StableKey" IS NOT NULL;
            CREATE UNIQUE INDEX "IX_CLINICAL_ACTIVITY_ActiveAppointmentId" ON "CLINICAL_ACTIVITY" ("ActiveAppointmentId") WHERE "ActiveAppointmentId" IS NOT NULL;
            CREATE INDEX "IX_CLINICAL_ACTIVITY_PatientId_Status" ON "CLINICAL_ACTIVITY" ("PatientId", "Status");
            CREATE UNIQUE INDEX "IX_CLINICAL_ACTIVITY_ProcessKind_RecordId" ON "CLINICAL_ACTIVITY" ("ProcessKind", "RecordId");
            CREATE INDEX "IX_CLINICAL_ACTIVITY_SourceActivityId_SourceItemId" ON "CLINICAL_ACTIVITY" ("SourceActivityId", "SourceItemId");
            CREATE INDEX "IX_CLINICAL_ACTIVITY_VisitId_RecordedAt" ON "CLINICAL_ACTIVITY" ("VisitId", "RecordedAt");
            CREATE UNIQUE INDEX "IX_FOLLOW_UP_INSTRUCTION_ActiveItemOnly" ON "CLINICAL_ACTIVITY" ("DossierId", "ItemId") WHERE "DossierId" IS NOT NULL AND "ItemId" IS NOT NULL AND "StepId" IS NULL AND "Status" IN ('NeedsBooking','Booked');
            CREATE UNIQUE INDEX "IX_FOLLOW_UP_INSTRUCTION_ActiveStep" ON "CLINICAL_ACTIVITY" ("StepId") WHERE "StepId" IS NOT NULL AND "Status" IN ('NeedsBooking','Booked');
            CREATE UNIQUE INDEX "IX_FOLLOW_UP_INSTRUCTION_Step_Completion" ON "CLINICAL_ACTIVITY" ("StepId", "SourceCompletionEventId") WHERE "StepId" IS NOT NULL AND "SourceCompletionEventId" IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET LOCAL TimeZone = 'UTC';

            DO $guard$
            BEGIN
                IF EXISTS (SELECT 1 FROM "CLINICAL_ACTIVITY" WHERE "ProcessKind" = 'ClinicalActivity' AND "Id" <> "RecordId") THEN
                    RAISE EXCEPTION 'Clinical activities must keep their storage key equal to the activity id before the treatment tables are restored';
                END IF;
            END $guard$;

            CREATE TABLE "CLINICAL_CARE_NOTE" (
                "Id" uuid NOT NULL,
                "ActorId" integer NOT NULL,
                "BranchId" integer NOT NULL,
                "Content" character varying(4000) NOT NULL,
                "PatientId" integer NOT NULL,
                "RecordedAt" timestamp with time zone NOT NULL,
                "Response" character varying(4000),
                "ReviewId" uuid,
                "SourceActivityId" uuid,
                "VisitId" uuid NOT NULL,
                CONSTRAINT "PK_CLINICAL_CARE_NOTE" PRIMARY KEY ("Id")
            );
            CREATE TABLE "CLINICAL_REVIEW" (
                "Id" uuid NOT NULL,
                "Basis" character varying(1000),
                "BranchId" integer NOT NULL,
                "Complaint" character varying(4000) NOT NULL,
                "Coverage" character varying(24) NOT NULL,
                "CoveredComponent" character varying(500),
                "DentistAccountId" integer NOT NULL,
                "Exclusions" character varying(1000),
                "Findings" character varying(4000) NOT NULL,
                "NextAction" character varying(2000),
                "Outcome" character varying(32) NOT NULL,
                "PatientId" integer NOT NULL,
                "Policy" character varying(160),
                "PolicyVersion" character varying(40),
                "RecordedAt" timestamp with time zone NOT NULL,
                "SourceActivityId" uuid,
                "SourceItemId" uuid,
                "Version" integer NOT NULL,
                "VisitId" uuid NOT NULL,
                CONSTRAINT "PK_CLINICAL_REVIEW" PRIMARY KEY ("Id")
            );
            CREATE TABLE "FOLLOW_UP_INSTRUCTION" (
                "Id" uuid NOT NULL,
                "ActiveAppointmentId" uuid,
                "BranchId" integer NOT NULL,
                "Capability" character varying(160),
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" integer NOT NULL,
                "DentistId" integer,
                "DossierId" uuid,
                "DurationMinutes" integer NOT NULL,
                "EarliestOn" date NOT NULL,
                "Guidance" character varying(4000),
                "ItemId" uuid,
                "LatestOn" date NOT NULL,
                "PatientId" integer NOT NULL,
                "PlanVersion" integer,
                "Purpose" character varying(40) NOT NULL,
                "Reason" character varying(2000) NOT NULL,
                "SourceActivityId" uuid,
                "SourceCompletionEventId" uuid,
                "SourceVisitId" uuid,
                "Status" character varying(20) NOT NULL,
                "StepId" uuid,
                "TargetOn" date NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "Version" integer NOT NULL,
                CONSTRAINT "PK_FOLLOW_UP_INSTRUCTION" PRIMARY KEY ("Id"),
                CONSTRAINT "CK_FOLLOW_UP_DURATION" CHECK ("DurationMinutes" BETWEEN 15 AND 480 AND "DurationMinutes" % 15 = 0),
                CONSTRAINT "CK_FOLLOW_UP_WINDOW" CHECK ("EarliestOn" <= "TargetOn" AND "TargetOn" <= "LatestOn"),
                CONSTRAINT "FK_FOLLOW_UP_INSTRUCTION_PATIENT_PROFILE_PatientId" FOREIGN KEY ("PatientId") REFERENCES "PATIENT_PROFILE" ("Id") ON DELETE RESTRICT
            );
            CREATE TABLE "TREATMENT_EVENT" (
                "Id" uuid NOT NULL,
                "ActorId" integer NOT NULL,
                "DossierId" uuid NOT NULL,
                "DossierVersion" integer NOT NULL,
                "Kind" character varying(40) NOT NULL,
                "OccurredAt" timestamp with time zone NOT NULL,
                "PayloadJson" jsonb NOT NULL,
                CONSTRAINT "PK_TREATMENT_EVENT" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_TREATMENT_EVENT_TREATMENT_DOSSIER_DossierId" FOREIGN KEY ("DossierId") REFERENCES "TREATMENT_DOSSIER" ("Id") ON DELETE RESTRICT
            );
            CREATE TABLE "TREATMENT_PROGRESSION_EVENT" (
                "Id" uuid NOT NULL,
                "ActorId" integer NOT NULL,
                "AggregateId" uuid NOT NULL,
                "Kind" character varying(80) NOT NULL,
                "OccurredAt" timestamp with time zone NOT NULL,
                "PayloadJson" jsonb NOT NULL,
                "StableKey" character varying(160),
                CONSTRAINT "PK_TREATMENT_PROGRESSION_EVENT" PRIMARY KEY ("Id")
            );
            CREATE TABLE "TREATMENT_REVISION_DRAFT" (
                "Id" uuid NOT NULL,
                "ActorId" integer NOT NULL,
                "BasePlanVersion" integer NOT NULL,
                "DossierId" uuid NOT NULL,
                "ItemId" uuid NOT NULL,
                "PayloadJson" jsonb NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "Version" integer NOT NULL,
                CONSTRAINT "PK_TREATMENT_REVISION_DRAFT" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_TREATMENT_REVISION_DRAFT_TREATMENT_DOSSIER_DossierId" FOREIGN KEY ("DossierId") REFERENCES "TREATMENT_DOSSIER" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_TREATMENT_REVISION_DRAFT_TREATMENT_PLAN_ITEM_ItemId" FOREIGN KEY ("ItemId") REFERENCES "TREATMENT_PLAN_ITEM" ("Id") ON DELETE RESTRICT
            );
            CREATE TABLE "TREATMENT_SHEET_DRAFT" (
                "Id" uuid NOT NULL,
                "ActorId" integer NOT NULL,
                "DossierId" uuid NOT NULL,
                "Kind" character varying(12) NOT NULL,
                "LastFingerprint" character varying(64) NOT NULL,
                "LastRequestKey" uuid NOT NULL,
                "PayloadJson" jsonb NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "Version" integer NOT NULL,
                "VisitId" uuid,
                CONSTRAINT "PK_TREATMENT_SHEET_DRAFT" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_TREATMENT_SHEET_DRAFT_TREATMENT_DOSSIER_DossierId" FOREIGN KEY ("DossierId") REFERENCES "TREATMENT_DOSSIER" ("Id") ON DELETE RESTRICT
            );
            CREATE TABLE "TREATMENT_STEP" (
                "Id" uuid NOT NULL,
                "Capability" character varying(160),
                "ChargingCoverage" character varying(40) NOT NULL,
                "CompletedAt" timestamp with time zone,
                "CompletionEventId" uuid,
                "DossierId" uuid NOT NULL,
                "EarliestOffsetDays" integer,
                "EstimatedMaxMinutes" integer,
                "EstimatedMinMinutes" integer,
                "EstimatedMinutes" integer NOT NULL,
                "HoldEffectiveAt" timestamp with time zone,
                "HoldReason" character varying(1000),
                "ItemId" uuid NOT NULL,
                "LabRequirement" character varying(160),
                "LatestOffsetDays" integer,
                "Name" character varying(160) NOT NULL,
                "Outcome" character varying(16) NOT NULL,
                "PlanId" uuid NOT NULL,
                "PlanVersion" integer NOT NULL,
                "PredecessorsJson" jsonb NOT NULL,
                "Region" character varying(80),
                "ReservedMinutes" integer NOT NULL,
                "ReviewCondition" character varying(1000),
                "ShortReservationAcknowledgedAt" timestamp with time zone,
                "ShortReservationAcknowledgedBy" integer,
                "TargetOffsetDays" integer,
                "Version" integer NOT NULL,
                CONSTRAINT "PK_TREATMENT_STEP" PRIMARY KEY ("Id"),
                CONSTRAINT "CK_STEP_DURATION" CHECK ("EstimatedMinutes" > 0 AND "ReservedMinutes" BETWEEN 15 AND 480 AND "ReservedMinutes" % 15 = 0 AND "ReservedMinutes" >= "EstimatedMinutes"),
                CONSTRAINT "CK_STEP_ESTIMATE_RANGE" CHECK (("EstimatedMinMinutes" IS NULL AND "EstimatedMaxMinutes" IS NULL) OR ("EstimatedMinMinutes" IS NOT NULL AND "EstimatedMaxMinutes" IS NOT NULL AND "EstimatedMinMinutes" > 0 AND "EstimatedMinMinutes" <= "EstimatedMinutes" AND "EstimatedMinutes" <= "EstimatedMaxMinutes" AND "EstimatedMaxMinutes" <= 480)),
                CONSTRAINT "FK_TREATMENT_STEP_TREATMENT_DOSSIER_DossierId" FOREIGN KEY ("DossierId") REFERENCES "TREATMENT_DOSSIER" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_TREATMENT_STEP_TREATMENT_PLAN_ITEM_ItemId" FOREIGN KEY ("ItemId") REFERENCES "TREATMENT_PLAN_ITEM" ("Id") ON DELETE RESTRICT
            );
            CREATE TABLE "TREATMENT_TEMPLATE_ITEM" (
                "Id" uuid NOT NULL,
                "AssistantId" integer,
                "AssistantName" character varying(200),
                "CostVnd" bigint NOT NULL,
                "Date" date,
                "DeletedAt" timestamp with time zone,
                "DentistId" integer,
                "DentistName" character varying(200),
                "EstimatedMinutes" integer,
                "Note" character varying(1000),
                "Position" integer NOT NULL,
                "ServiceId" integer NOT NULL,
                "ServiceName" character varying(200) NOT NULL,
                "TeethJson" jsonb NOT NULL,
                "TemplateId" uuid NOT NULL,
                CONSTRAINT "PK_TREATMENT_TEMPLATE_ITEM" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_TREATMENT_TEMPLATE_ITEM_SCHEDULING_SERVICE_ServiceId" FOREIGN KEY ("ServiceId") REFERENCES "SCHEDULING_SERVICE" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_TREATMENT_TEMPLATE_ITEM_TREATMENT_TEMPLATE_TemplateId" FOREIGN KEY ("TemplateId") REFERENCES "TREATMENT_TEMPLATE" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_TREATMENT_TEMPLATE_ITEM_USER_ACCOUNT_AssistantId" FOREIGN KEY ("AssistantId") REFERENCES "USER_ACCOUNT" ("EmployeeId") ON DELETE RESTRICT,
                CONSTRAINT "FK_TREATMENT_TEMPLATE_ITEM_USER_ACCOUNT_DentistId" FOREIGN KEY ("DentistId") REFERENCES "USER_ACCOUNT" ("EmployeeId") ON DELETE RESTRICT
            );
            CREATE TABLE "TREATMENT_TEMPLATE_VERSION" (
                "Id" uuid NOT NULL,
                "ActivatedAt" timestamp with time zone,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" integer NOT NULL,
                "Number" integer NOT NULL,
                "RetiredAt" timestamp with time zone,
                "State" character varying(16) NOT NULL,
                "StepsJson" jsonb NOT NULL,
                "TemplateId" uuid NOT NULL,
                CONSTRAINT "PK_TREATMENT_TEMPLATE_VERSION" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_TREATMENT_TEMPLATE_VERSION_TREATMENT_TEMPLATE_TemplateId" FOREIGN KEY ("TemplateId") REFERENCES "TREATMENT_TEMPLATE" ("Id") ON DELETE RESTRICT
            );

            INSERT INTO "TREATMENT_SHEET_DRAFT"
            SELECT restored.* FROM "TREATMENT_DOSSIER" p CROSS JOIN LATERAL jsonb_array_elements(p."SheetDrafts") item
            CROSS JOIN LATERAL jsonb_populate_record(NULL::"TREATMENT_SHEET_DRAFT",
                jsonb_set(item, '{PayloadJson}', (item->>'PayloadJson')::jsonb)) restored;
            INSERT INTO "TREATMENT_REVISION_DRAFT"
            SELECT restored.* FROM "TREATMENT_DOSSIER" p CROSS JOIN LATERAL jsonb_array_elements(p."RevisionDrafts") item
            CROSS JOIN LATERAL jsonb_populate_record(NULL::"TREATMENT_REVISION_DRAFT",
                jsonb_set(item, '{PayloadJson}', (item->>'PayloadJson')::jsonb)) restored;
            INSERT INTO "TREATMENT_STEP"
            SELECT restored.* FROM "TREATMENT_PLAN_ITEM" p CROSS JOIN LATERAL jsonb_array_elements(p."Steps") item
            CROSS JOIN LATERAL jsonb_populate_record(NULL::"TREATMENT_STEP",
                jsonb_set(item, '{PredecessorsJson}', (item->>'PredecessorsJson')::jsonb)) restored;
            INSERT INTO "TREATMENT_TEMPLATE_ITEM"
            SELECT restored.* FROM "TREATMENT_TEMPLATE" p CROSS JOIN LATERAL jsonb_array_elements(p."Items") item
            CROSS JOIN LATERAL jsonb_populate_record(NULL::"TREATMENT_TEMPLATE_ITEM",
                jsonb_set(item, '{TeethJson}', (item->>'TeethJson')::jsonb)) restored;
            INSERT INTO "TREATMENT_TEMPLATE_VERSION"
            SELECT restored.* FROM "TREATMENT_TEMPLATE" p CROSS JOIN LATERAL jsonb_array_elements(p."Versions") item
            CROSS JOIN LATERAL jsonb_populate_record(NULL::"TREATMENT_TEMPLATE_VERSION",
                jsonb_set(item, '{StepsJson}', (item->>'StepsJson')::jsonb)) restored;
            INSERT INTO "CLINICAL_REVIEW" ("Id", "Basis", "BranchId", "Complaint", "Coverage", "CoveredComponent", "DentistAccountId", "Exclusions", "Findings", "NextAction", "Outcome", "PatientId", "Policy", "PolicyVersion", "RecordedAt", "SourceActivityId", "SourceItemId", "Version", "VisitId")
            SELECT "RecordId", "Basis", "BranchId", "Complaint", "Coverage", "CoveredComponent", "DentistAccountId", "Exclusions", "Findings", "NextAction", "Outcome", "PatientId", "Policy", "PolicyVersion", "RecordedAt", "SourceActivityId", "SourceItemId", "Version", "VisitId" FROM "CLINICAL_ACTIVITY" WHERE "ProcessKind" = 'ClinicalReview';
            INSERT INTO "CLINICAL_CARE_NOTE" ("Id", "ActorId", "BranchId", "Content", "PatientId", "RecordedAt", "Response", "ReviewId", "SourceActivityId", "VisitId")
            SELECT "RecordId", "ActorId", "BranchId", "Content", "PatientId", "RecordedAt", "Response", "ReviewId", "SourceActivityId", "VisitId" FROM "CLINICAL_ACTIVITY" WHERE "ProcessKind" = 'ClinicalCareNote';
            INSERT INTO "FOLLOW_UP_INSTRUCTION" ("Id", "ActiveAppointmentId", "BranchId", "Capability", "CreatedAt", "CreatedBy", "DentistId", "DossierId", "DurationMinutes", "EarliestOn", "Guidance", "ItemId", "LatestOn", "PatientId", "PlanVersion", "Purpose", "Reason", "SourceActivityId", "SourceCompletionEventId", "SourceVisitId", "Status", "StepId", "TargetOn", "UpdatedAt", "Version")
            SELECT "RecordId", "ActiveAppointmentId", "BranchId", "Capability", "CreatedAt", "CreatedBy", "DentistId", "DossierId", "DurationMinutes", "EarliestOn", "Guidance", "ItemId", "LatestOn", "PatientId", "PlanVersion", "Purpose", "Reason", "SourceActivityId", "SourceCompletionEventId", "SourceVisitId", "Status", "StepId", "TargetOn", "UpdatedAt", "Version" FROM "CLINICAL_ACTIVITY" WHERE "ProcessKind" = 'FollowUpInstruction';
            INSERT INTO "TREATMENT_EVENT" ("Id", "ActorId", "DossierId", "DossierVersion", "Kind", "OccurredAt", "PayloadJson")
            SELECT "LegacyId", "EventActorId", "DossierId", "DossierVersion", "Kind", "OccurredAt", "PayloadJson" FROM "AUDIT_LOG" WHERE "AuditType" = 'TreatmentEvent';
            INSERT INTO "TREATMENT_PROGRESSION_EVENT" ("Id", "ActorId", "AggregateId", "Kind", "OccurredAt", "PayloadJson", "StableKey")
            SELECT "LegacyId", "EventActorId", "AggregateId", "Kind", "OccurredAt", "PayloadJson", "StableKey" FROM "AUDIT_LOG" WHERE "AuditType" = 'TreatmentProgressionEvent';

            DELETE FROM "CLINICAL_ACTIVITY" WHERE "ProcessKind" <> 'ClinicalActivity';
            DELETE FROM "AUDIT_LOG" WHERE "AuditType" IN ('TreatmentEvent', 'TreatmentProgressionEvent');
            DROP INDEX "IX_AUDIT_LOG_AggregateId_OccurredAt";
            DROP INDEX "IX_AUDIT_LOG_DossierId_OccurredAt";
            DROP INDEX "IX_AUDIT_LOG_StableKey";
            DROP INDEX "IX_CLINICAL_ACTIVITY_ActiveAppointmentId";
            DROP INDEX "IX_CLINICAL_ACTIVITY_PatientId_Status";
            DROP INDEX "IX_CLINICAL_ACTIVITY_ProcessKind_RecordId";
            DROP INDEX "IX_CLINICAL_ACTIVITY_SourceActivityId_SourceItemId";
            DROP INDEX "IX_CLINICAL_ACTIVITY_VisitId_RecordedAt";
            DROP INDEX "IX_FOLLOW_UP_INSTRUCTION_ActiveItemOnly";
            DROP INDEX "IX_FOLLOW_UP_INSTRUCTION_ActiveStep";
            DROP INDEX "IX_FOLLOW_UP_INSTRUCTION_Step_Completion";
            ALTER TABLE "AUDIT_LOG" DROP CONSTRAINT "FK_AUDIT_LOG_TREATMENT_DOSSIER_DossierId";
            ALTER TABLE "CLINICAL_ACTIVITY" DROP CONSTRAINT "CK_CLINICAL_ACTIVITY_VALUES", DROP CONSTRAINT "CK_FOLLOW_UP_DURATION", DROP CONSTRAINT "CK_FOLLOW_UP_WINDOW";
            ALTER TABLE "AUDIT_LOG" DROP COLUMN "DossierId", DROP COLUMN "Kind", DROP COLUMN "EventActorId", DROP COLUMN "DossierVersion", DROP COLUMN "AggregateId", DROP COLUMN "StableKey";
            ALTER TABLE "TREATMENT_DOSSIER" DROP COLUMN "StorageStamp", DROP COLUMN "RevisionDrafts", DROP COLUMN "SheetDrafts";
            ALTER TABLE "TREATMENT_PLAN_ITEM" DROP COLUMN "StorageStamp", DROP COLUMN "Steps";
            ALTER TABLE "TREATMENT_TEMPLATE" DROP COLUMN "StorageStamp", DROP COLUMN "Items", DROP COLUMN "Versions";
            ALTER TABLE "CLINICAL_ACTIVITY"
                DROP COLUMN "RecordId",
                DROP COLUMN "StorageStamp",
                DROP COLUMN "ProcessKind",
                DROP COLUMN "SourceActivityId",
                DROP COLUMN "Content",
                DROP COLUMN "Response",
                DROP COLUMN "ActorId",
                DROP COLUMN "RecordedAt",
                DROP COLUMN "SourceItemId",
                DROP COLUMN "Complaint",
                DROP COLUMN "Findings",
                DROP COLUMN "Coverage",
                DROP COLUMN "CoveredComponent",
                DROP COLUMN "Exclusions",
                DROP COLUMN "Policy",
                DROP COLUMN "PolicyVersion",
                DROP COLUMN "Basis",
                DROP COLUMN "NextAction",
                DROP COLUMN "DentistAccountId",
                DROP COLUMN "SourceVisitId",
                DROP COLUMN "SourceCompletionEventId",
                DROP COLUMN "Purpose",
                DROP COLUMN "Reason",
                DROP COLUMN "Guidance",
                DROP COLUMN "EarliestOn",
                DROP COLUMN "TargetOn",
                DROP COLUMN "LatestOn",
                DROP COLUMN "DurationMinutes",
                DROP COLUMN "DentistId",
                DROP COLUMN "Capability",
                DROP COLUMN "Status",
                DROP COLUMN "ActiveAppointmentId",
                DROP COLUMN "CreatedBy",
                DROP COLUMN "UpdatedAt",
                ALTER COLUMN "BranchId" SET NOT NULL,
                ALTER COLUMN "ChargingBasis" SET NOT NULL,
                ALTER COLUMN "CreatedAt" SET NOT NULL,
                ALTER COLUMN "DiscountSnapshotVnd" SET NOT NULL,
                ALTER COLUMN "FeeStatus" SET NOT NULL,
                ALTER COLUMN "IsPlanTransfer" SET NOT NULL,
                ALTER COLUMN "Outcome" SET NOT NULL,
                ALTER COLUMN "PatientId" SET NOT NULL,
                ALTER COLUMN "PerformedAt" SET NOT NULL,
                ALTER COLUMN "PerformerId" SET NOT NULL,
                ALTER COLUMN "PriceSnapshotVnd" SET NOT NULL,
                ALTER COLUMN "Quantity" SET NOT NULL,
                ALTER COLUMN "ServiceId" SET NOT NULL,
                ALTER COLUMN "SourceKind" SET NOT NULL,
                ALTER COLUMN "State" SET NOT NULL,
                ALTER COLUMN "Unit" SET NOT NULL,
                ALTER COLUMN "Version" SET NOT NULL,
                ADD CONSTRAINT "CK_CLINICAL_ACTIVITY_VALUES" CHECK ("Quantity" > 0 AND "PriceSnapshotVnd" >= 0);
            CREATE INDEX "IX_CLINICAL_CARE_NOTE_VisitId_RecordedAt" ON "CLINICAL_CARE_NOTE" ("VisitId", "RecordedAt");
            CREATE INDEX "IX_CLINICAL_REVIEW_SourceActivityId_SourceItemId" ON "CLINICAL_REVIEW" ("SourceActivityId", "SourceItemId");
            CREATE INDEX "IX_CLINICAL_REVIEW_VisitId_RecordedAt" ON "CLINICAL_REVIEW" ("VisitId", "RecordedAt");
            CREATE UNIQUE INDEX "IX_FOLLOW_UP_INSTRUCTION_ActiveAppointmentId" ON "FOLLOW_UP_INSTRUCTION" ("ActiveAppointmentId") WHERE "ActiveAppointmentId" IS NOT NULL;
            CREATE UNIQUE INDEX "IX_FOLLOW_UP_INSTRUCTION_ActiveItemOnly" ON "FOLLOW_UP_INSTRUCTION" ("DossierId", "ItemId") WHERE "DossierId" IS NOT NULL AND "ItemId" IS NOT NULL AND "StepId" IS NULL AND "Status" IN ('NeedsBooking','Booked');
            CREATE UNIQUE INDEX "IX_FOLLOW_UP_INSTRUCTION_ActiveStep" ON "FOLLOW_UP_INSTRUCTION" ("StepId") WHERE "StepId" IS NOT NULL AND "Status" IN ('NeedsBooking','Booked');
            CREATE INDEX "IX_FOLLOW_UP_INSTRUCTION_PatientId_Status" ON "FOLLOW_UP_INSTRUCTION" ("PatientId", "Status");
            CREATE UNIQUE INDEX "IX_FOLLOW_UP_INSTRUCTION_Step_Completion" ON "FOLLOW_UP_INSTRUCTION" ("StepId", "SourceCompletionEventId") WHERE "StepId" IS NOT NULL AND "SourceCompletionEventId" IS NOT NULL;
            CREATE INDEX "IX_TREATMENT_EVENT_DossierId_OccurredAt" ON "TREATMENT_EVENT" ("DossierId", "OccurredAt");
            CREATE INDEX "IX_TREATMENT_PROGRESSION_EVENT_AggregateId_OccurredAt" ON "TREATMENT_PROGRESSION_EVENT" ("AggregateId", "OccurredAt");
            CREATE UNIQUE INDEX "IX_TREATMENT_PROGRESSION_EVENT_StableKey" ON "TREATMENT_PROGRESSION_EVENT" ("StableKey") WHERE "StableKey" IS NOT NULL;
            CREATE UNIQUE INDEX "IX_TREATMENT_REVISION_DRAFT_DossierId_ItemId_ActorId" ON "TREATMENT_REVISION_DRAFT" ("DossierId", "ItemId", "ActorId");
            CREATE INDEX "IX_TREATMENT_REVISION_DRAFT_ItemId" ON "TREATMENT_REVISION_DRAFT" ("ItemId");
            CREATE INDEX "IX_TREATMENT_SHEET_DRAFT_DossierId_Kind_UpdatedAt" ON "TREATMENT_SHEET_DRAFT" ("DossierId", "Kind", "UpdatedAt");
            CREATE INDEX "IX_TREATMENT_STEP_DossierId_PlanVersion" ON "TREATMENT_STEP" ("DossierId", "PlanVersion");
            CREATE INDEX "IX_TREATMENT_STEP_ItemId" ON "TREATMENT_STEP" ("ItemId");
            CREATE INDEX "IX_TREATMENT_TEMPLATE_ITEM_AssistantId" ON "TREATMENT_TEMPLATE_ITEM" ("AssistantId");
            CREATE INDEX "IX_TREATMENT_TEMPLATE_ITEM_DentistId" ON "TREATMENT_TEMPLATE_ITEM" ("DentistId");
            CREATE INDEX "IX_TREATMENT_TEMPLATE_ITEM_ServiceId" ON "TREATMENT_TEMPLATE_ITEM" ("ServiceId");
            CREATE INDEX "IX_TREATMENT_TEMPLATE_ITEM_TemplateId_Position" ON "TREATMENT_TEMPLATE_ITEM" ("TemplateId", "Position");
            CREATE UNIQUE INDEX "IX_TREATMENT_TEMPLATE_VERSION_Active" ON "TREATMENT_TEMPLATE_VERSION" ("TemplateId") WHERE "State" = 'Active';
            CREATE UNIQUE INDEX "IX_TREATMENT_TEMPLATE_VERSION_TemplateId_Number" ON "TREATMENT_TEMPLATE_VERSION" ("TemplateId", "Number");
            """);
    }
}
