using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecurringWorkingSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WORKING_SCHEDULE_REVISION",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PublishedBy = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WORKING_SCHEDULE_REVISION", x => x.Id);
                    table.CheckConstraint("CK_REVISION_KIND", "\"Kind\" IN ('Template','Exception') AND \"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_WORKING_SCHEDULE_REVISION_EMPLOYEE_PROFILE_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "EMPLOYEE_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WORKING_SCHEDULE_REVISION_USER_ACCOUNT_PublishedBy",
                        column: x => x.PublishedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WORKING_REVISION_PERIOD",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    StartMinute = table.Column<int>(type: "integer", nullable: false),
                    EndMinute = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WORKING_REVISION_PERIOD", x => x.Id);
                    table.CheckConstraint("CK_REVISION_PERIOD_RANGE", "\"Day\" BETWEEN 0 AND 6 AND \"StartMinute\" >= 0 AND \"EndMinute\" <= 1440 AND \"StartMinute\" < \"EndMinute\" AND \"StartMinute\" % 30 = 0 AND \"EndMinute\" % 30 = 0 AND \"Kind\" IN ('Work','Break','Block')");
                    table.ForeignKey(
                        name: "FK_WORKING_REVISION_PERIOD_SCHEDULING_BRANCH_BranchId",
                        column: x => x.BranchId,
                        principalTable: "SCHEDULING_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WORKING_REVISION_PERIOD_WORKING_SCHEDULE_REVISION_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "WORKING_SCHEDULE_REVISION",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WORKING_REVISION_PERIOD_BranchId",
                table: "WORKING_REVISION_PERIOD",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_WORKING_REVISION_PERIOD_RevisionId",
                table: "WORKING_REVISION_PERIOD",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_WORKING_SCHEDULE_REVISION_EmployeeId_Date",
                table: "WORKING_SCHEDULE_REVISION",
                columns: new[] { "EmployeeId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_WORKING_SCHEDULE_REVISION_EmployeeId_Version",
                table: "WORKING_SCHEDULE_REVISION",
                columns: new[] { "EmployeeId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WORKING_SCHEDULE_REVISION_PublishedBy",
                table: "WORKING_SCHEDULE_REVISION",
                column: "PublishedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WORKING_REVISION_PERIOD");

            migrationBuilder.DropTable(
                name: "WORKING_SCHEDULE_REVISION");
        }
    }
}
