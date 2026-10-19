using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialExaminationOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SCHEDULING_SERVICE_BranchId",
                table: "SCHEDULING_SERVICE");

            migrationBuilder.AddColumn<bool>(
                name: "IsInitialExamination",
                table: "SCHEDULING_SERVICE",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_SCHEDULING_SERVICE_BranchId",
                table: "SCHEDULING_SERVICE",
                column: "BranchId",
                unique: true,
                filter: "\"IsInitialExamination\" = TRUE");
            // Preserve old service references; create one explicit examination instead of guessing from names.
            migrationBuilder.Sql("""
                UPDATE "SCHEDULING_SERVICE" SET "PublicBookable" = FALSE;
                INSERT INTO "SCHEDULING_SERVICE" ("BranchId", "Name", "PublicDurationMinutes", "Active", "PublicBookable", "IsInitialExamination")
                SELECT "Id", 'Khám', 30, TRUE, TRUE, TRUE FROM "SCHEDULING_BRANCH";
                UPDATE "SCHEDULING_BRANCH" SET "BookingSettingsVersion" = "BookingSettingsVersion" + 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SCHEDULING_SERVICE_BranchId",
                table: "SCHEDULING_SERVICE");

            migrationBuilder.DropColumn(
                name: "IsInitialExamination",
                table: "SCHEDULING_SERVICE");

            migrationBuilder.CreateIndex(
                name: "IX_SCHEDULING_SERVICE_BranchId",
                table: "SCHEDULING_SERVICE",
                column: "BranchId");
        }
    }
}
