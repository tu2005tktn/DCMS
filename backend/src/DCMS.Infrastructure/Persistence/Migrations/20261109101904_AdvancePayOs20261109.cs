using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdvancePayOs20261109 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TREATMENT_PAYOS_ORDER_ActiveDossier",
                table: "TREATMENT_PAYOS_ORDER");

            migrationBuilder.AddColumn<Guid>(
                name: "AdvanceId",
                table: "TREATMENT_PAYOS_ORDER",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "TREATMENT_PAYOS_ORDER",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Treatment");

            migrationBuilder.CreateIndex(
                name: "IX_TREATMENT_PAYOS_ORDER_ActiveDossierPurpose",
                table: "TREATMENT_PAYOS_ORDER",
                columns: new[] { "DossierId", "Purpose" },
                unique: true,
                filter: "\"State\" IN ('Creating','Pending','Review')");

            migrationBuilder.CreateIndex(
                name: "IX_TREATMENT_PAYOS_ORDER_AdvanceId",
                table: "TREATMENT_PAYOS_ORDER",
                column: "AdvanceId");

            migrationBuilder.AddForeignKey(
                name: "FK_TREATMENT_PAYOS_ORDER_FINANCE_ADVANCE_AdvanceId",
                table: "TREATMENT_PAYOS_ORDER",
                column: "AdvanceId",
                principalTable: "FINANCE_ADVANCE",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TREATMENT_PAYOS_ORDER_FINANCE_ADVANCE_AdvanceId",
                table: "TREATMENT_PAYOS_ORDER");

            migrationBuilder.DropIndex(
                name: "IX_TREATMENT_PAYOS_ORDER_ActiveDossierPurpose",
                table: "TREATMENT_PAYOS_ORDER");

            migrationBuilder.DropIndex(
                name: "IX_TREATMENT_PAYOS_ORDER_AdvanceId",
                table: "TREATMENT_PAYOS_ORDER");

            migrationBuilder.DropColumn(
                name: "AdvanceId",
                table: "TREATMENT_PAYOS_ORDER");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "TREATMENT_PAYOS_ORDER");

            migrationBuilder.CreateIndex(
                name: "IX_TREATMENT_PAYOS_ORDER_ActiveDossier",
                table: "TREATMENT_PAYOS_ORDER",
                column: "DossierId",
                unique: true,
                filter: "\"State\" IN ('Creating','Pending','Review')");
        }
    }
}
