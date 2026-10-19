using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentRescheduleDentist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NewDentistId",
                table: "APPOINTMENT_CHANGE",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OldDentistId",
                table: "APPOINTMENT_CHANGE",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NewDentistId",
                table: "APPOINTMENT_CHANGE");

            migrationBuilder.DropColumn(
                name: "OldDentistId",
                table: "APPOINTMENT_CHANGE");
        }
    }
}
