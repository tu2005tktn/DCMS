using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyClinicalActivityTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceKind",
                table: "CLINICAL_ACTIVITY");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceKind",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            // SourceKind is derived from VisitId after Up: restore the stored value for activities.
            migrationBuilder.Sql("UPDATE \"CLINICAL_ACTIVITY\" SET \"SourceKind\" = CASE WHEN \"VisitId\" IS NULL THEN 'Dossier' ELSE 'Visit' END WHERE \"ProcessKind\" = 'ClinicalActivity';");
        }
    }
}
