using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PatientPrescriptionExternalPurchase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsExternalPurchase",
                table: "PATIENT_PRESCRIPTION_ITEM",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresExternalPurchase",
                table: "PATIENT_PRESCRIPTION",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsExternalPurchase",
                table: "PATIENT_PRESCRIPTION_ITEM");

            migrationBuilder.DropColumn(
                name: "RequiresExternalPurchase",
                table: "PATIENT_PRESCRIPTION");
        }
    }
}
