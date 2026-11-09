using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLabOrderStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "Status",
                table: "LAB_ORDER",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotSent");

            migrationBuilder.Sql("UPDATE \"LAB_ORDER\" SET \"Status\" = 'Delivered' WHERE \"DeliveredOn\" IS NOT NULL;");

            migrationBuilder.CreateTable(
                name: "LAB_ORDER_STATUS_LOG",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LabOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangedBy = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LAB_ORDER_STATUS_LOG", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LAB_ORDER_STATUS_LOG_LAB_ORDER_LabOrderId",
                        column: x => x.LabOrderId,
                        principalTable: "LAB_ORDER",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LAB_ORDER_BranchId_Status_DueOn",
                table: "LAB_ORDER",
                columns: new[] { "BranchId", "Status", "DueOn" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LAB_ORDER_STATUS",
                table: "LAB_ORDER",
                sql: "\"Status\" IN ('NotSent','Sent','Received','Delivered','Rework')");

            migrationBuilder.CreateIndex(
                name: "IX_LAB_ORDER_STATUS_LOG_LabOrderId_ChangedAt",
                table: "LAB_ORDER_STATUS_LOG",
                columns: new[] { "LabOrderId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LAB_ORDER_STATUS_LOG");

            migrationBuilder.DropIndex(
                name: "IX_LAB_ORDER_BranchId_Status_DueOn",
                table: "LAB_ORDER");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LAB_ORDER_STATUS",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "ReceivedOn",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "SentOn",
                table: "LAB_ORDER");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "LAB_ORDER");
        }
    }
}
