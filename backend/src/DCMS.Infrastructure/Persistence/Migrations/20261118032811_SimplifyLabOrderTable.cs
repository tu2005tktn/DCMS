using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyLabOrderTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LAB_ORDER_BranchId_Code",
                table: "LAB_ORDER");

            migrationBuilder.DropIndex(
                name: "IX_LAB_ORDER_BranchId_Status_DueOn",
                table: "LAB_ORDER");

            migrationBuilder.DropIndex(
                name: "IX_LAB_ORDER_DossierId",
                table: "LAB_ORDER");

            migrationBuilder.AddColumn<string>(
                name: "Spec",
                table: "LAB_ORDER",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "LAB_ORDER",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.Sql("""
                UPDATE "LAB_ORDER"
                SET "Spec" = jsonb_build_object(
                        'MarginOuter', "MarginOuter",
                        'MarginInner', "MarginInner",
                        'MarginSide', "MarginSide",
                        'BridgeShapes', COALESCE("BridgeShapes", '[]'::jsonb),
                        'IsChild', COALESCE("IsChild", false),
                        'Teeth', COALESCE("Teeth", '[]'::jsonb),
                        'Shade', COALESCE("Shade", '{}'::jsonb),
                        'Material', "Material",
                        'Warranty', "Warranty",
                        'SentOn', "SentOn",
                        'ReceivedOn', "ReceivedOn"
                    ),
                    "Version" = GREATEST("Version", 1);
                """);

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "BridgeShapes",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "DossierId",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "IsChild",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "MarginInner",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "MarginOuter",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "MarginSide",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "Material",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "PatientId",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "ReceivedOn",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "SentOn",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "Shade",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "StorageStamp",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "Teeth",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "Warranty",
                table: "LAB_ORDER");

            migrationBuilder.CreateIndex(
                name: "IX_LAB_ORDER_Code",
                table: "LAB_ORDER",
                column: "Code",
                filter: "\"Code\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LAB_ORDER_Status_DueOn",
                table: "LAB_ORDER",
                columns: new[] { "Status", "DueOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LAB_ORDER_Code",
                table: "LAB_ORDER");

            migrationBuilder.DropIndex(
                name: "IX_LAB_ORDER_Status_DueOn",
                table: "LAB_ORDER");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "LAB_ORDER",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "LAB_ORDER",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BridgeShapes",
                table: "LAB_ORDER",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "LAB_ORDER",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "LAB_ORDER",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "DossierId",
                table: "LAB_ORDER",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsChild",
                table: "LAB_ORDER",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MarginInner",
                table: "LAB_ORDER",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MarginOuter",
                table: "LAB_ORDER",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MarginSide",
                table: "LAB_ORDER",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Material",
                table: "LAB_ORDER",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PatientId",
                table: "LAB_ORDER",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReceivedOn",
                table: "LAB_ORDER",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SentOn",
                table: "LAB_ORDER",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Shade",
                table: "LAB_ORDER",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<Guid>(
                name: "StorageStamp",
                table: "LAB_ORDER",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<string>(
                name: "Teeth",
                table: "LAB_ORDER",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "LAB_ORDER",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "LAB_ORDER",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Warranty",
                table: "LAB_ORDER",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "LAB_ORDER" o
                SET "DossierId" = a."DossierId",
                    "PatientId" = d."PatientId",
                    "BranchId" = d."BranchId",
                    "MarginOuter" = o."Spec"->>'MarginOuter',
                    "MarginInner" = o."Spec"->>'MarginInner',
                    "MarginSide" = o."Spec"->>'MarginSide',
                    "BridgeShapes" = CASE WHEN jsonb_typeof(o."Spec"->'BridgeShapes') = 'array' THEN o."Spec"->'BridgeShapes' ELSE '[]'::jsonb END,
                    "IsChild" = COALESCE((o."Spec"->>'IsChild')::boolean, false),
                    "Teeth" = CASE WHEN jsonb_typeof(o."Spec"->'Teeth') = 'array' THEN o."Spec"->'Teeth' ELSE '[]'::jsonb END,
                    "Shade" = CASE WHEN jsonb_typeof(o."Spec"->'Shade') = 'object' THEN o."Spec"->'Shade' ELSE '{}'::jsonb END,
                    "Material" = o."Spec"->>'Material',
                    "Warranty" = o."Spec"->>'Warranty',
                    "SentOn" = NULLIF(o."Spec"->>'SentOn', '')::date,
                    "ReceivedOn" = NULLIF(o."Spec"->>'ReceivedOn', '')::date,
                    "CreatedAt" = COALESCE(
                        (SELECT (l.elem->>'ChangedAt')::timestamptz FROM jsonb_array_elements(o."StatusLogs") AS l(elem) ORDER BY (l.elem->>'ChangedAt')::timestamptz ASC LIMIT 1),
                        a."CreatedAt"),
                    "CreatedBy" = COALESCE(
                        (SELECT (l.elem->>'ChangedBy')::integer FROM jsonb_array_elements(o."StatusLogs") AS l(elem) ORDER BY (l.elem->>'ChangedAt')::timestamptz ASC LIMIT 1),
                        a."PerformerId",
                        o."DentistId"),
                    "UpdatedAt" = COALESCE(
                        (SELECT (l.elem->>'ChangedAt')::timestamptz FROM jsonb_array_elements(o."StatusLogs") AS l(elem) ORDER BY (l.elem->>'ChangedAt')::timestamptz DESC LIMIT 1),
                        a."CreatedAt"),
                    "UpdatedBy" = COALESCE(
                        (SELECT (l.elem->>'ChangedBy')::integer FROM jsonb_array_elements(o."StatusLogs") AS l(elem) ORDER BY (l.elem->>'ChangedAt')::timestamptz DESC LIMIT 1),
                        a."PerformerId",
                        o."DentistId")
                FROM "CLINICAL_ACTIVITY" a
                JOIN "TREATMENT_DOSSIER" d ON d."Id" = a."DossierId"
                WHERE a."Id" = o."ActivityId";
                """);

            migrationBuilder.DropColumn(
                name: "Spec",
                table: "LAB_ORDER");

            migrationBuilder.CreateIndex(
                name: "IX_LAB_ORDER_BranchId_Code",
                table: "LAB_ORDER",
                columns: new[] { "BranchId", "Code" },
                unique: true,
                filter: "\"Code\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LAB_ORDER_BranchId_Status_DueOn",
                table: "LAB_ORDER",
                columns: new[] { "BranchId", "Status", "DueOn" });

            migrationBuilder.CreateIndex(
                name: "IX_LAB_ORDER_DossierId",
                table: "LAB_ORDER",
                column: "DossierId");
        }
    }
}
