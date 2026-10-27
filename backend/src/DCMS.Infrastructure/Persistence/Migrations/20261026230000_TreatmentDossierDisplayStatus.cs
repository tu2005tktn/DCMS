using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DCMS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DcmsDbContext))]
[Migration("20261026230000_TreatmentDossierDisplayStatus")]
public sealed class TreatmentDossierDisplayStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "DisplayCompleted", table: "TREATMENT_DOSSIER", type: "boolean",
            nullable: false, defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DisplayCompleted", table: "TREATMENT_DOSSIER");
    }
}
