using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TreatmentPayOsOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TREATMENT_PAYOS_ORDER",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DossierId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrderCode = table.Column<long>(type: "bigint", nullable: false),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CollectorEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentLinkId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    QrCode = table.Column<string>(type: "text", nullable: true),
                    AccountNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    AccountName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    BankBin = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    BankReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TREATMENT_PAYOS_ORDER", x => x.Id);
                    table.CheckConstraint("CK_TREATMENT_PAYOS_ORDER_AMOUNT", "\"AmountVnd\" > 0");
                    table.ForeignKey(
                        name: "FK_TREATMENT_PAYOS_ORDER_TREATMENT_DOSSIER_DossierId",
                        column: x => x.DossierId,
                        principalTable: "TREATMENT_DOSSIER",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TREATMENT_PAYOS_ORDER_TREATMENT_DOSSIER_PAYMENT_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "TREATMENT_DOSSIER_PAYMENT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TREATMENT_PAYOS_ORDER_LINE",
                columns: table => new
                {
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TREATMENT_PAYOS_ORDER_LINE", x => new { x.OrderId, x.ActivityId });
                    table.CheckConstraint("CK_TREATMENT_PAYOS_ORDER_LINE_AMOUNT", "\"AmountVnd\" > 0");
                    table.ForeignKey(
                        name: "FK_TREATMENT_PAYOS_ORDER_LINE_CLINICAL_ACTIVITY_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "CLINICAL_ACTIVITY",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TREATMENT_PAYOS_ORDER_LINE_TREATMENT_PAYOS_ORDER_OrderId",
                        column: x => x.OrderId,
                        principalTable: "TREATMENT_PAYOS_ORDER",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TREATMENT_PAYOS_ORDER_ActiveDossier",
                table: "TREATMENT_PAYOS_ORDER",
                column: "DossierId",
                unique: true,
                filter: "\"State\" IN ('Creating','Pending','Review')");

            migrationBuilder.CreateIndex(
                name: "IX_TREATMENT_PAYOS_ORDER_OrderCode",
                table: "TREATMENT_PAYOS_ORDER",
                column: "OrderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TREATMENT_PAYOS_ORDER_PaymentId",
                table: "TREATMENT_PAYOS_ORDER",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_TREATMENT_PAYOS_ORDER_PaymentLinkId",
                table: "TREATMENT_PAYOS_ORDER",
                column: "PaymentLinkId",
                unique: true,
                filter: "\"PaymentLinkId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TREATMENT_PAYOS_ORDER_RequestKey",
                table: "TREATMENT_PAYOS_ORDER",
                column: "RequestKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TREATMENT_PAYOS_ORDER_LINE_ActivityId",
                table: "TREATMENT_PAYOS_ORDER_LINE",
                column: "ActivityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TREATMENT_PAYOS_ORDER_LINE");

            migrationBuilder.DropTable(
                name: "TREATMENT_PAYOS_ORDER");
        }
    }
}
