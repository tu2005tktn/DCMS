using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyMaterialPurchaseTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CategoryName",
                table: "MATERIAL_PURCHASE");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "MATERIAL_PURCHASE");

            migrationBuilder.DropColumn(
                name: "MaterialName",
                table: "MATERIAL_PURCHASE");

            migrationBuilder.DropColumn(
                name: "SupplierName",
                table: "MATERIAL_PURCHASE");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "MATERIAL_PURCHASE");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "MATERIAL_PURCHASE");

            migrationBuilder.DropColumn(
                name: "UsageUnit",
                table: "MATERIAL_PURCHASE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CategoryName",
                table: "MATERIAL_PURCHASE",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "MATERIAL_PURCHASE",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MaterialName",
                table: "MATERIAL_PURCHASE",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SupplierName",
                table: "MATERIAL_PURCHASE",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "MATERIAL_PURCHASE",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "MATERIAL_PURCHASE",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UsageUnit",
                table: "MATERIAL_PURCHASE",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            // Restore the snapshots from the catalogs and the actors from the purchase receipts
            // (docs/specs/MATERIAL-PURCHASE-TABLE-SIMPLIFICATION.vi.md).
            migrationBuilder.Sql("""
                UPDATE "MATERIAL_PURCHASE" p
                SET "MaterialName" = m."Name", "CategoryName" = c."Name", "UsageUnit" = m."UsageUnit", "UpdatedAt" = p."CreatedAt"
                FROM "MATERIAL_CATALOG_ITEM" m
                JOIN "MATERIAL_CATEGORY" c ON c."Id" = m."CategoryId"
                WHERE m."Id" = p."MaterialId";

                UPDATE "MATERIAL_PURCHASE" p
                SET "SupplierName" = s."Name"
                FROM "CATALOG_PROVIDER" s
                WHERE s."Id" = p."SupplierId";

                UPDATE "MATERIAL_PURCHASE" p
                SET "CreatedBy" = r."ActorId"
                FROM "COMMAND_RECEIPT" r
                WHERE r."ReceiptType" = 'SystemCatalogChange' AND r."Command" = 'PurchaseCreate' AND r."ResourceId" = p."Id";

                UPDATE "MATERIAL_PURCHASE" p
                SET "UpdatedBy" = latest."ActorId", "UpdatedAt" = latest."OccurredAt"
                FROM (
                    SELECT DISTINCT ON (r."ResourceId") r."ResourceId", r."ActorId", r."OccurredAt"
                    FROM "COMMAND_RECEIPT" r
                    WHERE r."ReceiptType" = 'SystemCatalogChange' AND r."Command" IN ('PurchaseCreate', 'PurchaseUpdate', 'PurchaseDelete')
                    ORDER BY r."ResourceId", r."OccurredAt" DESC
                ) latest
                WHERE latest."ResourceId" = p."Id";
                """);
        }
    }
}
