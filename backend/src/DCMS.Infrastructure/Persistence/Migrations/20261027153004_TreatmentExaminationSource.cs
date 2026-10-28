using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TreatmentExaminationSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceExaminationJson",
                table: "TREATMENT_DOSSIER",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql("ALTER TABLE \"TREATMENT_DOSSIER\" ADD COLUMN IF NOT EXISTS \"SourceExaminationVersion\" integer;");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceExaminationVisitId",
                table: "TREATMENT_DOSSIER",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TREATMENT_DOSSIER_SourceExaminationVisitId",
                table: "TREATMENT_DOSSIER",
                column: "SourceExaminationVisitId");

            migrationBuilder.AddForeignKey(
                name: "FK_TREATMENT_DOSSIER_VISIT_SourceExaminationVisitId",
                table: "TREATMENT_DOSSIER",
                column: "SourceExaminationVisitId",
                principalTable: "VISIT",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TREATMENT_DOSSIER_VISIT_SourceExaminationVisitId",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropIndex(
                name: "IX_TREATMENT_DOSSIER_SourceExaminationVisitId",
                table: "TREATMENT_DOSSIER");

            migrationBuilder.DropColumn(
                name: "SourceExaminationJson",
                table: "TREATMENT_DOSSIER");

            // Shared column is owned by TreatmentPaymentV25; retain it when removing this extension.

            migrationBuilder.DropColumn(
                name: "SourceExaminationVisitId",
                table: "TREATMENT_DOSSIER");
        }
    }
}
