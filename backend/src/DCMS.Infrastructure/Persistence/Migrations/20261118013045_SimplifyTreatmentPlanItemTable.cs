using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyTreatmentPlanItemTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TREATMENT_ITEM_DURATION_V23",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropIndex(
                name: "IX_CLINICAL_ACTIVITY_ItemId_PlanVersion",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.AlterColumn<Guid>(
                name: "ItemId",
                table: "CLINICAL_ACTIVITY",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("""
                UPDATE "CLINICAL_ACTIVITY" a
                SET "ServiceNameSnapshot" = i."ServiceName"
                FROM "TREATMENT_PLAN_ITEM" i
                WHERE a."ItemId" = i."Id" AND (a."ServiceNameSnapshot" IS NULL OR btrim(a."ServiceNameSnapshot") = '');

                UPDATE "CLINICAL_ACTIVITY" a
                SET "ItemId" = NULL, "IsPlanTransfer" = FALSE
                WHERE a."ItemId" IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM "TREATMENT_PLAN_ITEM" i
                      WHERE i."Id" = a."ItemId"
                        AND NOT i."SheetDeleted" AND NOT i."HiddenFromSheet"
                        AND i."Decision" NOT IN ('Refused', 'Deferred')
                        AND (i."DecisionPlanVersion" IS NOT NULL OR i."EntrySource" = 'ClinicianEntry')
                  );

                DELETE FROM "TREATMENT_PLAN_ITEM"
                WHERE "SheetDeleted" OR "HiddenFromSheet"
                   OR "Decision" IN ('Refused', 'Deferred')
                   OR ("DecisionPlanVersion" IS NULL AND COALESCE("EntrySource", '') <> 'ClinicianEntry');

                UPDATE "TREATMENT_PLAN_ITEM"
                SET "DiscountType" = NULL, "DiscountValue" = NULL, "DiscountReason" = NULL
                WHERE "DiscountStatus" IS DISTINCT FROM 'Approved';
                """);

            migrationBuilder.DropColumn(
                name: "ConsentDocumentId",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "ConsentPlanVersion",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "Counseling",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "DecidedAt",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "DecidedBy",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "Decision",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "DecisionMaker",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "DecisionPlanVersion",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "DiscountBasisHash",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "DiscountDecidedAt",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "DiscountDecidedBy",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "DiscountQualifyingItemId",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "DiscountStatus",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "EntryRecordedAt",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "EntryRecordedBy",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "EntrySource",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "EstimatedEnd",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "EstimatedMaxMinutes",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "EstimatedMinMinutes",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "EstimatedSessions",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "ExpectedServiceLifeMonths",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "HiddenFromSheet",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "Material",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "Progress",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "ReferencePriceVersion",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "ReferenceUnitPriceVnd",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "RepresentativeBasis",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "ReservedMinutes",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "ReturnEarliestOn",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "ReturnLatestOn",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "ReturnTargetOn",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "SheetDeleted",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "ShortReservationAcknowledgedAt",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "ShortReservationAcknowledgedBy",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "SourceDiagnosisId",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "Steps",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "StorageStamp",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "WarrantyActivatedOn",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "WarrantyComponent",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "WarrantyConfirmedAt",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "WarrantyConfirmedBy",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "WarrantyEndsOn",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "WarrantyExclusions",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "WarrantyExplanation",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "WarrantyPolicy",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "WarrantyPolicyVersion",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.DropColumn(
                name: "WarrantyScope",
                table: "TREATMENT_PLAN_ITEM");

            migrationBuilder.CreateIndex(
                name: "IX_CLINICAL_ACTIVITY_ItemId_PlanVersion",
                table: "CLINICAL_ACTIVITY",
                columns: new[] { "ItemId", "PlanVersion" },
                unique: true,
                filter: "\"IsPlanTransfer\" = TRUE AND \"ItemId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_CLINICAL_ACTIVITY_TREATMENT_PLAN_ITEM_ItemId",
                table: "CLINICAL_ACTIVITY",
                column: "ItemId",
                principalTable: "TREATMENT_PLAN_ITEM",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CLINICAL_ACTIVITY_TREATMENT_PLAN_ITEM_ItemId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropIndex(
                name: "IX_CLINICAL_ACTIVITY_ItemId_PlanVersion",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.AddColumn<Guid>(
                name: "ConsentDocumentId",
                table: "TREATMENT_PLAN_ITEM",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConsentPlanVersion",
                table: "TREATMENT_PLAN_ITEM",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Counseling",
                table: "TREATMENT_PLAN_ITEM",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecidedAt",
                table: "TREATMENT_PLAN_ITEM",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DecidedBy",
                table: "TREATMENT_PLAN_ITEM",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Decision",
                table: "TREATMENT_PLAN_ITEM",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DecisionMaker",
                table: "TREATMENT_PLAN_ITEM",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DecisionPlanVersion",
                table: "TREATMENT_PLAN_ITEM",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountBasisHash",
                table: "TREATMENT_PLAN_ITEM",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DiscountDecidedAt",
                table: "TREATMENT_PLAN_ITEM",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscountDecidedBy",
                table: "TREATMENT_PLAN_ITEM",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DiscountQualifyingItemId",
                table: "TREATMENT_PLAN_ITEM",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountStatus",
                table: "TREATMENT_PLAN_ITEM",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EntryRecordedAt",
                table: "TREATMENT_PLAN_ITEM",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EntryRecordedBy",
                table: "TREATMENT_PLAN_ITEM",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntrySource",
                table: "TREATMENT_PLAN_ITEM",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EstimatedEnd",
                table: "TREATMENT_PLAN_ITEM",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedMaxMinutes",
                table: "TREATMENT_PLAN_ITEM",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedMinMinutes",
                table: "TREATMENT_PLAN_ITEM",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedSessions",
                table: "TREATMENT_PLAN_ITEM",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpectedServiceLifeMonths",
                table: "TREATMENT_PLAN_ITEM",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HiddenFromSheet",
                table: "TREATMENT_PLAN_ITEM",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Material",
                table: "TREATMENT_PLAN_ITEM",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Progress",
                table: "TREATMENT_PLAN_ITEM",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ReferencePriceVersion",
                table: "TREATMENT_PLAN_ITEM",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ReferenceUnitPriceVnd",
                table: "TREATMENT_PLAN_ITEM",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeBasis",
                table: "TREATMENT_PLAN_ITEM",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReservedMinutes",
                table: "TREATMENT_PLAN_ITEM",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReturnEarliestOn",
                table: "TREATMENT_PLAN_ITEM",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReturnLatestOn",
                table: "TREATMENT_PLAN_ITEM",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReturnTargetOn",
                table: "TREATMENT_PLAN_ITEM",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SheetDeleted",
                table: "TREATMENT_PLAN_ITEM",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ShortReservationAcknowledgedAt",
                table: "TREATMENT_PLAN_ITEM",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShortReservationAcknowledgedBy",
                table: "TREATMENT_PLAN_ITEM",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceDiagnosisId",
                table: "TREATMENT_PLAN_ITEM",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Steps",
                table: "TREATMENT_PLAN_ITEM",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<Guid>(
                name: "StorageStamp",
                table: "TREATMENT_PLAN_ITEM",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateOnly>(
                name: "WarrantyActivatedOn",
                table: "TREATMENT_PLAN_ITEM",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarrantyComponent",
                table: "TREATMENT_PLAN_ITEM",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WarrantyConfirmedAt",
                table: "TREATMENT_PLAN_ITEM",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarrantyConfirmedBy",
                table: "TREATMENT_PLAN_ITEM",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WarrantyEndsOn",
                table: "TREATMENT_PLAN_ITEM",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarrantyExclusions",
                table: "TREATMENT_PLAN_ITEM",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarrantyExplanation",
                table: "TREATMENT_PLAN_ITEM",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarrantyPolicy",
                table: "TREATMENT_PLAN_ITEM",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarrantyPolicyVersion",
                table: "TREATMENT_PLAN_ITEM",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarrantyScope",
                table: "TREATMENT_PLAN_ITEM",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "CLINICAL_ACTIVITY"
                SET "ItemId" = '00000000-0000-0000-0000-000000000000'::uuid
                WHERE "ItemId" IS NULL;

                UPDATE "TREATMENT_PLAN_ITEM"
                SET "Decision" = 'Agreed',
                    "DecisionPlanVersion" = 1,
                    "Progress" = CASE WHEN "DisplayCompleted" THEN 'Completed' ELSE 'NotStarted' END,
                    "DiscountStatus" = CASE WHEN "DiscountType" IS NOT NULL THEN 'Approved' ELSE NULL END,
                    "StorageStamp" = gen_random_uuid();
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "ItemId",
                table: "CLINICAL_ACTIVITY",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TREATMENT_ITEM_DURATION_V23",
                table: "TREATMENT_PLAN_ITEM",
                sql: "(\"EstimatedMinMinutes\" IS NULL AND \"EstimatedMaxMinutes\" IS NULL OR \"EstimatedMinMinutes\" > 0 AND \"EstimatedMinMinutes\" <= \"EstimatedMinutes\" AND \"EstimatedMinutes\" <= \"EstimatedMaxMinutes\" AND \"EstimatedMaxMinutes\" <= 480) AND (\"ReservedMinutes\" IS NULL OR \"ReservedMinutes\" BETWEEN 15 AND 480 AND \"ReservedMinutes\" % 15 = 0 AND \"ReservedMinutes\" >= \"EstimatedMinutes\")");

            migrationBuilder.CreateIndex(
                name: "IX_CLINICAL_ACTIVITY_ItemId_PlanVersion",
                table: "CLINICAL_ACTIVITY",
                columns: new[] { "ItemId", "PlanVersion" },
                unique: true,
                filter: "\"IsPlanTransfer\" = TRUE");
        }
    }
}
