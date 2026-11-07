using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    // Optional supplier per purchase row (docs/specs/MATERIAL-PURCHASES.md). Additive nullable columns; existing rows stay empty.
    [DbContext(typeof(DcmsDbContext))]
    [Migration("20261105150000_MaterialPurchaseSupplier")]
    public partial class MaterialPurchaseSupplier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(name: "SupplierId", table: "MATERIAL_PURCHASE", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<string>(name: "SupplierName", table: "MATERIAL_PURCHASE", type: "character varying(160)", maxLength: 160, nullable: true);
            migrationBuilder.CreateIndex(name: "IX_MATERIAL_PURCHASE_SupplierId", table: "MATERIAL_PURCHASE", column: "SupplierId");
            migrationBuilder.AddForeignKey(name: "FK_MATERIAL_PURCHASE_CATALOG_PROVIDER_SupplierId", table: "MATERIAL_PURCHASE", column: "SupplierId",
                principalTable: "CATALOG_PROVIDER", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_MATERIAL_PURCHASE_CATALOG_PROVIDER_SupplierId", table: "MATERIAL_PURCHASE");
            migrationBuilder.DropIndex(name: "IX_MATERIAL_PURCHASE_SupplierId", table: "MATERIAL_PURCHASE");
            migrationBuilder.DropColumn(name: "SupplierName", table: "MATERIAL_PURCHASE");
            migrationBuilder.DropColumn(name: "SupplierId", table: "MATERIAL_PURCHASE");
        }
    }
}
