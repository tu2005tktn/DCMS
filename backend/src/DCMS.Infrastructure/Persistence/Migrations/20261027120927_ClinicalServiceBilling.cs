using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClinicalServiceBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingUnit",
                table: "SCHEDULING_SERVICE",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Lần");

            migrationBuilder.AddColumn<string>(
                name: "InvestigationKind",
                table: "SCHEDULING_SERVICE",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInvestigationService",
                table: "SCHEDULING_SERVICE",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BillingMode",
                table: "INVESTIGATION_ORDER",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PerformedAt",
                table: "INVESTIGATION_ORDER",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PerformedBy",
                table: "INVESTIGATION_ORDER",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriceVersionSnapshot",
                table: "INVESTIGATION_ORDER",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProviderId",
                table: "INVESTIGATION_ORDER",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderNameSnapshot",
                table: "INVESTIGATION_ORDER",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProviderUnitCostSnapshotVnd",
                table: "INVESTIGATION_ORDER",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "INVESTIGATION_ORDER",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "QuoteEvidence",
                table: "INVESTIGATION_ORDER",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QuotedAt",
                table: "INVESTIGATION_ORDER",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuotedBy",
                table: "INVESTIGATION_ORDER",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceId",
                table: "INVESTIGATION_ORDER",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceNameSnapshot",
                table: "INVESTIGATION_ORDER",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UnitPriceSnapshotVnd",
                table: "INVESTIGATION_ORDER",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitSnapshot",
                table: "INVESTIGATION_ORDER",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BRANCH_RECEIPT_SEQUENCE",
                columns: table => new
                {
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    NextNumber = table.Column<long>(type: "bigint", nullable: false)
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
                name: "FINANCE_AUDIT",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Target = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FINANCE_AUDIT", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FINANCE_AUDIT_USER_ACCOUNT_ActorId",
                        column: x => x.ActorId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FINANCE_CHARGE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestigationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPriceVnd = table.Column<long>(type: "bigint", nullable: false),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false),
                    PriceVersion = table.Column<int>(type: "integer", nullable: false),
                    PostedBy = table.Column<int>(type: "integer", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FINANCE_CHARGE", x => x.Id);
                    table.CheckConstraint("CK_FINANCE_CHARGE_AMOUNT", "\"AmountVnd\" >= 0 AND \"Quantity\" > 0 AND \"UnitPriceVnd\" >= 0");
                    table.ForeignKey(
                        name: "FK_FINANCE_CHARGE_CLINICAL_ACTIVITY_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "CLINICAL_ACTIVITY",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FINANCE_CHARGE_INVESTIGATION_ORDER_InvestigationId",
                        column: x => x.InvestigationId,
                        principalTable: "INVESTIGATION_ORDER",
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
                name: "FINANCE_COMMAND_RECEIPT",
                columns: table => new
                {
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FINANCE_COMMAND_RECEIPT", x => new { x.ActorId, x.RequestKey });
                    table.ForeignKey(
                        name: "FK_FINANCE_COMMAND_RECEIPT_USER_ACCOUNT_ActorId",
                        column: x => x.ActorId,
                        principalTable: "USER_ACCOUNT",
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
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    PatientName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    BranchName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false),
                    Payer = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DeclaredReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BankReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NormalizedBankReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConfirmationNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    ConfirmedBy = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAYMENT_RECEIPT", x => x.Id);
                    table.CheckConstraint("CK_PAYMENT_RECEIPT", "\"AmountVnd\" > 0 AND \"Method\" IN ('Cash','Transfer') AND \"State\" IN ('Pending','Posted','Cancelled')");
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
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PROVIDER", x => x.Id);
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
                name: "PROVIDER_PAYABLE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_INVESTIGATION_ORDER_PerformedBy",
                table: "INVESTIGATION_ORDER",
                column: "PerformedBy");

            migrationBuilder.CreateIndex(
                name: "IX_INVESTIGATION_ORDER_ProviderId",
                table: "INVESTIGATION_ORDER",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_INVESTIGATION_ORDER_QuotedBy",
                table: "INVESTIGATION_ORDER",
                column: "QuotedBy");

            migrationBuilder.CreateIndex(
                name: "IX_INVESTIGATION_ORDER_ServiceId",
                table: "INVESTIGATION_ORDER",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_AUDIT_ActorId",
                table: "FINANCE_AUDIT",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_AUDIT_BranchId_At",
                table: "FINANCE_AUDIT",
                columns: new[] { "BranchId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_CHARGE_ActivityId",
                table: "FINANCE_CHARGE",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_FINANCE_CHARGE_BranchId",
                table: "FINANCE_CHARGE",
                column: "BranchId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_INVESTIGATION_ORDER_PROVIDER_ProviderId",
                table: "INVESTIGATION_ORDER",
                column: "ProviderId",
                principalTable: "PROVIDER",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INVESTIGATION_ORDER_SCHEDULING_SERVICE_ServiceId",
                table: "INVESTIGATION_ORDER",
                column: "ServiceId",
                principalTable: "SCHEDULING_SERVICE",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INVESTIGATION_ORDER_USER_ACCOUNT_PerformedBy",
                table: "INVESTIGATION_ORDER",
                column: "PerformedBy",
                principalTable: "USER_ACCOUNT",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INVESTIGATION_ORDER_USER_ACCOUNT_QuotedBy",
                table: "INVESTIGATION_ORDER",
                column: "QuotedBy",
                principalTable: "USER_ACCOUNT",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_INVESTIGATION_ORDER_PROVIDER_ProviderId",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropForeignKey(
                name: "FK_INVESTIGATION_ORDER_SCHEDULING_SERVICE_ServiceId",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropForeignKey(
                name: "FK_INVESTIGATION_ORDER_USER_ACCOUNT_PerformedBy",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropForeignKey(
                name: "FK_INVESTIGATION_ORDER_USER_ACCOUNT_QuotedBy",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropTable(
                name: "BRANCH_RECEIPT_SEQUENCE");

            migrationBuilder.DropTable(
                name: "FINANCE_AUDIT");

            migrationBuilder.DropTable(
                name: "FINANCE_COMMAND_RECEIPT");

            migrationBuilder.DropTable(
                name: "FINANCIAL_ACCOUNT");

            migrationBuilder.DropTable(
                name: "PAYMENT_ALLOCATION");

            migrationBuilder.DropTable(
                name: "PROVIDER_PAYABLE");

            migrationBuilder.DropTable(
                name: "PAYMENT_RECEIPT");

            migrationBuilder.DropTable(
                name: "FINANCE_CHARGE");

            migrationBuilder.DropTable(
                name: "PROVIDER");

            migrationBuilder.DropIndex(
                name: "IX_INVESTIGATION_ORDER_PerformedBy",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropIndex(
                name: "IX_INVESTIGATION_ORDER_ProviderId",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropIndex(
                name: "IX_INVESTIGATION_ORDER_QuotedBy",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropIndex(
                name: "IX_INVESTIGATION_ORDER_ServiceId",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropColumn(
                name: "BillingUnit",
                table: "SCHEDULING_SERVICE");

            migrationBuilder.DropColumn(
                name: "InvestigationKind",
                table: "SCHEDULING_SERVICE");

            migrationBuilder.DropColumn(
                name: "IsInvestigationService",
                table: "SCHEDULING_SERVICE");

            migrationBuilder.DropColumn(
                name: "BillingMode",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropColumn(
                name: "PerformedAt",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropColumn(
                name: "PerformedBy",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropColumn(
                name: "PriceVersionSnapshot",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropColumn(
                name: "ProviderNameSnapshot",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropColumn(
                name: "ProviderUnitCostSnapshotVnd",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropColumn(
                name: "QuoteEvidence",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropColumn(
                name: "QuotedAt",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropColumn(
                name: "QuotedBy",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropColumn(
                name: "ServiceId",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropColumn(
                name: "ServiceNameSnapshot",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropColumn(
                name: "UnitPriceSnapshotVnd",
                table: "INVESTIGATION_ORDER");

            migrationBuilder.DropColumn(
                name: "UnitSnapshot",
                table: "INVESTIGATION_ORDER");
        }
    }
}
