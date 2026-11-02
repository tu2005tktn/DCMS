using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DossierImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "VisitId",
                table: "CLINICAL_DOCUMENT",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CLINICAL_DOCUMENT_Context",
                table: "CLINICAL_DOCUMENT",
                sql: "\"VisitId\" IS NOT NULL OR \"DossierId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Existing dossier-only files must be retained; never invent a Visit on rollback.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "CLINICAL_DOCUMENT" WHERE "VisitId" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot revert DossierImages while dossier-only documents exist';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_CLINICAL_DOCUMENT_Context",
                table: "CLINICAL_DOCUMENT");

            migrationBuilder.AlterColumn<Guid>(
                name: "VisitId",
                table: "CLINICAL_DOCUMENT",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
