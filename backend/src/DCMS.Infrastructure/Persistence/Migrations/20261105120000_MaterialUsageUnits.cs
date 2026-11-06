using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    // Usage unit on the catalog and conversion factor on purchase rows (docs/specs/MATERIAL-PURCHASES.md). Additive; existing rows keep factor 1.
    [DbContext(typeof(DcmsDbContext))]
    [Migration("20261105120000_MaterialUsageUnits")]
    public partial class MaterialUsageUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "UsageUnit", table: "MATERIAL_CATALOG_ITEM", type: "character varying(40)", maxLength: 40, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "ConversionFactor", table: "MATERIAL_PURCHASE", type: "numeric(12,3)", precision: 12, scale: 3, nullable: false, defaultValue: 1m);
            migrationBuilder.AddColumn<string>(name: "UsageUnit", table: "MATERIAL_PURCHASE", type: "character varying(40)", maxLength: 40, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "UsageQuantity", table: "MATERIAL_PURCHASE", type: "numeric(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 0m);
            migrationBuilder.Sql("UPDATE \"MATERIAL_PURCHASE\" SET \"UsageQuantity\" = \"Quantity\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "UsageQuantity", table: "MATERIAL_PURCHASE");
            migrationBuilder.DropColumn(name: "UsageUnit", table: "MATERIAL_PURCHASE");
            migrationBuilder.DropColumn(name: "ConversionFactor", table: "MATERIAL_PURCHASE");
            migrationBuilder.DropColumn(name: "UsageUnit", table: "MATERIAL_CATALOG_ITEM");
        }
    }
}
