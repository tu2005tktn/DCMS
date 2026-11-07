using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations;

public partial class AllowCustomPrescriptionMedicines : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AlterColumn<Guid>(
        name: "MedicineId",
        table: "EXTERNAL_PRESCRIPTION_LINE",
        type: "uuid",
        nullable: true,
        oldClrType: typeof(Guid),
        oldType: "uuid");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.AlterColumn<Guid>(
        name: "MedicineId",
        table: "EXTERNAL_PRESCRIPTION_LINE",
        type: "uuid",
        nullable: false,
        oldClrType: typeof(Guid),
        oldType: "uuid",
        oldNullable: true);
}
