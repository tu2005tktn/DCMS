using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TreatmentPlanBookingConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PatientConsentConfirmedAt",
                table: "APPOINTMENT",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PatientConsentConfirmedBy",
                table: "APPOINTMENT",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_PatientConsentConfirmedBy",
                table: "APPOINTMENT",
                column: "PatientConsentConfirmedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_APPOINTMENT_USER_ACCOUNT_PatientConsentConfirmedBy",
                table: "APPOINTMENT",
                column: "PatientConsentConfirmedBy",
                principalTable: "USER_ACCOUNT",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_APPOINTMENT_USER_ACCOUNT_PatientConsentConfirmedBy",
                table: "APPOINTMENT");

            migrationBuilder.DropIndex(
                name: "IX_APPOINTMENT_PatientConsentConfirmedBy",
                table: "APPOINTMENT");

            migrationBuilder.DropColumn(
                name: "PatientConsentConfirmedAt",
                table: "APPOINTMENT");

            migrationBuilder.DropColumn(
                name: "PatientConsentConfirmedBy",
                table: "APPOINTMENT");
        }
    }
}
