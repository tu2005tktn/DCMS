using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DCMS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DcmsDbContext))]
[Migration("20261025190000_ClinicalWorkAndCare")]
public sealed class ClinicalWorkAndCare : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE "CLINICAL_ACTIVITY" (
              "Id" uuid PRIMARY KEY, "VisitId" uuid NOT NULL, "PatientId" integer NOT NULL,
              "BranchId" integer NOT NULL, "DossierId" uuid, "ItemId" uuid, "StepId" uuid,
              "PlanVersion" integer, "ServiceId" integer NOT NULL, "Region" varchar(80),
              "Unit" varchar(40) NOT NULL, "Quantity" numeric(12,3) NOT NULL,
              "PerformedAt" timestamptz NOT NULL, "ElapsedMinutes" integer,
              "PerformerId" integer NOT NULL, "Outcome" varchar(4000) NOT NULL,
              "NextSteps" varchar(2000), "ChargingBasis" varchar(16) NOT NULL,
              "ChargingBasisEvidence" varchar(1000),
              "PriceSnapshotVnd" numeric(18,0) NOT NULL, "ReviewId" uuid,
              "State" varchar(16) NOT NULL, "Version" integer NOT NULL,
              "CreatedAt" timestamptz NOT NULL, "PostedAt" timestamptz,
              CONSTRAINT "CK_CLINICAL_ACTIVITY_VALUES" CHECK ("Quantity" > 0 AND "PriceSnapshotVnd" >= 0)
            );
            CREATE INDEX "IX_CLINICAL_ACTIVITY_VisitId_State" ON "CLINICAL_ACTIVITY" ("VisitId", "State");
            CREATE INDEX "IX_CLINICAL_ACTIVITY_PatientId_DossierId_StepId" ON "CLINICAL_ACTIVITY" ("PatientId", "DossierId", "StepId");
            CREATE TABLE "CLINICAL_REVIEW" (
              "Id" uuid PRIMARY KEY, "VisitId" uuid NOT NULL, "PatientId" integer NOT NULL,
              "BranchId" integer NOT NULL, "SourceActivityId" uuid, "SourceItemId" uuid,
              "Complaint" varchar(4000) NOT NULL, "Findings" varchar(4000) NOT NULL,
              "Coverage" varchar(24) NOT NULL, "Outcome" varchar(32) NOT NULL,
              "CoveredComponent" varchar(500), "Exclusions" varchar(1000),
              "Policy" varchar(160), "PolicyVersion" varchar(40), "Basis" varchar(1000),
              "NextAction" varchar(2000), "DentistAccountId" integer NOT NULL,
              "RecordedAt" timestamptz NOT NULL, "Version" integer NOT NULL
            );
            CREATE INDEX "IX_CLINICAL_REVIEW_VisitId_RecordedAt" ON "CLINICAL_REVIEW" ("VisitId", "RecordedAt");
            CREATE INDEX "IX_CLINICAL_REVIEW_SourceActivityId_SourceItemId" ON "CLINICAL_REVIEW" ("SourceActivityId", "SourceItemId");
            CREATE TABLE "CLINICAL_CARE_NOTE" (
              "Id" uuid PRIMARY KEY, "VisitId" uuid NOT NULL, "PatientId" integer NOT NULL,
              "BranchId" integer NOT NULL, "SourceActivityId" uuid, "ReviewId" uuid,
              "Content" varchar(4000) NOT NULL, "Response" varchar(4000),
              "ActorId" integer NOT NULL, "RecordedAt" timestamptz NOT NULL
            );
            CREATE INDEX "IX_CLINICAL_CARE_NOTE_VisitId_RecordedAt" ON "CLINICAL_CARE_NOTE" ("VisitId", "RecordedAt");
            CREATE TABLE "CLINICAL_COMMAND_RECEIPT" (
              "RequestKey" uuid PRIMARY KEY, "ActorId" integer NOT NULL,
              "Command" varchar(40) NOT NULL, "Fingerprint" varchar(64) NOT NULL,
              "ResultId" uuid NOT NULL
            );
            CREATE TABLE "CARE_TASK" (
              "Id" uuid PRIMARY KEY, "PatientId" integer NOT NULL, "BranchId" integer NOT NULL,
              "InstructionId" uuid, "AppointmentId" uuid, "ReviewId" uuid,
              "EpisodeKey" varchar(200), "Type" varchar(40) NOT NULL,
              "Content" varchar(4000) NOT NULL, "OwnerAccountId" integer,
              "DueAt" timestamptz NOT NULL, "Status" varchar(16) NOT NULL,
              "Version" integer NOT NULL, "CreatedBy" integer NOT NULL,
              "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL
            );
            CREATE INDEX "IX_CARE_TASK_BranchId_DueAt_Status" ON "CARE_TASK" ("BranchId", "DueAt", "Status");
            CREATE UNIQUE INDEX "IX_CARE_TASK_EpisodeKey" ON "CARE_TASK" ("EpisodeKey") WHERE "EpisodeKey" IS NOT NULL;
            CREATE TABLE "CARE_CONTACT_ATTEMPT" (
              "Id" uuid PRIMARY KEY, "TaskId" uuid NOT NULL REFERENCES "CARE_TASK" ("Id") ON DELETE RESTRICT,
              "Channel" varchar(32) NOT NULL, "Result" varchar(40) NOT NULL,
              "NextAction" varchar(1000), "RebookedAppointmentId" uuid,
              "ActorId" integer NOT NULL, "OccurredAt" timestamptz NOT NULL
            );
            CREATE INDEX "IX_CARE_CONTACT_ATTEMPT_TaskId_OccurredAt" ON "CARE_CONTACT_ATTEMPT" ("TaskId", "OccurredAt");
            CREATE TABLE "CARE_ESCALATION" (
              "Id" uuid PRIMARY KEY, "TaskId" uuid NOT NULL REFERENCES "CARE_TASK" ("Id") ON DELETE RESTRICT,
              "TargetClinicianAccountId" integer NOT NULL, "Reason" varchar(1000) NOT NULL,
              "Resolution" varchar(4000), "Status" varchar(16) NOT NULL,
              "CreatedBy" integer NOT NULL, "CreatedAt" timestamptz NOT NULL,
              "Version" integer NOT NULL, "AcknowledgedAt" timestamptz, "ResolvedAt" timestamptz
            );
            CREATE INDEX "IX_CARE_ESCALATION_TaskId_CreatedAt" ON "CARE_ESCALATION" ("TaskId", "CreatedAt");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE "CARE_ESCALATION";
            DROP TABLE "CARE_CONTACT_ATTEMPT";
            DROP TABLE "CARE_TASK";
            DROP TABLE "CLINICAL_COMMAND_RECEIPT";
            DROP TABLE "CLINICAL_CARE_NOTE";
            DROP TABLE "CLINICAL_REVIEW";
            DROP TABLE "CLINICAL_ACTIVITY";
            """);
    }
}
