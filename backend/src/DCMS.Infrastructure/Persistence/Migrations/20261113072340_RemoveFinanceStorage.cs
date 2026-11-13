using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFinanceStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Finance is removed until it is specified again (docs/specs/CARE-LAB-FINANCE-STORAGE.vi.md).
            // Its history in the shared stores goes with its tables; Down recreates empty tables only.
            migrationBuilder.Sql("""
                DELETE FROM "AUDIT_LOG" WHERE "AuditType" = 'FinanceAudit';
                DELETE FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'FinanceCommandReceipt';
                """);

            migrationBuilder.DropTable(
                name: "BRANCH_RECEIPT_SEQUENCE");

            migrationBuilder.DropTable(
                name: "EXAMINATION_FEE");

            migrationBuilder.DropTable(
                name: "FINANCE_ADVANCE_ALLOCATION");

            migrationBuilder.DropTable(
                name: "FINANCE_PAYMENT_METHOD");

            migrationBuilder.DropTable(
                name: "FINANCE_REFUND");

            migrationBuilder.DropTable(
                name: "FINANCIAL_ACCOUNT");

            migrationBuilder.DropTable(
                name: "PAYMENT_ALLOCATION");

            migrationBuilder.DropTable(
                name: "PROVIDER_PAYABLE");

            migrationBuilder.DropTable(
                name: "RECEIPT_PRINT_EVENT");

            migrationBuilder.DropTable(
                name: "TREATMENT_DOSSIER_PAYMENT_LINE");

            migrationBuilder.DropTable(
                name: "TREATMENT_PAYOS_ORDER");

            migrationBuilder.DropTable(
                name: "FINANCE_INVOICE_LINE");

            migrationBuilder.DropTable(
                name: "PROVIDER");

            migrationBuilder.DropTable(
                name: "FINANCE_ADVANCE");

            migrationBuilder.DropTable(
                name: "TREATMENT_DOSSIER_PAYMENT");

            migrationBuilder.DropTable(
                name: "FINANCE_CHARGE");

            migrationBuilder.DropTable(
                name: "FINANCE_INVOICE");

            migrationBuilder.DropTable(
                name: "PAYMENT_RECEIPT");

            migrationBuilder.DropIndex(
                name: "IX_AUDIT_LOG_BranchId_At",
                table: "AUDIT_LOG");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BRANCH_RECEIPT_SEQUENCE",
                columns: table => new
                {
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    NextNumber = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BRANCH_RECEIPT_SEQUENCE", x => x.BranchId);
                    table.ForeignKey(
                        name: "FK_BRANCH_RECEIPT_SEQUENCE_SCHEDULING_BRANCH_BranchId",
                        column: x => x.BranchId,
                        principalTable: "SCHEDULING_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FINANCE_CHARGE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    DossierId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvestigationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PostedBy = table.Column<int>(type: "integer", nullable: false),
                    PriceVersion = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UnitPriceVnd = table.Column<long>(type: "bigint", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FINANCE_CHARGE", x => x.Id);
                    table.CheckConstraint("CK_FINANCE_CHARGE_AMOUNT", "\"AmountVnd\" >= 0 AND \"Quantity\" > 0 AND \"UnitPriceVnd\" >= 0");
                    table.CheckConstraint("CK_FINANCE_CHARGE_SOURCE", "(\"VisitId\" IS NOT NULL) <> (\"DossierId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_FINANCE_CHARGE_CLINICAL_ACTIVITY_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "CLINICAL_ACTIVITY",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_CHARGE_PATIENT_PROFILE_PatientId",
                        column: x => x.PatientId,
                        principalTable: "PATIENT_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_CHARGE_SCHEDULING_BRANCH_BranchId",
                        column: x => x.BranchId,
                        principalTable: "SCHEDULING_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_CHARGE_SCHEDULING_SERVICE_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "SCHEDULING_SERVICE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_CHARGE_TREATMENT_DOSSIER_DossierId",
                        column: x => x.DossierId,
                        principalTable: "TREATMENT_DOSSIER",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_CHARGE_USER_ACCOUNT_PostedBy",
                        column: x => x.PostedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_CHARGE_VISIT_VisitId",
                        column: x => x.VisitId,
                        principalTable: "VISIT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FINANCE_INVOICE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    DossierId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FINANCE_INVOICE", x => x.Id);
                    table.CheckConstraint("CK_FINANCE_INVOICE_DATES", "\"DueDate\" >= \"InvoiceDate\"");
                    table.CheckConstraint("CK_FINANCE_INVOICE_SOURCE", "(\"VisitId\" IS NOT NULL) <> (\"DossierId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_FINANCE_INVOICE_PATIENT_PROFILE_PatientId",
                        column: x => x.PatientId,
                        principalTable: "PATIENT_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_INVOICE_SCHEDULING_BRANCH_BranchId",
                        column: x => x.BranchId,
                        principalTable: "SCHEDULING_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_INVOICE_TREATMENT_DOSSIER_DossierId",
                        column: x => x.DossierId,
                        principalTable: "TREATMENT_DOSSIER",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_INVOICE_USER_ACCOUNT_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_INVOICE_VISIT_VisitId",
                        column: x => x.VisitId,
                        principalTable: "VISIT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FINANCE_PAYMENT_METHOD",
                columns: table => new
                {
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FINANCE_PAYMENT_METHOD", x => new { x.BranchId, x.Code });
                    table.ForeignKey(
                        name: "FK_FINANCE_PAYMENT_METHOD_SCHEDULING_BRANCH_BranchId",
                        column: x => x.BranchId,
                        principalTable: "SCHEDULING_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FINANCIAL_ACCOUNT",
                columns: table => new
                {
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FINANCIAL_ACCOUNT", x => new { x.PatientId, x.BranchId });
                    table.ForeignKey(
                        name: "FK_FINANCIAL_ACCOUNT_PATIENT_PROFILE_PatientId",
                        column: x => x.PatientId,
                        principalTable: "PATIENT_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCIAL_ACCOUNT_SCHEDULING_BRANCH_BranchId",
                        column: x => x.BranchId,
                        principalTable: "SCHEDULING_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAYMENT_RECEIPT",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false),
                    BankReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    BranchName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ConfirmationNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConfirmedBy = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    DeclaredReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DossierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NormalizedBankReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    PatientName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Payer = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Charge"),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAYMENT_RECEIPT", x => x.Id);
                    table.CheckConstraint("CK_PAYMENT_RECEIPT", "\"AmountVnd\" > 0 AND length(btrim(\"Method\")) > 0 AND \"State\" IN ('Pending','Posted','Cancelled')");
                    table.CheckConstraint("CK_PAYMENT_RECEIPT_SOURCE", "(\"Purpose\" = 'Advance' AND \"VisitId\" IS NULL AND \"DossierId\" IS NULL) OR (\"Purpose\" = 'Charge' AND ((\"VisitId\" IS NOT NULL) <> (\"DossierId\" IS NOT NULL)))");
                    table.ForeignKey(
                        name: "FK_PAYMENT_RECEIPT_PATIENT_PROFILE_PatientId",
                        column: x => x.PatientId,
                        principalTable: "PATIENT_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAYMENT_RECEIPT_SCHEDULING_BRANCH_BranchId",
                        column: x => x.BranchId,
                        principalTable: "SCHEDULING_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAYMENT_RECEIPT_TREATMENT_DOSSIER_DossierId",
                        column: x => x.DossierId,
                        principalTable: "TREATMENT_DOSSIER",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAYMENT_RECEIPT_USER_ACCOUNT_ConfirmedBy",
                        column: x => x.ConfirmedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAYMENT_RECEIPT_USER_ACCOUNT_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAYMENT_RECEIPT_VISIT_VisitId",
                        column: x => x.VisitId,
                        principalTable: "VISIT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PROVIDER",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PROVIDER", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TREATMENT_DOSSIER_PAYMENT",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    CollectorEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    DossierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    TotalVnd = table.Column<long>(type: "bigint", nullable: false)
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
                name: "EXAMINATION_FEE",
                columns: table => new
                {
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: true),
                    ChargeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<int>(type: "integer", nullable: true),
                    PriceSource = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    PriceVersion = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ServiceId = table.Column<int>(type: "integer", nullable: true),
                    ServiceName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXAMINATION_FEE", x => x.VisitId);
                    table.ForeignKey(
                        name: "FK_EXAMINATION_FEE_FINANCE_CHARGE_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "FINANCE_CHARGE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EXAMINATION_FEE_SCHEDULING_SERVICE_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "SCHEDULING_SERVICE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EXAMINATION_FEE_USER_ACCOUNT_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EXAMINATION_FEE_USER_ACCOUNT_PostedBy",
                        column: x => x.PostedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EXAMINATION_FEE_VISIT_VisitId",
                        column: x => x.VisitId,
                        principalTable: "VISIT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FINANCE_INVOICE_LINE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscountVnd = table.Column<long>(type: "bigint", nullable: false),
                    GrossVnd = table.Column<long>(type: "bigint", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    NetVnd = table.Column<long>(type: "bigint", nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FINANCE_INVOICE_LINE", x => x.Id);
                    table.CheckConstraint("CK_FINANCE_INVOICE_LINE_AMOUNT", "\"GrossVnd\" >= 0 AND \"DiscountVnd\" >= 0 AND \"NetVnd\" = \"GrossVnd\" - \"DiscountVnd\"");
                    table.ForeignKey(
                        name: "FK_FINANCE_INVOICE_LINE_FINANCE_CHARGE_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "FINANCE_CHARGE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_INVOICE_LINE_FINANCE_INVOICE_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "FINANCE_INVOICE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FINANCE_ADVANCE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    DossierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FINANCE_ADVANCE", x => x.Id);
                    table.CheckConstraint("CK_FINANCE_ADVANCE_KIND", "\"Kind\" IN ('Deposit','ServiceAdvance')");
                    table.ForeignKey(
                        name: "FK_FINANCE_ADVANCE_PATIENT_PROFILE_PatientId",
                        column: x => x.PatientId,
                        principalTable: "PATIENT_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_ADVANCE_PAYMENT_RECEIPT_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "PAYMENT_RECEIPT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_ADVANCE_SCHEDULING_BRANCH_BranchId",
                        column: x => x.BranchId,
                        principalTable: "SCHEDULING_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_ADVANCE_TREATMENT_DOSSIER_DossierId",
                        column: x => x.DossierId,
                        principalTable: "TREATMENT_DOSSIER",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_ADVANCE_USER_ACCOUNT_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAYMENT_ALLOCATION",
                columns: table => new
                {
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAYMENT_ALLOCATION", x => new { x.PaymentId, x.ChargeId });
                    table.CheckConstraint("CK_PAYMENT_ALLOCATION_AMOUNT", "\"AmountVnd\" > 0");
                    table.ForeignKey(
                        name: "FK_PAYMENT_ALLOCATION_FINANCE_CHARGE_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "FINANCE_CHARGE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAYMENT_ALLOCATION_PAYMENT_RECEIPT_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "PAYMENT_RECEIPT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RECEIPT_PRINT_EVENT",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    PrintedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    SettingsVersion = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "PROVIDER_PAYABLE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    ChargeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PROVIDER_PAYABLE", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PROVIDER_PAYABLE_FINANCE_CHARGE_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "FINANCE_CHARGE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PROVIDER_PAYABLE_PROVIDER_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "PROVIDER",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PROVIDER_PAYABLE_SCHEDULING_BRANCH_BranchId",
                        column: x => x.BranchId,
                        principalTable: "SCHEDULING_BRANCH",
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

            migrationBuilder.CreateTable(
                name: "FINANCE_ADVANCE_ALLOCATION",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AllocatedBy = table.Column<int>(type: "integer", nullable: false),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false),
                    InvoiceLineId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FINANCE_ADVANCE_ALLOCATION", x => x.Id);
                    table.CheckConstraint("CK_FINANCE_ADVANCE_ALLOCATION_AMOUNT", "\"AmountVnd\" > 0");
                    table.ForeignKey(
                        name: "FK_FINANCE_ADVANCE_ALLOCATION_FINANCE_ADVANCE_AdvanceId",
                        column: x => x.AdvanceId,
                        principalTable: "FINANCE_ADVANCE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_ADVANCE_ALLOCATION_FINANCE_INVOICE_LINE_InvoiceLine~",
                        column: x => x.InvoiceLineId,
                        principalTable: "FINANCE_INVOICE_LINE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_ADVANCE_ALLOCATION_USER_ACCOUNT_AllocatedBy",
                        column: x => x.AllocatedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FINANCE_REFUND",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<int>(type: "integer", nullable: true),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    ChargeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<int>(type: "integer", nullable: true),
                    DossierPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestedBy = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FINANCE_REFUND", x => x.Id);
                    table.CheckConstraint("CK_FINANCE_REFUND_AMOUNT", "\"AmountVnd\" > 0");
                    table.CheckConstraint("CK_FINANCE_REFUND_SOURCE", "(\"PaymentId\" IS NOT NULL) <> (\"DossierPaymentId\" IS NOT NULL)");
                    table.CheckConstraint("CK_FINANCE_REFUND_STATE", "\"State\" IN ('Requested','Approved','Completed','Rejected')");
                    table.CheckConstraint("CK_FINANCE_REFUND_TARGET", "(\"AdvanceId\" IS NOT NULL AND \"InvoiceId\" IS NULL AND \"ChargeId\" IS NULL) OR (\"InvoiceId\" IS NOT NULL AND \"ChargeId\" IS NOT NULL AND \"AdvanceId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_FINANCE_REFUND_FINANCE_ADVANCE_AdvanceId",
                        column: x => x.AdvanceId,
                        principalTable: "FINANCE_ADVANCE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_REFUND_FINANCE_CHARGE_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "FINANCE_CHARGE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_REFUND_FINANCE_INVOICE_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "FINANCE_INVOICE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_REFUND_PATIENT_PROFILE_PatientId",
                        column: x => x.PatientId,
                        principalTable: "PATIENT_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_REFUND_PAYMENT_RECEIPT_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "PAYMENT_RECEIPT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_REFUND_SCHEDULING_BRANCH_BranchId",
                        column: x => x.BranchId,
                        principalTable: "SCHEDULING_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_REFUND_TREATMENT_DOSSIER_PAYMENT_DossierPaymentId",
                        column: x => x.DossierPaymentId,
                        principalTable: "TREATMENT_DOSSIER_PAYMENT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_REFUND_USER_ACCOUNT_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_REFUND_USER_ACCOUNT_CompletedBy",
                        column: x => x.CompletedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_REFUND_USER_ACCOUNT_RequestedBy",
                        column: x => x.RequestedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TREATMENT_PAYOS_ORDER",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    AccountNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    AdvanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false),
                    BankBin = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    BankReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    CollectorEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    DossierId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OrderCode = table.Column<long>(type: "bigint", nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentLinkId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Purpose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Treatment"),
                    QrCode = table.Column<string>(type: "text", nullable: true),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Lines = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TREATMENT_PAYOS_ORDER", x => x.Id);
                    table.CheckConstraint("CK_TREATMENT_PAYOS_ORDER_AMOUNT", "\"AmountVnd\" > 0");
                    table.ForeignKey(
                        name: "FK_TREATMENT_PAYOS_ORDER_FINANCE_ADVANCE_AdvanceId",
                        column: x => x.AdvanceId,
                        principalTable: "FINANCE_ADVANCE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_LOG_BranchId_At",
                table: "AUDIT_LOG",
                columns: new[] { "BranchId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_EXAMINATION_FEE_ChargeId",
                table: "EXAMINATION_FEE",
                column: "ChargeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EXAMINATION_FEE_CreatedBy",
                table: "EXAMINATION_FEE",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_EXAMINATION_FEE_PostedBy",
                table: "EXAMINATION_FEE",
                column: "PostedBy");

            migrationBuilder.CreateIndex(
                name: "IX_EXAMINATION_FEE_ServiceId",
                table: "EXAMINATION_FEE",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_EXAMINATION_FEE_State",
                table: "EXAMINATION_FEE",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_ADVANCE_BranchId",
                table: "FINANCE_ADVANCE",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_ADVANCE_CreatedBy",
                table: "FINANCE_ADVANCE",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_ADVANCE_DossierId",
                table: "FINANCE_ADVANCE",
                column: "DossierId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_ADVANCE_PatientId_BranchId",
                table: "FINANCE_ADVANCE",
                columns: new[] { "PatientId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_ADVANCE_PaymentId",
                table: "FINANCE_ADVANCE",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_ADVANCE_ALLOCATION_AdvanceId_InvoiceLineId",
                table: "FINANCE_ADVANCE_ALLOCATION",
                columns: new[] { "AdvanceId", "InvoiceLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_ADVANCE_ALLOCATION_AllocatedBy",
                table: "FINANCE_ADVANCE_ALLOCATION",
                column: "AllocatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_ADVANCE_ALLOCATION_InvoiceLineId",
                table: "FINANCE_ADVANCE_ALLOCATION",
                column: "InvoiceLineId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_CHARGE_ActivityId",
                table: "FINANCE_CHARGE",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_CHARGE_BranchId",
                table: "FINANCE_CHARGE",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_CHARGE_DossierId",
                table: "FINANCE_CHARGE",
                column: "DossierId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_CHARGE_InvestigationId",
                table: "FINANCE_CHARGE",
                column: "InvestigationId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_CHARGE_PatientId_BranchId",
                table: "FINANCE_CHARGE",
                columns: new[] { "PatientId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_CHARGE_PostedBy",
                table: "FINANCE_CHARGE",
                column: "PostedBy");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_CHARGE_ServiceId",
                table: "FINANCE_CHARGE",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_CHARGE_SourceKind_SourceId",
                table: "FINANCE_CHARGE",
                columns: new[] { "SourceKind", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_CHARGE_VisitId",
                table: "FINANCE_CHARGE",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_INVOICE_BranchId_Number",
                table: "FINANCE_INVOICE",
                columns: new[] { "BranchId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_INVOICE_CreatedBy",
                table: "FINANCE_INVOICE",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_INVOICE_DossierId",
                table: "FINANCE_INVOICE",
                column: "DossierId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_INVOICE_PatientId_BranchId_DueDate",
                table: "FINANCE_INVOICE",
                columns: new[] { "PatientId", "BranchId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_INVOICE_VisitId",
                table: "FINANCE_INVOICE",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_INVOICE_LINE_ChargeId",
                table: "FINANCE_INVOICE_LINE",
                column: "ChargeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_INVOICE_LINE_InvoiceId",
                table: "FINANCE_INVOICE_LINE",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_REFUND_AdvanceId",
                table: "FINANCE_REFUND",
                column: "AdvanceId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_REFUND_ApprovedBy",
                table: "FINANCE_REFUND",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_REFUND_BranchId_Number",
                table: "FINANCE_REFUND",
                columns: new[] { "BranchId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_REFUND_ChargeId",
                table: "FINANCE_REFUND",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_REFUND_CompletedBy",
                table: "FINANCE_REFUND",
                column: "CompletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_REFUND_DossierPaymentId",
                table: "FINANCE_REFUND",
                column: "DossierPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_REFUND_InvoiceId",
                table: "FINANCE_REFUND",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_REFUND_PatientId_BranchId_RequestedAt",
                table: "FINANCE_REFUND",
                columns: new[] { "PatientId", "BranchId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_REFUND_PaymentId",
                table: "FINANCE_REFUND",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_REFUND_RequestedBy",
                table: "FINANCE_REFUND",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCIAL_ACCOUNT_BranchId",
                table: "FINANCIAL_ACCOUNT",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_ALLOCATION_ChargeId",
                table: "PAYMENT_ALLOCATION",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_RECEIPT_BranchId_NormalizedBankReference",
                table: "PAYMENT_RECEIPT",
                columns: new[] { "BranchId", "NormalizedBankReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_RECEIPT_BranchId_Number",
                table: "PAYMENT_RECEIPT",
                columns: new[] { "BranchId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_RECEIPT_ConfirmedBy",
                table: "PAYMENT_RECEIPT",
                column: "ConfirmedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_RECEIPT_CreatedBy",
                table: "PAYMENT_RECEIPT",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_RECEIPT_DossierId",
                table: "PAYMENT_RECEIPT",
                column: "DossierId");

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_RECEIPT_PatientId_BranchId",
                table: "PAYMENT_RECEIPT",
                columns: new[] { "PatientId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_RECEIPT_VisitId",
                table: "PAYMENT_RECEIPT",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_PROVIDER_NormalizedCode",
                table: "PROVIDER",
                column: "NormalizedCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PROVIDER_NormalizedName",
                table: "PROVIDER",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PROVIDER_PAYABLE_BranchId",
                table: "PROVIDER_PAYABLE",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PROVIDER_PAYABLE_ChargeId",
                table: "PROVIDER_PAYABLE",
                column: "ChargeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PROVIDER_PAYABLE_ProviderId",
                table: "PROVIDER_PAYABLE",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_RECEIPT_PRINT_EVENT_ActorId",
                table: "RECEIPT_PRINT_EVENT",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_RECEIPT_PRINT_EVENT_ReceiptId_ActorId_RequestKey",
                table: "RECEIPT_PRINT_EVENT",
                columns: new[] { "ReceiptId", "ActorId", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TREATMENT_DOSSIER_PAYMENT_DossierId_PaidOn",
                table: "TREATMENT_DOSSIER_PAYMENT",
                columns: new[] { "DossierId", "PaidOn" });

            migrationBuilder.CreateIndex(
                name: "IX_TREATMENT_DOSSIER_PAYMENT_LINE_ActivityId",
                table: "TREATMENT_DOSSIER_PAYMENT_LINE",
                column: "ActivityId");

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
        }
    }
}
