using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    // Optional lot number per purchase row (docs/specs/MATERIAL-PURCHASES.md). Additive nullable column.
    [DbContext(typeof(DcmsDbContext))]
    [Migration("20261106120000_MaterialPurchaseLot")]
    public partial class MaterialPurchaseLot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.AddColumn<string>(name: "LotNumber", table: "MATERIAL_PURCHASE", type: "character varying(60)", maxLength: 60, nullable: true);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropColumn(name: "LotNumber", table: "MATERIAL_PURCHASE");
    }
}
