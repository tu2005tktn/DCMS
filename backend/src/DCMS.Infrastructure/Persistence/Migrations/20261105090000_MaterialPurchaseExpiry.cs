using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    // Optional expiry date per purchase row (docs/specs/MATERIAL-PURCHASES.md). Additive nullable column; existing rows stay empty.
    [DbContext(typeof(DcmsDbContext))]
    [Migration("20261105090000_MaterialPurchaseExpiry")]
    public partial class MaterialPurchaseExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpiresOn",
                table: "MATERIAL_PURCHASE",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ExpiresOn", table: "MATERIAL_PURCHASE");
        }
    }
}
