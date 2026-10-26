using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations;

public partial class ReceptionVisits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The old S17/S18 code was removed while its migration may remain applied. Adopt its Visit
        // in place, preserving IDs, receipts, audits and legacy clinical references. All SQL is in EF's transaction.
        migrationBuilder.Sql("""
            DO $upgrade$
            BEGIN
              IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema=current_schema() AND table_name='VISIT' AND column_name='ActualArrivalAt') THEN
                ALTER TABLE "VISIT" RENAME COLUMN "ActualArrivalAt" TO "ArrivalAt";
                ALTER TABLE "VISIT" RENAME COLUMN "EnteredAt" TO "RecordedAt";
                ALTER TABLE "VISIT" RENAME COLUMN "EnteredBy" TO "RecordedBy";
                ALTER TABLE "VISIT" RENAME CONSTRAINT "FK_VISIT_USER_ACCOUNT_EnteredBy" TO "FK_VISIT_USER_ACCOUNT_RecordedBy";
                ALTER TABLE "VISIT" RENAME CONSTRAINT "CK_VISIT_ARRIVAL" TO "CK_VISIT_Arrival";
                ALTER TABLE "VISIT" RENAME CONSTRAINT "CK_VISIT_STATUS" TO "CK_VISIT_Status";
                ALTER TABLE "VISIT" RENAME CONSTRAINT "CK_VISIT_PURPOSE" TO "CK_VISIT_Purpose";
                ALTER INDEX "IX_VISIT_EnteredBy" RENAME TO "IX_VISIT_RecordedBy";
                ALTER TABLE "VISIT" ALTER COLUMN "Code" SET DEFAULT ('VS-' || replace(gen_random_uuid()::text, '-', ''));
                ALTER TABLE "VISIT" ALTER COLUMN "IdentityNote" SET DEFAULT 'Verified at reception';
                ALTER TABLE "VISIT" ALTER COLUMN "QueueOrder" SET DEFAULT 0;
                ALTER TABLE "VISIT_AUDIT" ALTER COLUMN "Reason" SET DEFAULT '';
                ALTER TABLE "RECEPTION_COMMAND_RECEIPT" RENAME TO "RECEPTION_RECEIPT";
                ALTER TABLE "RECEPTION_RECEIPT" RENAME CONSTRAINT "PK_RECEPTION_COMMAND_RECEIPT" TO "PK_RECEPTION_RECEIPT";
                ALTER TABLE "RECEPTION_RECEIPT" RENAME CONSTRAINT "FK_RECEPTION_COMMAND_RECEIPT_USER_ACCOUNT_ActorAccountId" TO "FK_RECEPTION_RECEIPT_USER_ACCOUNT_ActorAccountId";
                ALTER TABLE "RECEPTION_RECEIPT" RENAME CONSTRAINT "FK_RECEPTION_COMMAND_RECEIPT_VISIT_VisitId" TO "FK_RECEPTION_RECEIPT_VISIT_VisitId";
                ALTER INDEX "IX_RECEPTION_COMMAND_RECEIPT_VisitId" RENAME TO "IX_RECEPTION_RECEIPT_VisitId";
              ELSIF to_regclass('"VISIT"') IS NOT NULL AND NOT EXISTS
                (SELECT 1 FROM information_schema.columns WHERE table_schema=current_schema() AND table_name='VISIT' AND column_name='RecordedAt') THEN
                RAISE EXCEPTION 'Unsupported VISIT schema; reception upgrade has made no committed changes';
              END IF;
            END $upgrade$;

            CREATE TABLE IF NOT EXISTS "VISIT" (
              "Id" uuid NOT NULL CONSTRAINT "PK_VISIT" PRIMARY KEY,
              "AppointmentId" uuid NOT NULL,
              "PatientId" integer NOT NULL, "BranchId" integer NOT NULL, "DentistId" integer NOT NULL,
              "Status" varchar(24) NOT NULL, "Version" integer NOT NULL,
              "ArrivalAt" timestamptz NOT NULL, "RecordedAt" timestamptz NOT NULL, "RecordedBy" integer NOT NULL,
              "ArrivalReason" varchar(500), "Purpose" varchar(40) NOT NULL, "Note" varchar(1000),
              "SourceJson" jsonb, "RequiresClinicalSourceReview" boolean NOT NULL DEFAULT false,
              CONSTRAINT "CK_VISIT_Status" CHECK ("Status" IN ('Queued','InProgress','Completed','Cancelled')),
              CONSTRAINT "CK_VISIT_Purpose" CHECK ("Purpose" IN ('InitialExamination','TreatmentContinuation','PostoperativeOrWarrantyReview','PeriodicOrNewProblem')),
              CONSTRAINT "CK_VISIT_Arrival" CHECK ("ArrivalAt" <= "RecordedAt"),
              CONSTRAINT "FK_VISIT_APPOINTMENT_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT"("Id") ON DELETE RESTRICT,
              CONSTRAINT "FK_VISIT_PATIENT_PROFILE_PatientId" FOREIGN KEY ("PatientId") REFERENCES "PATIENT_PROFILE"("Id") ON DELETE RESTRICT,
              CONSTRAINT "FK_VISIT_SCHEDULING_BRANCH_BranchId" FOREIGN KEY ("BranchId") REFERENCES "SCHEDULING_BRANCH"("Id") ON DELETE RESTRICT,
              CONSTRAINT "FK_VISIT_EMPLOYEE_PROFILE_DentistId" FOREIGN KEY ("DentistId") REFERENCES "EMPLOYEE_PROFILE"("Id") ON DELETE RESTRICT,
              CONSTRAINT "FK_VISIT_USER_ACCOUNT_RecordedBy" FOREIGN KEY ("RecordedBy") REFERENCES "USER_ACCOUNT"("Id") ON DELETE RESTRICT
            );
            ALTER TABLE "VISIT" ADD COLUMN IF NOT EXISTS "SourceJson" jsonb;
            ALTER TABLE "VISIT" ADD COLUMN IF NOT EXISTS "RequiresClinicalSourceReview" boolean NOT NULL DEFAULT false;
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_VISIT_AppointmentId" ON "VISIT"("AppointmentId");
            CREATE INDEX IF NOT EXISTS "IX_VISIT_PatientId" ON "VISIT"("PatientId");
            CREATE INDEX IF NOT EXISTS "IX_VISIT_DentistId" ON "VISIT"("DentistId");
            CREATE INDEX IF NOT EXISTS "IX_VISIT_RecordedBy" ON "VISIT"("RecordedBy");
            CREATE INDEX IF NOT EXISTS "IX_VISIT_BranchId_ArrivalAt_Status" ON "VISIT"("BranchId","ArrivalAt","Status");

            CREATE TABLE IF NOT EXISTS "RECEPTION_RECEIPT" (
              "ActorAccountId" integer NOT NULL, "RequestKey" uuid NOT NULL, "VisitId" uuid NOT NULL, "Fingerprint" varchar(64) NOT NULL,
              CONSTRAINT "PK_RECEPTION_RECEIPT" PRIMARY KEY ("ActorAccountId","RequestKey"),
              CONSTRAINT "FK_RECEPTION_RECEIPT_USER_ACCOUNT_ActorAccountId" FOREIGN KEY ("ActorAccountId") REFERENCES "USER_ACCOUNT"("Id") ON DELETE RESTRICT,
              CONSTRAINT "FK_RECEPTION_RECEIPT_VISIT_VisitId" FOREIGN KEY ("VisitId") REFERENCES "VISIT"("Id") ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS "IX_RECEPTION_RECEIPT_VisitId" ON "RECEPTION_RECEIPT"("VisitId");
            CREATE TABLE IF NOT EXISTS "VISIT_AUDIT" (
              "Id" uuid NOT NULL CONSTRAINT "PK_VISIT_AUDIT" PRIMARY KEY, "VisitId" uuid NOT NULL,
              "ActorAccountId" integer NOT NULL, "Action" varchar(32) NOT NULL, "OccurredAt" timestamptz NOT NULL,
              "BeforeJson" jsonb NOT NULL, "AfterJson" jsonb NOT NULL,
              CONSTRAINT "FK_VISIT_AUDIT_VISIT_VisitId" FOREIGN KEY ("VisitId") REFERENCES "VISIT"("Id") ON DELETE RESTRICT,
              CONSTRAINT "FK_VISIT_AUDIT_USER_ACCOUNT_ActorAccountId" FOREIGN KEY ("ActorAccountId") REFERENCES "USER_ACCOUNT"("Id") ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS "IX_VISIT_AUDIT_ActorAccountId" ON "VISIT_AUDIT"("ActorAccountId");
            CREATE INDEX IF NOT EXISTS "IX_VISIT_AUDIT_VisitId_OccurredAt" ON "VISIT_AUDIT"("VisitId","OccurredAt");

            DO $sources$
            BEGIN
              IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema=current_schema() AND table_name='VISIT' AND column_name='DossierId') THEN
                UPDATE "VISIT" v SET "RequiresClinicalSourceReview" = v."Purpose" <> 'InitialExamination',
                  "Note" = COALESCE(v."Note",v."AdditionalConcern"),
                  "SourceJson" = jsonb_strip_nulls(jsonb_build_object('PriorCourseId',v."DossierId",'ItemId',v."ItemId",'StepId',v."StepId",'ActivityId',v."ActivityId",'FollowUpId',a."FollowUpId"))
                FROM "APPOINTMENT" a WHERE a."Id"=v."AppointmentId" AND v."SourceJson" IS NULL;
                IF to_regclass('"CLINICAL_SOURCE_REFERENCE"') IS NOT NULL THEN
                  UPDATE "VISIT" v SET "SourceJson" = v."SourceJson" || jsonb_strip_nulls(jsonb_build_object(
                    'Label',(SELECT c."Label" FROM "CLINICAL_SOURCE_REFERENCE" c WHERE c."Id"=v."DossierId" AND c."PatientId"=v."PatientId" AND c."BranchId"=v."BranchId"),
                    'Blocker',(SELECT string_agg(DISTINCT c."Blocker",'; ') FROM "CLINICAL_SOURCE_REFERENCE" c WHERE c."Id" IN (v."DossierId",v."ItemId",v."StepId",v."ActivityId") AND c."PatientId"=v."PatientId" AND c."BranchId"=v."BranchId")));
                END IF;
              END IF;
            END $sources$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // An adopted legacy database keeps its rows and original columns on rollback.
        migrationBuilder.Sql("""
            DO $rollback$
            BEGIN
              IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema=current_schema() AND table_name='VISIT' AND column_name='Code') THEN
                ALTER TABLE "VISIT" RENAME COLUMN "ArrivalAt" TO "ActualArrivalAt";
                ALTER TABLE "VISIT" RENAME COLUMN "RecordedAt" TO "EnteredAt";
                ALTER TABLE "VISIT" RENAME COLUMN "RecordedBy" TO "EnteredBy";
                ALTER TABLE "VISIT" RENAME CONSTRAINT "FK_VISIT_USER_ACCOUNT_RecordedBy" TO "FK_VISIT_USER_ACCOUNT_EnteredBy";
                ALTER TABLE "VISIT" RENAME CONSTRAINT "CK_VISIT_Arrival" TO "CK_VISIT_ARRIVAL";
                ALTER TABLE "VISIT" RENAME CONSTRAINT "CK_VISIT_Status" TO "CK_VISIT_STATUS";
                ALTER TABLE "VISIT" RENAME CONSTRAINT "CK_VISIT_Purpose" TO "CK_VISIT_PURPOSE";
                ALTER INDEX "IX_VISIT_RecordedBy" RENAME TO "IX_VISIT_EnteredBy";
                ALTER TABLE "RECEPTION_RECEIPT" RENAME TO "RECEPTION_COMMAND_RECEIPT";
                ALTER TABLE "RECEPTION_COMMAND_RECEIPT" RENAME CONSTRAINT "PK_RECEPTION_RECEIPT" TO "PK_RECEPTION_COMMAND_RECEIPT";
                ALTER TABLE "RECEPTION_COMMAND_RECEIPT" RENAME CONSTRAINT "FK_RECEPTION_RECEIPT_USER_ACCOUNT_ActorAccountId" TO "FK_RECEPTION_COMMAND_RECEIPT_USER_ACCOUNT_ActorAccountId";
                ALTER TABLE "RECEPTION_COMMAND_RECEIPT" RENAME CONSTRAINT "FK_RECEPTION_RECEIPT_VISIT_VisitId" TO "FK_RECEPTION_COMMAND_RECEIPT_VISIT_VisitId";
                ALTER INDEX "IX_RECEPTION_RECEIPT_VisitId" RENAME TO "IX_RECEPTION_COMMAND_RECEIPT_VisitId";
              ELSE
                DROP TABLE "RECEPTION_RECEIPT";
                DROP TABLE "VISIT_AUDIT";
                DROP TABLE "VISIT";
              END IF;
            END $rollback$;
            """);
    }
}
