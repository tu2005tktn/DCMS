using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DCMS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DcmsDbContext))]
[Migration("20261019143000_TreatmentProgression")]
public sealed class TreatmentProgression : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "TREATMENT_DOSSIER" ADD COLUMN "SourceTemplateId" uuid;
            ALTER TABLE "TREATMENT_DOSSIER" ADD COLUMN "SourceTemplateVersion" integer;
            CREATE INDEX "IX_TREATMENT_DOSSIER_SourceTemplateId" ON "TREATMENT_DOSSIER" ("SourceTemplateId");
            CREATE TABLE "TREATMENT_TEMPLATE" (
              "Id" uuid PRIMARY KEY, "BranchId" integer NOT NULL REFERENCES "SCHEDULING_BRANCH"("Id") ON DELETE RESTRICT,
              "Name" varchar(160) NOT NULL, "Version" integer NOT NULL,
              "CreatedBy" integer NOT NULL, "CreatedAt" timestamptz NOT NULL
            );
            CREATE INDEX "IX_TREATMENT_TEMPLATE_BranchId_Name" ON "TREATMENT_TEMPLATE" ("BranchId", "Name");
            CREATE TABLE "TREATMENT_TEMPLATE_VERSION" (
              "Id" uuid PRIMARY KEY, "TemplateId" uuid NOT NULL REFERENCES "TREATMENT_TEMPLATE"("Id") ON DELETE RESTRICT,
              "Number" integer NOT NULL, "State" varchar(16) NOT NULL,
              "StepsJson" jsonb NOT NULL, "CreatedBy" integer NOT NULL,
              "CreatedAt" timestamptz NOT NULL, "ActivatedAt" timestamptz, "RetiredAt" timestamptz,
              CONSTRAINT "CK_TEMPLATE_VERSION_STATE" CHECK ("State" IN ('Draft','Active','Retired'))
            );
            CREATE UNIQUE INDEX "IX_TREATMENT_TEMPLATE_VERSION_TemplateId_Number"
              ON "TREATMENT_TEMPLATE_VERSION" ("TemplateId", "Number");
            CREATE UNIQUE INDEX "IX_TREATMENT_TEMPLATE_VERSION_Active"
              ON "TREATMENT_TEMPLATE_VERSION" ("TemplateId") WHERE "State" = 'Active';
            CREATE TABLE "TREATMENT_STEP" (
              "Id" uuid PRIMARY KEY, "DossierId" uuid NOT NULL REFERENCES "TREATMENT_DOSSIER"("Id") ON DELETE RESTRICT,
              "PlanId" uuid NOT NULL, "PlanVersion" integer NOT NULL,
              "ItemId" uuid NOT NULL REFERENCES "TREATMENT_PLAN_ITEM"("Id") ON DELETE RESTRICT,
              "Name" varchar(160) NOT NULL, "Region" varchar(80),
              "EstimatedMinutes" integer NOT NULL, "ReservedMinutes" integer NOT NULL,
              "PredecessorsJson" jsonb NOT NULL, "Capability" varchar(160), "LabRequirement" varchar(160),
              "EarliestOffsetDays" integer, "TargetOffsetDays" integer, "LatestOffsetDays" integer,
              "ChargingCoverage" varchar(40) NOT NULL, "Outcome" varchar(16) NOT NULL,
              "HoldReason" varchar(1000), "ReviewCondition" varchar(1000), "HoldEffectiveAt" timestamptz,
              "CompletionEventId" uuid, "CompletedAt" timestamptz, "Version" integer NOT NULL,
              CONSTRAINT "CK_STEP_DURATION" CHECK ("EstimatedMinutes" > 0 AND "ReservedMinutes" BETWEEN 15 AND 480
                AND "ReservedMinutes" % 15 = 0 AND "ReservedMinutes" >= "EstimatedMinutes")
            );
            CREATE INDEX "IX_TREATMENT_STEP_DossierId_PlanVersion" ON "TREATMENT_STEP" ("DossierId", "PlanVersion");
            CREATE INDEX "IX_TREATMENT_STEP_ItemId" ON "TREATMENT_STEP" ("ItemId");
            CREATE TABLE "FOLLOW_UP_INSTRUCTION" (
              "Id" uuid PRIMARY KEY, "PatientId" integer NOT NULL REFERENCES "PATIENT_PROFILE"("Id") ON DELETE RESTRICT,
              "BranchId" integer NOT NULL, "DossierId" uuid, "ItemId" uuid, "StepId" uuid,
              "PlanVersion" integer, "SourceVisitId" uuid, "SourceActivityId" uuid,
              "SourceCompletionEventId" uuid, "Purpose" varchar(40) NOT NULL,
              "Reason" varchar(2000) NOT NULL, "Guidance" varchar(4000),
              "EarliestOn" date NOT NULL, "TargetOn" date NOT NULL, "LatestOn" date NOT NULL,
              "DurationMinutes" integer NOT NULL, "DentistId" integer, "Capability" varchar(160),
              "Status" varchar(20) NOT NULL, "ActiveAppointmentId" uuid, "Version" integer NOT NULL,
              "CreatedBy" integer NOT NULL, "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL,
              CONSTRAINT "CK_FOLLOW_UP_WINDOW" CHECK ("EarliestOn" <= "TargetOn" AND "TargetOn" <= "LatestOn"),
              CONSTRAINT "CK_FOLLOW_UP_DURATION" CHECK ("DurationMinutes" BETWEEN 15 AND 480 AND "DurationMinutes" % 15 = 0)
            );
            CREATE INDEX "IX_FOLLOW_UP_INSTRUCTION_PatientId_Status" ON "FOLLOW_UP_INSTRUCTION" ("PatientId", "Status");
            CREATE UNIQUE INDEX "IX_FOLLOW_UP_INSTRUCTION_Step_Completion"
              ON "FOLLOW_UP_INSTRUCTION" ("StepId", "SourceCompletionEventId")
              WHERE "StepId" IS NOT NULL AND "SourceCompletionEventId" IS NOT NULL;
            CREATE UNIQUE INDEX "IX_FOLLOW_UP_INSTRUCTION_ActiveAppointmentId"
              ON "FOLLOW_UP_INSTRUCTION" ("ActiveAppointmentId") WHERE "ActiveAppointmentId" IS NOT NULL;
            CREATE TABLE "TREATMENT_PROGRESSION_RECEIPT" (
              "RequestKey" uuid PRIMARY KEY, "ActorId" integer NOT NULL,
              "Command" varchar(40) NOT NULL, "Fingerprint" varchar(64) NOT NULL, "ResultJson" jsonb NOT NULL
            );
            CREATE TABLE "TREATMENT_PROGRESSION_EVENT" (
              "Id" uuid PRIMARY KEY, "AggregateId" uuid NOT NULL, "Kind" varchar(80) NOT NULL,
              "PayloadJson" jsonb NOT NULL, "StableKey" varchar(160),
              "ActorId" integer NOT NULL, "OccurredAt" timestamptz NOT NULL
            );
            CREATE UNIQUE INDEX "IX_TREATMENT_PROGRESSION_EVENT_StableKey"
              ON "TREATMENT_PROGRESSION_EVENT" ("StableKey") WHERE "StableKey" IS NOT NULL;
            CREATE INDEX "IX_TREATMENT_PROGRESSION_EVENT_AggregateId_OccurredAt"
              ON "TREATMENT_PROGRESSION_EVENT" ("AggregateId", "OccurredAt");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE "TREATMENT_PROGRESSION_EVENT";
            DROP TABLE "TREATMENT_PROGRESSION_RECEIPT";
            DROP TABLE "FOLLOW_UP_INSTRUCTION";
            DROP TABLE "TREATMENT_STEP";
            DROP TABLE "TREATMENT_TEMPLATE_VERSION";
            DROP TABLE "TREATMENT_TEMPLATE";
            DROP INDEX "IX_TREATMENT_DOSSIER_SourceTemplateId";
            ALTER TABLE "TREATMENT_DOSSIER" DROP COLUMN "SourceTemplateVersion";
            ALTER TABLE "TREATMENT_DOSSIER" DROP COLUMN "SourceTemplateId";
            """);
    }
}
