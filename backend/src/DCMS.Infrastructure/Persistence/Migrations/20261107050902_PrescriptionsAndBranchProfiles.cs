using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    // docs/specs/PRESCRIPTION-MANAGEMENT.md §2.6 and docs/specs/S58-BRANCHES-AND-CHAIRS.md §1.5.
    /// <inheritdoc />
    public partial class PrescriptionsAndBranchProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActiveIngredient",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                type: "character varying(200)", maxLength: 200, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Route",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "Uống");

            migrationBuilder.AddColumn<string>(
                name: "MorningDose",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "0");

            migrationBuilder.AddColumn<string>(
                name: "NoonDose",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "0");

            migrationBuilder.AddColumn<string>(
                name: "AfternoonDose",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "0");

            migrationBuilder.AddColumn<string>(
                name: "EveningDose",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "0");

            migrationBuilder.AddColumn<string>(
                name: "MealTiming",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "AfterMeal");

            migrationBuilder.AddColumn<int>(
                name: "Days",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                type: "integer", nullable: false, defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FinalizedAt",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                type: "timestamp with time zone", nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrescribedByDoctorId",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                type: "integer", nullable: false, defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PrescriptionId",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                type: "uuid", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Route",
                table: "CATALOG_MEDICINE",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false, defaultValue: "Uống");
            migrationBuilder.AddColumn<string>(
                name: "Contraindications",
                table: "CATALOG_MEDICINE",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "AllergyWarnings",
                table: "CATALOG_MEDICINE",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BRANCH_PROFILE",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Website = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LogoAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "Asia/Ho_Chi_Minh"),
                    BankCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PayeeName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    SlotIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    BookingSettingsVersion = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BRANCH_PROFILE", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BRANCH_PROFILE_SCHEDULING_BRANCH_Id",
                        column: x => x.Id,
                        principalTable: "SCHEDULING_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BRANCH_CHAIR",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OpenMinute = table.Column<int>(type: "integer", nullable: false),
                    CloseMinute = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BRANCH_CHAIR", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BRANCH_CHAIR_SCHEDULING_BRANCH_BranchId",
                        column: x => x.BranchId,
                        principalTable: "SCHEDULING_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BRANCH_CHAIR_SCHEDULING_CHAIR_Id",
                        column: x => x.Id,
                        principalTable: "SCHEDULING_CHAIR",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BRANCH_ASSET",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedBy = table.Column<int>(type: "integer", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BRANCH_ASSET", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BRANCH_ASSET_SCHEDULING_BRANCH_BranchId",
                        column: x => x.BranchId,
                        principalTable: "SCHEDULING_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BRANCH_ASSET_USER_ACCOUNT_UploadedBy",
                        column: x => x.UploadedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PRESCRIPTION_TEMPLATE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DiagnosisKind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRESCRIPTION_TEMPLATE", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PRESCRIPTION_TEMPLATE_USER_ACCOUNT_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PRESCRIPTION_TEMPLATE_USER_ACCOUNT_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PRESCRIPTION_TEMPLATE_LINE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicineId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicineCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MedicineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ActiveIngredient = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Strength = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DosageForm = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Route = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "Uống"),
                    MorningDose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "0"),
                    NoonDose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "0"),
                    AfternoonDose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "0"),
                    EveningDose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "0"),
                    MealTiming = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "AfterMeal"),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRESCRIPTION_TEMPLATE_LINE", x => x.Id);
                    table.CheckConstraint("CK_PRESCRIPTION_TEMPLATE_QUANTITY", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_PRESCRIPTION_TEMPLATE_LINE_CATALOG_MEDICINE_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "CATALOG_MEDICINE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PRESCRIPTION_TEMPLATE_LINE_PRESCRIPTION_TEMPLATE_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "PRESCRIPTION_TEMPLATE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PRESCRIPTION_AUDIT",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BeforeJson = table.Column<string>(type: "jsonb", nullable: false),
                    AfterJson = table.Column<string>(type: "jsonb", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRESCRIPTION_AUDIT", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PRESCRIPTION_AUDIT_USER_ACCOUNT_ActorId",
                        column: x => x.ActorId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EXTERNAL_PRESCRIPTION",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    PrescribedByDoctorId = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GeneralAdvice = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Diagnosis = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CorrectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SupersedesId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinalizedBy = table.Column<int>(type: "integer", nullable: true),
                    VerificationCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXTERNAL_PRESCRIPTION", x => x.Id);
                    table.CheckConstraint("CK_EXTERNAL_PRESCRIPTION_REVISION", "\"Revision\" >= 1");
                    table.CheckConstraint("CK_EXTERNAL_PRESCRIPTION_STATUS", "\"Status\" IN ('Draft','Finalized','Superseded')");
                    table.ForeignKey(
                        name: "FK_EXTERNAL_PRESCRIPTION_EXTERNAL_PRESCRIPTION_SupersedesId",
                        column: x => x.SupersedesId,
                        principalTable: "EXTERNAL_PRESCRIPTION",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EXTERNAL_PRESCRIPTION_USER_ACCOUNT_FinalizedBy",
                        column: x => x.FinalizedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EXTERNAL_PRESCRIPTION_USER_ACCOUNT_PrescribedByDoctorId",
                        column: x => x.PrescribedByDoctorId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EXTERNAL_PRESCRIPTION_VISIT_VisitId",
                        column: x => x.VisitId,
                        principalTable: "VISIT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // One Draft revision per visit that already has S19 lines; no demo data is created.
            migrationBuilder.Sql("""
                UPDATE "EXTERNAL_PRESCRIPTION_LINE" SET "PrescribedByDoctorId" = "CreatedBy" WHERE "PrescribedByDoctorId" = 0;
                INSERT INTO "EXTERNAL_PRESCRIPTION" ("Id", "VisitId", "PatientId", "BranchId", "PrescribedByDoctorId", "Revision", "Status",
                    "GeneralAdvice", "Version", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt")
                SELECT gen_random_uuid(), l."VisitId", MIN(l."PatientId"), MIN(l."BranchId"),
                    (array_agg(l."CreatedBy" ORDER BY l."CreatedAt"))[1], 1, 'Draft', '', 1,
                    (array_agg(l."CreatedBy" ORDER BY l."CreatedAt"))[1], MIN(l."CreatedAt"),
                    (array_agg(l."UpdatedBy" ORDER BY l."UpdatedAt" DESC))[1], MAX(l."UpdatedAt")
                FROM "EXTERNAL_PRESCRIPTION_LINE" l GROUP BY l."VisitId";
                UPDATE "EXTERNAL_PRESCRIPTION_LINE" l SET "PrescriptionId" = p."Id"
                FROM "EXTERNAL_PRESCRIPTION" p WHERE p."VisitId" = l."VisitId" AND l."PrescriptionId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "PrescriptionId",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BRANCH_ASSET_BranchId",
                table: "BRANCH_ASSET",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BRANCH_ASSET_UploadedBy",
                table: "BRANCH_ASSET",
                column: "UploadedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BRANCH_CHAIR_BranchId_Code",
                table: "BRANCH_CHAIR",
                columns: new[] { "BranchId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BRANCH_PROFILE_Code",
                table: "BRANCH_PROFILE",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EXTERNAL_PRESCRIPTION_FinalizedBy",
                table: "EXTERNAL_PRESCRIPTION",
                column: "FinalizedBy");

            migrationBuilder.CreateIndex(
                name: "IX_EXTERNAL_PRESCRIPTION_PatientId_Status",
                table: "EXTERNAL_PRESCRIPTION",
                columns: new[] { "PatientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EXTERNAL_PRESCRIPTION_PrescribedByDoctorId",
                table: "EXTERNAL_PRESCRIPTION",
                column: "PrescribedByDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_EXTERNAL_PRESCRIPTION_SupersedesId",
                table: "EXTERNAL_PRESCRIPTION",
                column: "SupersedesId");

            migrationBuilder.CreateIndex(
                name: "IX_EXTERNAL_PRESCRIPTION_VisitId_Revision",
                table: "EXTERNAL_PRESCRIPTION",
                columns: new[] { "VisitId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EXTERNAL_PRESCRIPTION_LINE_PatientId",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_EXTERNAL_PRESCRIPTION_LINE_PrescriptionId",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                column: "PrescriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PRESCRIPTION_AUDIT_ActorId",
                table: "PRESCRIPTION_AUDIT",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_PRESCRIPTION_TEMPLATE_Code",
                table: "PRESCRIPTION_TEMPLATE",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRESCRIPTION_TEMPLATE_CreatedBy",
                table: "PRESCRIPTION_TEMPLATE",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PRESCRIPTION_TEMPLATE_UpdatedBy",
                table: "PRESCRIPTION_TEMPLATE",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PRESCRIPTION_TEMPLATE_LINE_MedicineId",
                table: "PRESCRIPTION_TEMPLATE_LINE",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_PRESCRIPTION_TEMPLATE_LINE_TemplateId",
                table: "PRESCRIPTION_TEMPLATE_LINE",
                column: "TemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_EXTERNAL_PRESCRIPTION_LINE_EXTERNAL_PRESCRIPTION_PrescriptionId",
                table: "EXTERNAL_PRESCRIPTION_LINE",
                column: "PrescriptionId",
                principalTable: "EXTERNAL_PRESCRIPTION",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_EXTERNAL_PRESCRIPTION_LINE_EXTERNAL_PRESCRIPTION_PrescriptionId", table: "EXTERNAL_PRESCRIPTION_LINE");
            migrationBuilder.DropIndex(name: "IX_EXTERNAL_PRESCRIPTION_LINE_PatientId", table: "EXTERNAL_PRESCRIPTION_LINE");
            migrationBuilder.DropIndex(name: "IX_EXTERNAL_PRESCRIPTION_LINE_PrescriptionId", table: "EXTERNAL_PRESCRIPTION_LINE");
            migrationBuilder.DropTable(name: "EXTERNAL_PRESCRIPTION");
            migrationBuilder.DropTable(name: "PRESCRIPTION_AUDIT");
            migrationBuilder.DropTable(name: "PRESCRIPTION_TEMPLATE_LINE");
            migrationBuilder.DropTable(name: "PRESCRIPTION_TEMPLATE");
            migrationBuilder.DropTable(name: "BRANCH_ASSET");
            migrationBuilder.DropTable(name: "BRANCH_CHAIR");
            migrationBuilder.DropTable(name: "BRANCH_PROFILE");
            migrationBuilder.DropColumn(name: "ActiveIngredient", table: "EXTERNAL_PRESCRIPTION_LINE");
            migrationBuilder.DropColumn(name: "Route", table: "EXTERNAL_PRESCRIPTION_LINE");
            migrationBuilder.DropColumn(name: "MorningDose", table: "EXTERNAL_PRESCRIPTION_LINE");
            migrationBuilder.DropColumn(name: "NoonDose", table: "EXTERNAL_PRESCRIPTION_LINE");
            migrationBuilder.DropColumn(name: "AfternoonDose", table: "EXTERNAL_PRESCRIPTION_LINE");
            migrationBuilder.DropColumn(name: "EveningDose", table: "EXTERNAL_PRESCRIPTION_LINE");
            migrationBuilder.DropColumn(name: "MealTiming", table: "EXTERNAL_PRESCRIPTION_LINE");
            migrationBuilder.DropColumn(name: "Days", table: "EXTERNAL_PRESCRIPTION_LINE");
            migrationBuilder.DropColumn(name: "Status", table: "EXTERNAL_PRESCRIPTION_LINE");
            migrationBuilder.DropColumn(name: "FinalizedAt", table: "EXTERNAL_PRESCRIPTION_LINE");
            migrationBuilder.DropColumn(name: "PrescribedByDoctorId", table: "EXTERNAL_PRESCRIPTION_LINE");
            migrationBuilder.DropColumn(name: "PrescriptionId", table: "EXTERNAL_PRESCRIPTION_LINE");
            migrationBuilder.DropColumn(name: "Route", table: "CATALOG_MEDICINE");
            migrationBuilder.DropColumn(name: "Contraindications", table: "CATALOG_MEDICINE");
            migrationBuilder.DropColumn(name: "AllergyWarnings", table: "CATALOG_MEDICINE");
        }
    }
}
