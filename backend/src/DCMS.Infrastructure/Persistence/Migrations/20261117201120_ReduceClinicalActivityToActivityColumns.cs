using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReduceClinicalActivityToActivityColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every activity must already be a recorded activity of a dossier item whose patient and branch match the dossier.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "CLINICAL_ACTIVITY" a
                               LEFT JOIN "TREATMENT_DOSSIER" d ON d."Id" = a."DossierId"
                               WHERE a."DossierId" IS NULL OR a."ItemId" IS NULL OR a."State" <> 'Posted'
                                  OR d."Id" IS NULL OR d."PatientId" <> a."PatientId" OR d."BranchId" <> a."BranchId") THEN
                        RAISE EXCEPTION 'CLINICAL_ACTIVITY has rows that are not recorded activities of a matching treatment dossier';
                    END IF;
                END $$;
                UPDATE "CLINICAL_ACTIVITY" SET "CreatedAt" = "PostedAt" WHERE "PostedAt" IS NOT NULL;
                """);

            migrationBuilder.DropIndex(
                name: "IX_CLINICAL_ACTIVITY_PatientId_DossierId_StepId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropIndex(
                name: "IX_CLINICAL_ACTIVITY_VisitId_State",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "ChargingBasis",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "ChargingBasisEvidence",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "ElapsedMinutes",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "NextSteps",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "PatientId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "PostedAt",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "RecordedBy",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "State",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "StepId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "VisitId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.AlterColumn<Guid>(
                name: "ItemId",
                table: "CLINICAL_ACTIVITY",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "DossierId",
                table: "CLINICAL_ACTIVITY",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CLINICAL_ACTIVITY_DossierId_PerformedAt",
                table: "CLINICAL_ACTIVITY",
                columns: new[] { "DossierId", "PerformedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_CLINICAL_ACTIVITY_TREATMENT_DOSSIER_DossierId",
                table: "CLINICAL_ACTIVITY",
                column: "DossierId",
                principalTable: "TREATMENT_DOSSIER",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CLINICAL_ACTIVITY_TREATMENT_DOSSIER_DossierId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropIndex(
                name: "IX_CLINICAL_ACTIVITY_DossierId_PerformedAt",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.AlterColumn<Guid>(
                name: "ItemId",
                table: "CLINICAL_ACTIVITY",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "DossierId",
                table: "CLINICAL_ACTIVITY",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "CLINICAL_ACTIVITY",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ChargingBasis",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ChargingBasisEvidence",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ElapsedMinutes",
                table: "CLINICAL_ACTIVITY",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextSteps",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PatientId",
                table: "CLINICAL_ACTIVITY",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PostedAt",
                table: "CLINICAL_ACTIVITY",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecordedBy",
                table: "CLINICAL_ACTIVITY",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "StepId",
                table: "CLINICAL_ACTIVITY",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VisitId",
                table: "CLINICAL_ACTIVITY",
                type: "uuid",
                nullable: true);

            // Patient and branch come back from the dossier; removed visit-flow fields stay empty.
            migrationBuilder.Sql("""
                UPDATE "CLINICAL_ACTIVITY" a SET "PatientId" = d."PatientId", "BranchId" = d."BranchId",
                    "State" = 'Posted', "PostedAt" = a."CreatedAt", "ChargingBasis" = 'Separate'
                FROM "TREATMENT_DOSSIER" d WHERE d."Id" = a."DossierId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CLINICAL_ACTIVITY_PatientId_DossierId_StepId",
                table: "CLINICAL_ACTIVITY",
                columns: new[] { "PatientId", "DossierId", "StepId" });

            migrationBuilder.CreateIndex(
                name: "IX_CLINICAL_ACTIVITY_VisitId_State",
                table: "CLINICAL_ACTIVITY",
                columns: new[] { "VisitId", "State" });
        }
    }
}
