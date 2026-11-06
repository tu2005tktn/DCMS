using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    // Stock deduction lines per treatment activity (docs/specs/MATERIAL-STOCK.md). New table only.
    [DbContext(typeof(DcmsDbContext))]
    [Migration("20261106090000_MaterialUsage")]
    public partial class MaterialUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MATERIAL_USAGE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    UsageUnit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    DossierId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedBy = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MATERIAL_USAGE", x => x.Id);
                    table.ForeignKey("FK_MATERIAL_USAGE_MATERIAL_CATALOG_ITEM_MaterialId", x => x.MaterialId,
                        "MATERIAL_CATALOG_ITEM", "Id", onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex(name: "IX_MATERIAL_USAGE_ActivityId", table: "MATERIAL_USAGE", column: "ActivityId");
            migrationBuilder.CreateIndex(name: "IX_MATERIAL_USAGE_MaterialId", table: "MATERIAL_USAGE", column: "MaterialId");
            migrationBuilder.CreateIndex(name: "IX_MATERIAL_USAGE_UsedAt", table: "MATERIAL_USAGE", column: "UsedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "MATERIAL_USAGE");
    }
}
