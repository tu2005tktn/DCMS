using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyPrescriptionAndMedicineTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PRESCRIPTION_PRESCRIPTION_SupersedesId",
                table: "PRESCRIPTION");

            migrationBuilder.DropForeignKey(
                name: "FK_PRESCRIPTION_USER_ACCOUNT_CreatedBy",
                table: "PRESCRIPTION");

            migrationBuilder.DropForeignKey(
                name: "FK_PRESCRIPTION_USER_ACCOUNT_FinalizedBy",
                table: "PRESCRIPTION");

            migrationBuilder.DropForeignKey(
                name: "FK_PRESCRIPTION_USER_ACCOUNT_PrescribedByDoctorId",
                table: "PRESCRIPTION");

            migrationBuilder.DropForeignKey(
                name: "FK_PRESCRIPTION_USER_ACCOUNT_UpdatedBy",
                table: "PRESCRIPTION");

            migrationBuilder.DropForeignKey(
                name: "FK_PRESCRIPTION_VISIT_VisitId",
                table: "PRESCRIPTION");

            migrationBuilder.DropIndex(
                name: "IX_PRESCRIPTION_Code",
                table: "PRESCRIPTION");

            migrationBuilder.DropIndex(
                name: "IX_PRESCRIPTION_CreatedBy",
                table: "PRESCRIPTION");

            migrationBuilder.DropIndex(
                name: "IX_PRESCRIPTION_FinalizedBy",
                table: "PRESCRIPTION");

            migrationBuilder.DropIndex(
                name: "IX_PRESCRIPTION_PatientId_Status",
                table: "PRESCRIPTION");

            migrationBuilder.DropIndex(
                name: "IX_PRESCRIPTION_PrescribedByDoctorId",
                table: "PRESCRIPTION");

            migrationBuilder.DropIndex(
                name: "IX_PRESCRIPTION_SupersedesId",
                table: "PRESCRIPTION");

            migrationBuilder.DropIndex(
                name: "IX_PRESCRIPTION_UpdatedBy",
                table: "PRESCRIPTION");

            migrationBuilder.DropIndex(
                name: "IX_PRESCRIPTION_VisitId_Revision",
                table: "PRESCRIPTION");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PRESCRIPTION_VISIT_REVISION",
                table: "PRESCRIPTION");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PRESCRIPTION_VISIT_STATUS",
                table: "PRESCRIPTION");

            migrationBuilder.DropIndex(
                name: "IX_CATALOG_MEDICINE_NormalizedName",
                table: "CATALOG_MEDICINE");

            migrationBuilder.AddColumn<string>(
                name: "Spec",
                table: "CATALOG_MEDICINE",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.Sql("""
                DELETE FROM "COMMAND_RECEIPT"
                WHERE "PrescriptionId" IN (
                    SELECT "Id" FROM "PRESCRIPTION" WHERE "PrescriptionKind" <> 'PatientPrescription'
                );

                DELETE FROM "PRESCRIPTION"
                WHERE "PrescriptionKind" <> 'PatientPrescription';

                UPDATE "PRESCRIPTION"
                SET "Diagnosis" = COALESCE("Diagnosis", ''),
                    "Version" = GREATEST(COALESCE("Version", 1), 1),
                    "CreatedAt" = COALESCE("CreatedAt", now())
                WHERE "Diagnosis" IS NULL OR "Version" IS NULL OR "Version" < 1 OR "CreatedAt" IS NULL;

                UPDATE "CATALOG_MEDICINE"
                SET "Spec" = jsonb_build_object(
                        'ActiveIngredient', "ActiveIngredient",
                        'Strength', "Strength",
                        'DosageForm', "DosageForm",
                        'Packaging', "Packaging",
                        'Instructions', "Instructions"
                    ),
                    "Version" = GREATEST("Version", 1);
                """);

            migrationBuilder.DropColumn(
                name: "Code",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "CorrectionReason",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "DiagnosisKind",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "FinalizedBy",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "GeneralAdvice",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "PrescribedByDoctorId",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "PrescriptionKind",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "RequiresExternalPurchase",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "SupersedesId",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "TemplateLines",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "VerificationCode",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "VisitId",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "VisitLines",
                table: "PRESCRIPTION");

            migrationBuilder.DropColumn(
                name: "ActiveIngredient",
                table: "CATALOG_MEDICINE");

            migrationBuilder.DropColumn(
                name: "AllergyWarnings",
                table: "CATALOG_MEDICINE");

            migrationBuilder.DropColumn(
                name: "Contraindications",
                table: "CATALOG_MEDICINE");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CATALOG_MEDICINE");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CATALOG_MEDICINE");

            migrationBuilder.DropColumn(
                name: "DosageForm",
                table: "CATALOG_MEDICINE");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "CATALOG_MEDICINE");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "CATALOG_MEDICINE");

            migrationBuilder.DropColumn(
                name: "Packaging",
                table: "CATALOG_MEDICINE");

            migrationBuilder.DropColumn(
                name: "Route",
                table: "CATALOG_MEDICINE");

            migrationBuilder.DropColumn(
                name: "Strength",
                table: "CATALOG_MEDICINE");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CATALOG_MEDICINE");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "CATALOG_MEDICINE");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "PRESCRIPTION",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PrescriberEmployeeId",
                table: "PRESCRIPTION",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "PrescribedOn",
                table: "PRESCRIPTION",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PatientId",
                table: "PRESCRIPTION",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Diagnosis",
                table: "PRESCRIPTION",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "PRESCRIPTION",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BranchId",
                table: "PRESCRIPTION",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "CATALOG_MEDICINE",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_CATALOG_MEDICINE_Name_Upper"
                ON "CATALOG_MEDICINE" (UPPER("Name"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_CATALOG_MEDICINE_Name_Upper";
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "PRESCRIPTION",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<int>(
                name: "PrescriberEmployeeId",
                table: "PRESCRIPTION",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "PrescribedOn",
                table: "PRESCRIPTION",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<int>(
                name: "PatientId",
                table: "PRESCRIPTION",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Diagnosis",
                table: "PRESCRIPTION",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "PRESCRIPTION",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<int>(
                name: "BranchId",
                table: "PRESCRIPTION",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "PRESCRIPTION",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectionReason",
                table: "PRESCRIPTION",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "PRESCRIPTION",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "PRESCRIPTION",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiagnosisKind",
                table: "PRESCRIPTION",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FinalizedAt",
                table: "PRESCRIPTION",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FinalizedBy",
                table: "PRESCRIPTION",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeneralAdvice",
                table: "PRESCRIPTION",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "PRESCRIPTION",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "PRESCRIPTION",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrescribedByDoctorId",
                table: "PRESCRIPTION",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrescriptionKind",
                table: "PRESCRIPTION",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "RequiresExternalPurchase",
                table: "PRESCRIPTION",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "PRESCRIPTION",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PRESCRIPTION",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersedesId",
                table: "PRESCRIPTION",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateLines",
                table: "PRESCRIPTION",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "PRESCRIPTION",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "PRESCRIPTION",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationCode",
                table: "PRESCRIPTION",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VisitId",
                table: "PRESCRIPTION",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisitLines",
                table: "PRESCRIPTION",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "CATALOG_MEDICINE",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ActiveIngredient",
                table: "CATALOG_MEDICINE",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllergyWarnings",
                table: "CATALOG_MEDICINE",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Contraindications",
                table: "CATALOG_MEDICINE",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "CATALOG_MEDICINE",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "CATALOG_MEDICINE",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DosageForm",
                table: "CATALOG_MEDICINE",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "CATALOG_MEDICINE",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "CATALOG_MEDICINE",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Packaging",
                table: "CATALOG_MEDICINE",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Route",
                table: "CATALOG_MEDICINE",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Uống");

            migrationBuilder.AddColumn<string>(
                name: "Strength",
                table: "CATALOG_MEDICINE",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "CATALOG_MEDICINE",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "CATALOG_MEDICINE",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "PRESCRIPTION"
                SET "PrescriptionKind" = 'PatientPrescription',
                    "UpdatedAt" = "CreatedAt",
                    "RequiresExternalPurchase" = true;

                UPDATE "CATALOG_MEDICINE" m
                SET "NormalizedName" = UPPER(m."Name"),
                    "ActiveIngredient" = m."Spec"->>'ActiveIngredient',
                    "Strength" = m."Spec"->>'Strength',
                    "DosageForm" = m."Spec"->>'DosageForm',
                    "Packaging" = m."Spec"->>'Packaging',
                    "Instructions" = m."Spec"->>'Instructions',
                    "Route" = COALESCE(NULLIF(m."Route", ''), 'Uống'),
                    "CreatedBy" = CASE WHEN m."CreatedBy" = 0 THEN COALESCE((SELECT "Id" FROM "USER_ACCOUNT" ORDER BY "Id" LIMIT 1), 1) ELSE m."CreatedBy" END,
                    "UpdatedBy" = CASE WHEN m."UpdatedBy" = 0 THEN COALESCE((SELECT "Id" FROM "USER_ACCOUNT" ORDER BY "Id" LIMIT 1), 1) ELSE m."UpdatedBy" END;
                """);

            migrationBuilder.DropColumn(
                name: "Spec",
                table: "CATALOG_MEDICINE");

            migrationBuilder.CreateIndex(
                name: "IX_PRESCRIPTION_Code",
                table: "PRESCRIPTION",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRESCRIPTION_CreatedBy",
                table: "PRESCRIPTION",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PRESCRIPTION_FinalizedBy",
                table: "PRESCRIPTION",
                column: "FinalizedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PRESCRIPTION_PatientId_Status",
                table: "PRESCRIPTION",
                columns: new[] { "PatientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PRESCRIPTION_PrescribedByDoctorId",
                table: "PRESCRIPTION",
                column: "PrescribedByDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_PRESCRIPTION_SupersedesId",
                table: "PRESCRIPTION",
                column: "SupersedesId");

            migrationBuilder.CreateIndex(
                name: "IX_PRESCRIPTION_UpdatedBy",
                table: "PRESCRIPTION",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PRESCRIPTION_VisitId_Revision",
                table: "PRESCRIPTION",
                columns: new[] { "VisitId", "Revision" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PRESCRIPTION_VISIT_REVISION",
                table: "PRESCRIPTION",
                sql: "\"PrescriptionKind\" <> 'ExternalPrescription' OR \"Revision\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PRESCRIPTION_VISIT_STATUS",
                table: "PRESCRIPTION",
                sql: "\"PrescriptionKind\" <> 'ExternalPrescription' OR \"Status\" IN ('Draft','Finalized','Superseded')");

            migrationBuilder.CreateIndex(
                name: "IX_CATALOG_MEDICINE_NormalizedName",
                table: "CATALOG_MEDICINE",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PRESCRIPTION_PRESCRIPTION_SupersedesId",
                table: "PRESCRIPTION",
                column: "SupersedesId",
                principalTable: "PRESCRIPTION",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PRESCRIPTION_USER_ACCOUNT_CreatedBy",
                table: "PRESCRIPTION",
                column: "CreatedBy",
                principalTable: "USER_ACCOUNT",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PRESCRIPTION_USER_ACCOUNT_FinalizedBy",
                table: "PRESCRIPTION",
                column: "FinalizedBy",
                principalTable: "USER_ACCOUNT",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PRESCRIPTION_USER_ACCOUNT_PrescribedByDoctorId",
                table: "PRESCRIPTION",
                column: "PrescribedByDoctorId",
                principalTable: "USER_ACCOUNT",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PRESCRIPTION_USER_ACCOUNT_UpdatedBy",
                table: "PRESCRIPTION",
                column: "UpdatedBy",
                principalTable: "USER_ACCOUNT",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PRESCRIPTION_VISIT_VisitId",
                table: "PRESCRIPTION",
                column: "VisitId",
                principalTable: "VISIT",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
