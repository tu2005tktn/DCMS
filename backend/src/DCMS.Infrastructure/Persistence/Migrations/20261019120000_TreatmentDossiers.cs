using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DCMS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DcmsDbContext))]
[Migration("20261019120000_TreatmentDossiers")]
public sealed class TreatmentDossiers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE "TREATMENT_DOSSIER" (
              "Id" uuid PRIMARY KEY, "Code" varchar(40) NOT NULL UNIQUE,
              "PatientId" integer NOT NULL REFERENCES "PATIENT_PROFILE"("Id") ON DELETE RESTRICT,
              "BranchId" integer NOT NULL REFERENCES "SCHEDULING_BRANCH"("Id") ON DELETE RESTRICT,
              "DentistId" integer NOT NULL,
              "StartedOn" date NOT NULL, "Diagnosis" varchar(4000) NOT NULL,
              "Note" varchar(4000), "State" varchar(16) NOT NULL,
              "ClosureOutcome" varchar(32), "ClosureReason" varchar(1000),
              "ClosedAt" timestamptz, "ClosedBy" integer,
              "Version" integer NOT NULL, "PlanId" uuid NOT NULL,
              "PlanVersion" integer NOT NULL, "PlanState" varchar(16) NOT NULL,
              "Goals" varchar(4000), "PaymentTermsJson" jsonb NOT NULL,
              "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL,
              CONSTRAINT "FK_TREATMENT_DOSSIER_MEMBER" FOREIGN KEY ("BranchId", "DentistId")
                REFERENCES "SCHEDULING_MEMBER"("BranchId", "EmployeeId") ON DELETE RESTRICT
            );
            CREATE INDEX "IX_TREATMENT_DOSSIER_PatientId_BranchId_UpdatedAt"
              ON "TREATMENT_DOSSIER" ("PatientId", "BranchId", "UpdatedAt");
            CREATE INDEX "IX_TREATMENT_DOSSIER_BranchId_DentistId"
              ON "TREATMENT_DOSSIER" ("BranchId", "DentistId");
            CREATE TABLE "TREATMENT_PLAN_ITEM" (
              "Id" uuid PRIMARY KEY, "DossierId" uuid NOT NULL REFERENCES "TREATMENT_DOSSIER"("Id") ON DELETE RESTRICT,
              "SourceDiagnosisId" integer, "ServiceId" integer NOT NULL,
              "ServiceName" varchar(160) NOT NULL, "Region" varchar(80), "Unit" varchar(40) NOT NULL,
              "Quantity" numeric(12,3) NOT NULL, "PlannedOrder" integer NOT NULL,
              "EstimatedMinutes" integer, "EstimatedStart" date, "EstimatedEnd" date,
              "AgreedUnitPriceVnd" bigint NOT NULL, "Decision" varchar(16) NOT NULL,
              "Progress" varchar(16) NOT NULL, "Counseling" varchar(4000),
              "DecisionMaker" varchar(160), "RepresentativeBasis" varchar(1000),
              "DecidedAt" timestamptz, "DecidedBy" integer, "ConsentDocumentId" uuid,
              "ConsentPlanVersion" integer, "WarrantyPolicy" varchar(160),
              "WarrantyPolicyVersion" varchar(40), "WarrantyScope" varchar(1000),
              "WarrantyConfirmedAt" timestamptz, "WarrantyEndsOn" date, "WarrantyConfirmedBy" integer,
              CONSTRAINT "CK_TREATMENT_ITEM_VALUES" CHECK ("Quantity" > 0 AND "AgreedUnitPriceVnd" >= 0)
            );
            CREATE INDEX "IX_TREATMENT_PLAN_ITEM_DossierId_PlannedOrder"
              ON "TREATMENT_PLAN_ITEM" ("DossierId", "PlannedOrder");
            CREATE TABLE "TREATMENT_EVENT" (
              "Id" uuid PRIMARY KEY, "DossierId" uuid NOT NULL REFERENCES "TREATMENT_DOSSIER"("Id") ON DELETE RESTRICT,
              "Kind" varchar(40) NOT NULL, "PayloadJson" jsonb NOT NULL,
              "ActorId" integer NOT NULL, "OccurredAt" timestamptz NOT NULL, "DossierVersion" integer NOT NULL
            );
            CREATE INDEX "IX_TREATMENT_EVENT_DossierId_OccurredAt"
              ON "TREATMENT_EVENT" ("DossierId", "OccurredAt");
            CREATE TABLE "TREATMENT_RECEIPT" (
              "RequestKey" uuid PRIMARY KEY, "ActorId" integer NOT NULL,
              "Command" varchar(40) NOT NULL, "Fingerprint" varchar(64) NOT NULL,
              "DossierId" uuid NOT NULL REFERENCES "TREATMENT_DOSSIER"("Id") ON DELETE RESTRICT,
              "ResultJson" jsonb NOT NULL
            );
            CREATE INDEX "IX_TREATMENT_RECEIPT_DossierId" ON "TREATMENT_RECEIPT" ("DossierId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE "TREATMENT_RECEIPT";
            DROP TABLE "TREATMENT_EVENT";
            DROP TABLE "TREATMENT_PLAN_ITEM";
            DROP TABLE "TREATMENT_DOSSIER";
            """);
    }
}
