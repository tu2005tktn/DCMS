using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S58ReceiptAndExternalPrescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "BRANCH_RECEIPT_SEQUENCE",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "EXTERNAL_PRESCRIPTION_LINE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    MedicineId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicineCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MedicineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Strength = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DosageForm = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Unit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXTERNAL_PRESCRIPTION_LINE", x => x.Id);
                    table.CheckConstraint("CK_EXTERNAL_PRESCRIPTION_QUANTITY", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_EXTERNAL_PRESCRIPTION_LINE_CATALOG_MEDICINE_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "CATALOG_MEDICINE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EXTERNAL_PRESCRIPTION_LINE_USER_ACCOUNT_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EXTERNAL_PRESCRIPTION_LINE_USER_ACCOUNT_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EXTERNAL_PRESCRIPTION_LINE_VISIT_VisitId",
                        column: x => x.VisitId,
                        principalTable: "VISIT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PRESCRIPTION_COMMAND_RECEIPT",
                columns: table => new
                {
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRESCRIPTION_COMMAND_RECEIPT", x => new { x.ActorId, x.RequestKey });
                    table.ForeignKey(
                        name: "FK_PRESCRIPTION_COMMAND_RECEIPT_USER_ACCOUNT_ActorId",
                        column: x => x.ActorId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RECEIPT_PRINT_EVENT",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    SettingsVersion = table.Column<int>(type: "integer", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    PrintedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RECEIPT_PRINT_EVENT", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RECEIPT_PRINT_EVENT_PAYMENT_RECEIPT_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "PAYMENT_RECEIPT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RECEIPT_PRINT_EVENT_USER_ACCOUNT_ActorId",
                        column: x => x.ActorId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EXTERNAL_PRESCRIPTION_LINE_CreatedBy",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_EXTERNAL_PRESCRIPTION_LINE_MedicineId",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_EXTERNAL_PRESCRIPTION_LINE_UpdatedBy",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_EXTERNAL_PRESCRIPTION_LINE_VisitId",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_RECEIPT_PRINT_EVENT_ActorId",
                table: "RECEIPT_PRINT_EVENT",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_RECEIPT_PRINT_EVENT_ReceiptId_ActorId_RequestKey",
                table: "RECEIPT_PRINT_EVENT",
                columns: new[] { "ReceiptId", "ActorId", "RequestKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EXTERNAL_PRESCRIPTION_LINE");

            migrationBuilder.DropTable(
                name: "PRESCRIPTION_COMMAND_RECEIPT");

            migrationBuilder.DropTable(
                name: "RECEIPT_PRINT_EVENT");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "BRANCH_RECEIPT_SEQUENCE");
        }
    }
}
