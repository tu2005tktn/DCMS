using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    // Purchase log for materials/instruments (docs/specs/MATERIAL-PURCHASES.md). Additive: no seed, no change to existing data.
    [DbContext(typeof(DcmsDbContext))]
    [Migration("20261104160000_MaterialPurchases")]
    public partial class MaterialPurchases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MATERIAL_PURCHASE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    EnteredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UnitPriceVnd = table.Column<long>(type: "bigint", nullable: false),
                    TotalVnd = table.Column<long>(type: "bigint", nullable: false),
                    PaidVnd = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATERIAL_PURCHASE", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MATERIAL_PURCHASE_MATERIAL_CATALOG_ITEM_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "MATERIAL_CATALOG_ITEM",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MATERIAL_PURCHASE_EnteredOn_Id",
                table: "MATERIAL_PURCHASE",
                columns: new[] { "EnteredOn", "Id" },
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_MATERIAL_PURCHASE_MaterialId",
                table: "MATERIAL_PURCHASE",
                column: "MaterialId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MATERIAL_PURCHASE");
        }
    }
}
