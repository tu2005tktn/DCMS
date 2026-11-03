using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TreatmentLedgerIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "VisitId",
                table: "PAYMENT_RECEIPT",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "DossierId",
                table: "PAYMENT_RECEIPT",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "VisitId",
                table: "FINANCE_CHARGE",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "DossierId",
                table: "FINANCE_CHARGE",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_RECEIPT_DossierId",
                table: "PAYMENT_RECEIPT",
                column: "DossierId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PAYMENT_RECEIPT_SOURCE",
                table: "PAYMENT_RECEIPT",
                sql: "(\"VisitId\" IS NOT NULL) <> (\"DossierId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_CHARGE_DossierId",
                table: "FINANCE_CHARGE",
                column: "DossierId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FINANCE_CHARGE_SOURCE",
                table: "FINANCE_CHARGE",
                sql: "(\"VisitId\" IS NOT NULL) <> (\"DossierId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_FINANCE_CHARGE_TREATMENT_DOSSIER_DossierId",
                table: "FINANCE_CHARGE",
                column: "DossierId",
                principalTable: "TREATMENT_DOSSIER",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAYMENT_RECEIPT_TREATMENT_DOSSIER_DossierId",
                table: "PAYMENT_RECEIPT",
                column: "DossierId",
                principalTable: "TREATMENT_DOSSIER",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FINANCE_CHARGE_TREATMENT_DOSSIER_DossierId",
                table: "FINANCE_CHARGE");

            migrationBuilder.DropForeignKey(
                name: "FK_PAYMENT_RECEIPT_TREATMENT_DOSSIER_DossierId",
                table: "PAYMENT_RECEIPT");

            migrationBuilder.DropIndex(
                name: "IX_PAYMENT_RECEIPT_DossierId",
                table: "PAYMENT_RECEIPT");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PAYMENT_RECEIPT_SOURCE",
                table: "PAYMENT_RECEIPT");

            migrationBuilder.DropIndex(
                name: "IX_FINANCE_CHARGE_DossierId",
                table: "FINANCE_CHARGE");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FINANCE_CHARGE_SOURCE",
                table: "FINANCE_CHARGE");

            migrationBuilder.DropColumn(
                name: "DossierId",
                table: "PAYMENT_RECEIPT");

            migrationBuilder.DropColumn(
                name: "DossierId",
                table: "FINANCE_CHARGE");

            migrationBuilder.AlterColumn<Guid>(
                name: "VisitId",
                table: "PAYMENT_RECEIPT",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "VisitId",
                table: "FINANCE_CHARGE",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
