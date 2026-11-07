using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PatientPrescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PATIENT_PRESCRIPTION",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    PrescribedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Diagnosis = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OtherDiagnosis = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Advice = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PrescriberEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PATIENT_PRESCRIPTION", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PATIENT_PRESCRIPTION_EMPLOYEE_PROFILE_PrescriberEmployeeId",
                        column: x => x.PrescriberEmployeeId,
                        principalTable: "EMPLOYEE_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PATIENT_PRESCRIPTION_PATIENT_PROFILE_PatientId",
                        column: x => x.PatientId,
                        principalTable: "PATIENT_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PATIENT_PRESCRIPTION_SCHEDULING_BRANCH_BranchId",
                        column: x => x.BranchId,
                        principalTable: "SCHEDULING_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PATIENT_PRESCRIPTION_ITEM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrescriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicineId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicineCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MedicineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Strength = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DosageForm = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Unit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PATIENT_PRESCRIPTION_ITEM", x => x.Id);
                    table.CheckConstraint("CK_PATIENT_PRESCRIPTION_ITEM_QUANTITY", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_PATIENT_PRESCRIPTION_ITEM_CATALOG_MEDICINE_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "CATALOG_MEDICINE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PATIENT_PRESCRIPTION_ITEM_PATIENT_PRESCRIPTION_Prescription~",
                        column: x => x.PrescriptionId,
                        principalTable: "PATIENT_PRESCRIPTION",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PATIENT_PRESCRIPTION_RECEIPT",
                columns: table => new
                {
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PrescriptionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PATIENT_PRESCRIPTION_RECEIPT", x => new { x.ActorId, x.RequestKey });
                    table.ForeignKey(
                        name: "FK_PATIENT_PRESCRIPTION_RECEIPT_PATIENT_PRESCRIPTION_Prescript~",
                        column: x => x.PrescriptionId,
                        principalTable: "PATIENT_PRESCRIPTION",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PATIENT_PRESCRIPTION_RECEIPT_USER_ACCOUNT_ActorId",
                        column: x => x.ActorId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PATIENT_PRESCRIPTION_BranchId",
                table: "PATIENT_PRESCRIPTION",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PATIENT_PRESCRIPTION_PatientId_PrescribedOn",
                table: "PATIENT_PRESCRIPTION",
                columns: new[] { "PatientId", "PrescribedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_PATIENT_PRESCRIPTION_PrescriberEmployeeId",
                table: "PATIENT_PRESCRIPTION",
                column: "PrescriberEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PATIENT_PRESCRIPTION_ITEM_MedicineId",
                table: "PATIENT_PRESCRIPTION_ITEM",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_PATIENT_PRESCRIPTION_ITEM_PrescriptionId",
                table: "PATIENT_PRESCRIPTION_ITEM",
                column: "PrescriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PATIENT_PRESCRIPTION_RECEIPT_PrescriptionId",
                table: "PATIENT_PRESCRIPTION_RECEIPT",
                column: "PrescriptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PATIENT_PRESCRIPTION_ITEM");

            migrationBuilder.DropTable(
                name: "PATIENT_PRESCRIPTION_RECEIPT");

            migrationBuilder.DropTable(
                name: "PATIENT_PRESCRIPTION");
        }
    }
}
