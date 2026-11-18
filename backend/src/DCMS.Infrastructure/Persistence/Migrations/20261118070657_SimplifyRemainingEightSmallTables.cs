using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyRemainingEightSmallTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "MATERIAL_USAGE" AS u
                WHERE NOT EXISTS (SELECT 1 FROM "CLINICAL_ACTIVITY" AS a WHERE a."Id" = u."ActivityId");

                DELETE FROM "TREATMENT_TEMPLATE"
                WHERE "DefinitionKind" <> 'ItemPlan';

                UPDATE "ACCESS_GROUP"
                SET "Grants" = COALESCE((
                    SELECT jsonb_agg(jsonb_build_object(
                        'GroupId', "Id",
                        'Permission', COALESCE(elem->>'Type', ''),
                        'ClaimId', (elem->>'Id')::integer,
                        'ClaimValue', elem->>'Value'
                    ))
                    FROM jsonb_array_elements("RoleClaims") AS elem
                ), '[]'::jsonb)
                WHERE "RecordKind" = 'IdentityRole' AND jsonb_array_length("RoleClaims") > 0;
                """);

            migrationBuilder.DropIndex(
                name: "IX_REFERENCE_ENTRY_Kind_NormalizedName",
                table: "REFERENCE_ENTRY");

            migrationBuilder.DropIndex(
                name: "IX_MATERIAL_CATEGORY_NormalizedName",
                table: "MATERIAL_CATEGORY");

            migrationBuilder.DropIndex(
                name: "IX_MATERIAL_CATALOG_ITEM_CategoryId_NormalizedName",
                table: "MATERIAL_CATALOG_ITEM");

            migrationBuilder.DropIndex(
                name: "IX_ACCESS_GROUP_RecordKind_NormalizedCode",
                table: "ACCESS_GROUP");

            migrationBuilder.DropIndex(
                name: "IX_ACCESS_GROUP_RecordKind_NormalizedName",
                table: "ACCESS_GROUP");

            migrationBuilder.DropColumn(
                name: "DefinitionKind",
                table: "TREATMENT_TEMPLATE");

            migrationBuilder.DropColumn(
                name: "StorageStamp",
                table: "TREATMENT_TEMPLATE");

            migrationBuilder.DropColumn(
                name: "Versions",
                table: "TREATMENT_TEMPLATE");

            migrationBuilder.DropColumn(
                name: "BillingUnit",
                table: "SCHEDULING_SERVICE");

            migrationBuilder.DropColumn(
                name: "InvestigationKind",
                table: "SCHEDULING_SERVICE");

            migrationBuilder.DropColumn(
                name: "IsInvestigationService",
                table: "SCHEDULING_SERVICE");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "REFERENCE_ENTRY");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "REFERENCE_ENTRY");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "REFERENCE_ENTRY");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "REFERENCE_ENTRY");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "REFERENCE_ENTRY");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "MATERIAL_USAGE");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "MATERIAL_USAGE");

            migrationBuilder.DropColumn(
                name: "DossierId",
                table: "MATERIAL_USAGE");

            migrationBuilder.DropColumn(
                name: "MaterialName",
                table: "MATERIAL_USAGE");

            migrationBuilder.DropColumn(
                name: "PatientId",
                table: "MATERIAL_USAGE");

            migrationBuilder.DropColumn(
                name: "UsageUnit",
                table: "MATERIAL_USAGE");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "MATERIAL_CATEGORY");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "MATERIAL_CATEGORY");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "MATERIAL_CATEGORY");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "MATERIAL_CATALOG_ITEM");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "MATERIAL_CATALOG_ITEM");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "MATERIAL_CATALOG_ITEM");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CUSTOMER_FAMILY_MEMBER");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CUSTOMER_FAMILY_MEMBER");

            migrationBuilder.DropColumn(
                name: "NormalizedCode",
                table: "ACCESS_GROUP");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "ACCESS_GROUP");

            migrationBuilder.DropColumn(
                name: "RoleClaims",
                table: "ACCESS_GROUP");

            migrationBuilder.DropColumn(
                name: "RoleConcurrencyStamp",
                table: "ACCESS_GROUP");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "TREATMENT_TEMPLATE",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Grants",
                table: "ACCESS_GROUP",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb",
                oldClrType: typeof(string),
                oldType: "jsonb");

            migrationBuilder.CreateIndex(
                name: "IX_MATERIAL_CATALOG_ITEM_CategoryId",
                table: "MATERIAL_CATALOG_ITEM",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_MATERIAL_USAGE_CLINICAL_ACTIVITY_ActivityId",
                table: "MATERIAL_USAGE",
                column: "ActivityId",
                principalTable: "CLINICAL_ACTIVITY",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_REFERENCE_ENTRY_Kind_Name_Upper" ON "REFERENCE_ENTRY" ("Kind", UPPER("Name"));
                CREATE UNIQUE INDEX "IX_ACCESS_GROUP_RecordKind_Code_Upper" ON "ACCESS_GROUP" ("RecordKind", UPPER("Code"));
                CREATE UNIQUE INDEX "IX_ACCESS_GROUP_RecordKind_Name_Upper" ON "ACCESS_GROUP" ("RecordKind", UPPER("Name"));
                CREATE UNIQUE INDEX "IX_MATERIAL_CATEGORY_Name_Upper" ON "MATERIAL_CATEGORY" (UPPER("Name")) WHERE NOT "IsDeleted";
                CREATE UNIQUE INDEX "IX_MATERIAL_CATALOG_ITEM_CategoryId_Name_Upper" ON "MATERIAL_CATALOG_ITEM" ("CategoryId", UPPER("Name")) WHERE NOT "IsDeleted";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_REFERENCE_ENTRY_Kind_Name_Upper";
                DROP INDEX IF EXISTS "IX_ACCESS_GROUP_RecordKind_Code_Upper";
                DROP INDEX IF EXISTS "IX_ACCESS_GROUP_RecordKind_Name_Upper";
                DROP INDEX IF EXISTS "IX_MATERIAL_CATEGORY_Name_Upper";
                DROP INDEX IF EXISTS "IX_MATERIAL_CATALOG_ITEM_CategoryId_Name_Upper";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_MATERIAL_USAGE_CLINICAL_ACTIVITY_ActivityId",
                table: "MATERIAL_USAGE");

            migrationBuilder.DropIndex(
                name: "IX_MATERIAL_CATALOG_ITEM_CategoryId",
                table: "MATERIAL_CATALOG_ITEM");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "TREATMENT_TEMPLATE",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "DefinitionKind",
                table: "TREATMENT_TEMPLATE",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "LegacySteps");

            migrationBuilder.AddColumn<Guid>(
                name: "StorageStamp",
                table: "TREATMENT_TEMPLATE",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Versions",
                table: "TREATMENT_TEMPLATE",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "BillingUnit",
                table: "SCHEDULING_SERVICE",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InvestigationKind",
                table: "SCHEDULING_SERVICE",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInvestigationService",
                table: "SCHEDULING_SERVICE",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "REFERENCE_ENTRY",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "REFERENCE_ENTRY",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "REFERENCE_ENTRY",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "REFERENCE_ENTRY",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "REFERENCE_ENTRY",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "MATERIAL_USAGE",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "MATERIAL_USAGE",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "DossierId",
                table: "MATERIAL_USAGE",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaterialName",
                table: "MATERIAL_USAGE",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PatientId",
                table: "MATERIAL_USAGE",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UsageUnit",
                table: "MATERIAL_USAGE",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "MATERIAL_CATEGORY",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "MATERIAL_CATEGORY",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "MATERIAL_CATEGORY",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "MATERIAL_CATALOG_ITEM",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "MATERIAL_CATALOG_ITEM",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "MATERIAL_CATALOG_ITEM",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "CUSTOMER_FAMILY_MEMBER",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "CUSTOMER_FAMILY_MEMBER",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AlterColumn<string>(
                name: "Grants",
                table: "ACCESS_GROUP",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldDefaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedCode",
                table: "ACCESS_GROUP",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "ACCESS_GROUP",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RoleClaims",
                table: "ACCESS_GROUP",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "RoleConcurrencyStamp",
                table: "ACCESS_GROUP",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "ACCESS_GROUP"
                SET "NormalizedCode" = UPPER("Code"),
                    "NormalizedName" = UPPER("Name"),
                    "RoleConcurrencyStamp" = CASE WHEN "RecordKind" = 'IdentityRole' THEN "Version"::text ELSE NULL END,
                    "RoleClaims" = CASE
                        WHEN "RecordKind" = 'IdentityRole' AND jsonb_array_length("Grants") > 0 THEN COALESCE((
                            SELECT jsonb_agg(jsonb_build_object(
                                'Id', COALESCE((elem->>'ClaimId')::integer, 0),
                                'Type', elem->>'Permission',
                                'Value', elem->>'ClaimValue'
                            ))
                            FROM jsonb_array_elements("Grants") AS elem
                        ), '[]'::jsonb)
                        ELSE '[]'::jsonb
                    END,
                    "Grants" = CASE WHEN "RecordKind" = 'IdentityRole' THEN '[]'::jsonb ELSE "Grants" END;

                UPDATE "TREATMENT_TEMPLATE"
                SET "DefinitionKind" = 'ItemPlan',
                    "StorageStamp" = gen_random_uuid(),
                    "Versions" = '[]'::jsonb;

                UPDATE "SCHEDULING_SERVICE"
                SET "BillingUnit" = "Unit";

                UPDATE "REFERENCE_ENTRY"
                SET "NormalizedName" = UPPER("Name"),
                    "CreatedAt" = NOW(),
                    "UpdatedAt" = NOW();

                UPDATE "MATERIAL_CATEGORY"
                SET "NormalizedName" = UPPER("Name"),
                    "CreatedAt" = NOW(),
                    "UpdatedAt" = NOW();

                UPDATE "MATERIAL_CATALOG_ITEM"
                SET "NormalizedName" = UPPER("Name"),
                    "CreatedAt" = NOW(),
                    "UpdatedAt" = NOW();

                UPDATE "MATERIAL_USAGE" AS u
                SET "MaterialName" = m."Name",
                    "UsageUnit" = m."UsageUnit",
                    "DossierId" = a."DossierId",
                    "PatientId" = d."PatientId",
                    "BranchId" = d."BranchId",
                    "CreatedAt" = u."UsedAt"
                FROM "MATERIAL_CATALOG_ITEM" AS m,
                     "CLINICAL_ACTIVITY" AS a
                     JOIN "TREATMENT_DOSSIER" AS d ON d."Id" = a."DossierId"
                WHERE m."Id" = u."MaterialId" AND a."Id" = u."ActivityId";

                UPDATE "CUSTOMER_FAMILY_MEMBER"
                SET "CreatedAt" = NOW(),
                    "UpdatedAt" = NOW();
                """);

            migrationBuilder.CreateIndex(
                name: "IX_REFERENCE_ENTRY_Kind_NormalizedName",
                table: "REFERENCE_ENTRY",
                columns: new[] { "Kind", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MATERIAL_CATEGORY_NormalizedName",
                table: "MATERIAL_CATEGORY",
                column: "NormalizedName",
                unique: true,
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_MATERIAL_CATALOG_ITEM_CategoryId_NormalizedName",
                table: "MATERIAL_CATALOG_ITEM",
                columns: new[] { "CategoryId", "NormalizedName" },
                unique: true,
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_ACCESS_GROUP_RecordKind_NormalizedCode",
                table: "ACCESS_GROUP",
                columns: new[] { "RecordKind", "NormalizedCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACCESS_GROUP_RecordKind_NormalizedName",
                table: "ACCESS_GROUP",
                columns: new[] { "RecordKind", "NormalizedName" },
                unique: true);
        }
    }
}
