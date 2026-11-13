using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PatientNotificationRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_APPOINTMENT_REMINDER_AppointmentId_AppointmentVersion_LeadM~",
                table: "APPOINTMENT_REMINDER");

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_REMINDER_AppointmentId_AppointmentVersion_LeadM~",
                table: "APPOINTMENT_REMINDER",
                columns: new[] { "AppointmentId", "AppointmentVersion", "LeadMinutes", "AccountId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_APPOINTMENT_REMINDER_AppointmentId_AppointmentVersion_LeadM~",
                table: "APPOINTMENT_REMINDER");

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_REMINDER_AppointmentId_AppointmentVersion_LeadM~",
                table: "APPOINTMENT_REMINDER",
                columns: new[] { "AppointmentId", "AppointmentVersion", "LeadMinutes" },
                unique: true);
        }
    }
}
