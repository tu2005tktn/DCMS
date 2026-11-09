using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinanceSettlements20261109 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PAYMENT_RECEIPT",
                table: "PAYMENT_RECEIPT");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PAYMENT_RECEIPT_SOURCE",
                table: "PAYMENT_RECEIPT");

            migrationBuilder.AlterColumn<string>(
                name: "Method",
                table: "PAYMENT_RECEIPT",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "PAYMENT_RECEIPT",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Charge");

            migrationBuilder.CreateTable(
                name: "FINANCE_ADVANCE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    DossierId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
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
                name: "FINANCE_INVOICE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: true),
                    DossierId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
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
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
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
                name: "FINANCE_INVOICE_LINE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GrossVnd = table.Column<long>(type: "bigint", nullable: false),
                    DiscountVnd = table.Column<long>(type: "bigint", nullable: false),
                    NetVnd = table.Column<long>(type: "bigint", nullable: false)
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
                name: "FINANCE_REFUND",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChargeId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdvanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DossierPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false),
                    Method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    State = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RequestedBy = table.Column<int>(type: "integer", nullable: false),
                    ApprovedBy = table.Column<int>(type: "integer", nullable: true),
                    CompletedBy = table.Column<int>(type: "integer", nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                name: "FINANCE_ADVANCE_ALLOCATION",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false),
                    AllocatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AllocatedBy = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.AddCheckConstraint(
                name: "CK_PAYMENT_RECEIPT",
                table: "PAYMENT_RECEIPT",
                sql: "\"AmountVnd\" > 0 AND length(btrim(\"Method\")) > 0 AND \"State\" IN ('Pending','Posted','Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PAYMENT_RECEIPT_SOURCE",
                table: "PAYMENT_RECEIPT",
                sql: "(\"Purpose\" = 'Advance' AND \"VisitId\" IS NULL AND \"DossierId\" IS NULL) OR (\"Purpose\" = 'Charge' AND ((\"VisitId\" IS NOT NULL) <> (\"DossierId\" IS NOT NULL)))");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FINANCE_ADVANCE_ALLOCATION");

            migrationBuilder.DropTable(
                name: "FINANCE_PAYMENT_METHOD");

            migrationBuilder.DropTable(
                name: "FINANCE_REFUND");

            migrationBuilder.DropTable(
                name: "FINANCE_INVOICE_LINE");

            migrationBuilder.DropTable(
                name: "FINANCE_ADVANCE");

            migrationBuilder.DropTable(
                name: "FINANCE_INVOICE");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PAYMENT_RECEIPT",
                table: "PAYMENT_RECEIPT");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PAYMENT_RECEIPT_SOURCE",
                table: "PAYMENT_RECEIPT");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "PAYMENT_RECEIPT");

            migrationBuilder.AlterColumn<string>(
                name: "Method",
                table: "PAYMENT_RECEIPT",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PAYMENT_RECEIPT",
                table: "PAYMENT_RECEIPT",
                sql: "\"AmountVnd\" > 0 AND \"Method\" IN ('Cash','Transfer') AND \"State\" IN ('Pending','Posted','Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PAYMENT_RECEIPT_SOURCE",
                table: "PAYMENT_RECEIPT",
                sql: "(\"VisitId\" IS NOT NULL) <> (\"DossierId\" IS NOT NULL)");
        }
    }
}
