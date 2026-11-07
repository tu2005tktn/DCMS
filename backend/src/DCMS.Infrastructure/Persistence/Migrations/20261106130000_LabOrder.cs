using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LabOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LAB_ORDER",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    DossierId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    OrderedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    DeliveredOn = table.Column<DateOnly>(type: "date", nullable: true),
                    DentistId = table.Column<int>(type: "integer", nullable: false),
                    LabProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    LabWorkTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MarginOuter = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MarginInner = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MarginSide = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BridgeShapes = table.Column<string>(type: "jsonb", nullable: false),
                    IsChild = table.Column<bool>(type: "boolean", nullable: false),
                    Teeth = table.Column<string>(type: "jsonb", nullable: false),
                    Shade = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Material = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    TotalFeeVnd = table.Column<long>(type: "bigint", nullable: false),
                    PaidVnd = table.Column<long>(type: "bigint", nullable: false),
                    Warranty = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LAB_ORDER", x => x.Id);
                    table.CheckConstraint("CK_LAB_ORDER_FEES", "\"TotalFeeVnd\" >= 0 AND \"PaidVnd\" >= 0 AND \"PaidVnd\" <= \"TotalFeeVnd\"");
                    table.CheckConstraint("CK_LAB_ORDER_QUANTITY", "\"Quantity\" BETWEEN 1 AND 999");
                    table.ForeignKey(
                        name: "FK_LAB_ORDER_CLINICAL_ACTIVITY_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "CLINICAL_ACTIVITY",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LAB_ORDER_ActivityId",
                table: "LAB_ORDER",
                column: "ActivityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LAB_ORDER_BranchId_Code",
                table: "LAB_ORDER",
                columns: new[] { "BranchId", "Code" },
                unique: true,
                filter: "\"Code\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LAB_ORDER_DossierId",
                table: "LAB_ORDER",
                column: "DossierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LAB_ORDER");
        }
    }
}
