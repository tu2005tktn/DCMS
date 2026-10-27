using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DCMS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DcmsDbContext))]
[Migration("20261026233000_TreatmentSheetDrafts")]
public sealed class TreatmentSheetDrafts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TREATMENT_SHEET_DRAFT",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DossierId = table.Column<Guid>(type: "uuid", nullable: false),
                VisitId = table.Column<Guid>(type: "uuid", nullable: true),
                ActorId = table.Column<int>(type: "integer", nullable: false),
                Kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                Version = table.Column<int>(type: "integer", nullable: false),
                PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                LastRequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                LastFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TREATMENT_SHEET_DRAFT", x => x.Id);
                table.ForeignKey("FK_TREATMENT_SHEET_DRAFT_TREATMENT_DOSSIER_DossierId", x => x.DossierId,
                    "TREATMENT_DOSSIER", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_TREATMENT_SHEET_DRAFT_DossierId_Kind_UpdatedAt",
            table: "TREATMENT_SHEET_DRAFT", columns: ["DossierId", "Kind", "UpdatedAt"]);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "TREATMENT_SHEET_DRAFT");
}
