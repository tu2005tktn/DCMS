using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyTreatmentDossierTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "TREATMENT_DOSSIER" d
                SET "SourceExaminationVisitId" = d."SourceExaminationId"
                WHERE d."SourceExaminationVisitId" IS NULL
                  AND d."SourceExaminationId" IS NOT NULL
                  AND EXISTS (SELECT 1 FROM "VISIT" v WHERE v."Id" = d."SourceExaminationId");

                UPDATE "CLINICAL_DOCUMENT"
                SET "PlanId" = "DossierId"
                WHERE "DossierId" IS NOT NULL AND "PlanId" IS NOT NULL;

                UPDATE "TREATMENT_PLAN_ITEM" i
                SET "DecisionPlanVersion" = GREATEST(COALESCE(i."DecisionPlanVersion", 0), GREATEST(d."PlanVersion", 1))
                FROM "TREATMENT_DOSSIER" d
                WHERE i."DossierId" = d."Id" AND d."PlanState" = 'Committed';

                UPDATE "TREATMENT_PLAN_ITEM" i
                SET "DecisionPlanVersion" = NULL
                FROM "TREATMENT_DOSSIER" d
                WHERE i."DossierId" = d."Id" AND d."PlanState" <> 'Committed';
                """);

            migrationBuilder.DropIndex(
                name: "IX_TREATMENT_DOSSIER_SourceTemplateId",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "ClosedBy",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "ClosureOutcome",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "ClosureReason",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "DepositExceptionAt",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "DepositExceptionBy",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "DepositExceptionReason",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "DisplayCompleted",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "Goals",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "PaymentTermsJson",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "PlanId",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "PlanState",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "PlanVersion",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "RevisionDrafts",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "SourceExaminationId",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "SourceExaminationJson",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "SourceExaminationVersion",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "SourceTemplateId",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "SourceTemplateVersion",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "StorageStamp",
                table: "TREATMENT_DOSSIER");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "TREATMENT_DOSSIER",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClosedBy",
                table: "TREATMENT_DOSSIER",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosureOutcome",
                table: "TREATMENT_DOSSIER",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosureReason",
                table: "TREATMENT_DOSSIER",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DepositExceptionAt",
                table: "TREATMENT_DOSSIER",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepositExceptionBy",
                table: "TREATMENT_DOSSIER",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepositExceptionReason",
                table: "TREATMENT_DOSSIER",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DisplayCompleted",
                table: "TREATMENT_DOSSIER",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Goals",
                table: "TREATMENT_DOSSIER",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTermsJson",
                table: "TREATMENT_DOSSIER",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<Guid>(
                name: "PlanId",
                table: "TREATMENT_DOSSIER",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<string>(
                name: "PlanState",
                table: "TREATMENT_DOSSIER",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<int>(
                name: "PlanVersion",
                table: "TREATMENT_DOSSIER",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RevisionDrafts",
                table: "TREATMENT_DOSSIER",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceExaminationId",
                table: "TREATMENT_DOSSIER",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceExaminationJson",
                table: "TREATMENT_DOSSIER",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceExaminationVersion",
                table: "TREATMENT_DOSSIER",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceTemplateId",
                table: "TREATMENT_DOSSIER",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceTemplateVersion",
                table: "TREATMENT_DOSSIER",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StorageStamp",
                table: "TREATMENT_DOSSIER",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.Sql("""
                UPDATE "TREATMENT_DOSSIER" d
                SET "PlanId" = d."Id",
                    "PlanVersion" = COALESCE((SELECT MAX(i."DecisionPlanVersion") FROM "TREATMENT_PLAN_ITEM" i WHERE i."DossierId" = d."Id"), 0),
                    "PlanState" = CASE
                        WHEN EXISTS (SELECT 1 FROM "TREATMENT_PLAN_ITEM" i WHERE i."DossierId" = d."Id" AND i."DecisionPlanVersion" IS NOT NULL)
                         AND NOT EXISTS (SELECT 1 FROM "TREATMENT_PLAN_ITEM" i WHERE i."DossierId" = d."Id" AND NOT i."SheetDeleted" AND i."DecisionPlanVersion" IS NULL)
                            THEN 'Committed'
                        WHEN EXISTS (SELECT 1 FROM "TREATMENT_PLAN_ITEM" i WHERE i."DossierId" = d."Id")
                            THEN 'Draft'
                        ELSE 'None'
                    END,
                    "SourceExaminationId" = d."SourceExaminationVisitId",
                    "DisplayCompleted" = (d."State" = 'Completed');

                UPDATE "TREATMENT_DOSSIER" d
                SET "ClosureOutcome" = sub.outcome,
                    "ClosureReason" = sub.reason,
                    "ClosedAt" = sub."OccurredAt",
                    "ClosedBy" = sub."EventActorId"
                FROM (
                    SELECT DISTINCT ON (a."DossierId")
                        a."DossierId",
                        a."OccurredAt",
                        a."EventActorId",
                        COALESCE(a."PayloadJson"->>'outcome', a."PayloadJson"->>'Outcome') AS outcome,
                        COALESCE(a."PayloadJson"->>'reason', a."PayloadJson"->>'Reason') AS reason
                    FROM "AUDIT_LOG" a
                    WHERE a."AuditType" = 'TreatmentEvent' AND a."Kind" = 'DossierClosed'
                    ORDER BY a."DossierId", a."OccurredAt" DESC
                ) sub
                WHERE d."Id" = sub."DossierId" AND d."State" IN ('Completed', 'Stopped');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TREATMENT_DOSSIER_SourceTemplateId",
                table: "TREATMENT_DOSSIER",
                column: "SourceTemplateId");
        }
    }
}
