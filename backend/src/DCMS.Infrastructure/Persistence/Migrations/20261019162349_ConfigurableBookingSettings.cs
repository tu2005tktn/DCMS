using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurableBookingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SERVICE_DURATION",
                table: "SCHEDULING_SERVICE");

            migrationBuilder.AddColumn<int>(
                name: "BookingSettingsVersion",
                table: "SCHEDULING_BRANCH",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "SlotIntervalMinutes",
                table: "SCHEDULING_BRANCH",
                type: "integer",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SERVICE_DURATION",
                table: "SCHEDULING_SERVICE",
                sql: "\"PublicDurationMinutes\" BETWEEN 1 AND 480");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BRANCH_SLOT_INTERVAL",
                table: "SCHEDULING_BRANCH",
                sql: "\"SlotIntervalMinutes\" BETWEEN 1 AND 120 AND \"BookingSettingsVersion\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SERVICE_DURATION",
                table: "SCHEDULING_SERVICE");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BRANCH_SLOT_INTERVAL",
                table: "SCHEDULING_BRANCH");

            migrationBuilder.DropColumn(
                name: "BookingSettingsVersion",
                table: "SCHEDULING_BRANCH");

            migrationBuilder.DropColumn(
                name: "SlotIntervalMinutes",
                table: "SCHEDULING_BRANCH");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SERVICE_DURATION",
                table: "SCHEDULING_SERVICE",
                sql: "\"PublicDurationMinutes\" IN (30,60)");
        }
    }
}
