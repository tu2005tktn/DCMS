using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DossierPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TREATMENT_DOSSIER_PAYMENT",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DossierId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CollectorEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TotalVnd = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TREATMENT_DOSSIER_PAYMENT", x => x.Id);
                    table.CheckConstraint("CK_DOSSIER_PAYMENT_METHOD", "\"Method\" IN ('Cash','Transfer')");
                    table.CheckConstraint("CK_DOSSIER_PAYMENT_TOTAL", "\"TotalVnd\" > 0");
                    table.ForeignKey(
                        name: "FK_TREATMENT_DOSSIER_PAYMENT_TREATMENT_DOSSIER_DossierId",
                        column: x => x.DossierId,
                        principalTable: "TREATMENT_DOSSIER",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TREATMENT_DOSSIER_PAYMENT_LINE",
                columns: table => new
                {
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TREATMENT_DOSSIER_PAYMENT_LINE", x => new { x.PaymentId, x.ActivityId });
                    table.CheckConstraint("CK_DOSSIER_PAYMENT_LINE_AMOUNT", "\"AmountVnd\" > 0");
                    table.ForeignKey(
                        name: "FK_TREATMENT_DOSSIER_PAYMENT_LINE_CLINICAL_ACTIVITY_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "CLINICAL_ACTIVITY",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TREATMENT_DOSSIER_PAYMENT_LINE_TREATMENT_DOSSIER_PAYMENT_Pa~",
                        column: x => x.PaymentId,
                        principalTable: "TREATMENT_DOSSIER_PAYMENT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TREATMENT_DOSSIER_PAYMENT_DossierId_PaidOn",
                table: "TREATMENT_DOSSIER_PAYMENT",
                columns: new[] { "DossierId", "PaidOn" });

            migrationBuilder.CreateIndex(
                name: "IX_TREATMENT_DOSSIER_PAYMENT_LINE_ActivityId",
                table: "TREATMENT_DOSSIER_PAYMENT_LINE",
                column: "ActivityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TREATMENT_DOSSIER_PAYMENT_LINE");

            migrationBuilder.DropTable(
                name: "TREATMENT_DOSSIER_PAYMENT");
        }
    }
}
