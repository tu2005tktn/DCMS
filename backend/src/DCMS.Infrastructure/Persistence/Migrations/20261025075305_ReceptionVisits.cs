using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReceptionVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CLINICAL_SOURCE_REFERENCE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Blocker = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CLINICAL_SOURCE_REFERENCE", x => x.Id);
                    table.UniqueConstraint("AK_CLINICAL_SOURCE_REFERENCE_Id_BranchId_PatientId", x => new { x.Id, x.BranchId, x.PatientId });
                    table.CheckConstraint("CK_CLINICAL_SOURCE_KIND", "\"Kind\" IN ('Dossier','Item','Step','Activity')");
                    table.ForeignKey(
                        name: "FK_CLINICAL_SOURCE_REFERENCE_CLINICAL_SOURCE_REFERENCE_ParentI~",
                        columns: x => new { x.ParentId, x.BranchId, x.PatientId },
                        principalTable: "CLINICAL_SOURCE_REFERENCE",
                        principalColumns: new[] { "Id", "BranchId", "PatientId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CLINICAL_SOURCE_REFERENCE_PATIENT_PROFILE_PatientId",
                        column: x => x.PatientId,
                        principalTable: "PATIENT_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CLINICAL_SOURCE_REFERENCE_SCHEDULING_BRANCH_BranchId",
                        column: x => x.BranchId,
                        principalTable: "SCHEDULING_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VISIT",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    DentistId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DossierId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    StepId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewReason = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    AdditionalConcern = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IdentityNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActualArrivalAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EnteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArrivalReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EnteredBy = table.Column<int>(type: "integer", nullable: false),
                    QueueOrder = table.Column<long>(type: "bigint", nullable: false),
                    ClinicalPriority = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    PriorityDentistId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VISIT", x => x.Id);
                    table.CheckConstraint("CK_VISIT_ARRIVAL", "\"ActualArrivalAt\" <= \"EnteredAt\"");
                    table.CheckConstraint("CK_VISIT_PURPOSE", "\"Purpose\" IN ('InitialExamination','TreatmentContinuation','PostoperativeOrWarrantyReview','PeriodicOrNewProblem')");
                    table.CheckConstraint("CK_VISIT_STATUS", "\"Status\" IN ('Queued','InProgress','Completed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_VISIT_APPOINTMENT_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "APPOINTMENT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VISIT_EMPLOYEE_PROFILE_DentistId",
                        column: x => x.DentistId,
                        principalTable: "EMPLOYEE_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VISIT_EMPLOYEE_PROFILE_PriorityDentistId",
                        column: x => x.PriorityDentistId,
                        principalTable: "EMPLOYEE_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VISIT_PATIENT_PROFILE_PatientId",
                        column: x => x.PatientId,
                        principalTable: "PATIENT_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VISIT_SCHEDULING_BRANCH_BranchId",
                        column: x => x.BranchId,
                        principalTable: "SCHEDULING_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VISIT_USER_ACCOUNT_EnteredBy",
                        column: x => x.EnteredBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RECEPTION_COMMAND_RECEIPT",
                columns: table => new
                {
                    ActorAccountId = table.Column<int>(type: "integer", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RECEPTION_COMMAND_RECEIPT", x => new { x.ActorAccountId, x.RequestKey });
                    table.ForeignKey(
                        name: "FK_RECEPTION_COMMAND_RECEIPT_USER_ACCOUNT_ActorAccountId",
                        column: x => x.ActorAccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RECEPTION_COMMAND_RECEIPT_VISIT_VisitId",
                        column: x => x.VisitId,
                        principalTable: "VISIT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VISIT_AUDIT",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorAccountId = table.Column<int>(type: "integer", nullable: false),
                    DecidingDentistId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BeforeJson = table.Column<string>(type: "jsonb", nullable: false),
                    AfterJson = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VISIT_AUDIT", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VISIT_AUDIT_EMPLOYEE_PROFILE_DecidingDentistId",
                        column: x => x.DecidingDentistId,
                        principalTable: "EMPLOYEE_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VISIT_AUDIT_USER_ACCOUNT_ActorAccountId",
                        column: x => x.ActorAccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VISIT_AUDIT_VISIT_VisitId",
                        column: x => x.VisitId,
                        principalTable: "VISIT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CLINICAL_SOURCE_REFERENCE_BranchId_PatientId",
                table: "CLINICAL_SOURCE_REFERENCE",
                columns: new[] { "BranchId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_CLINICAL_SOURCE_REFERENCE_ParentId_BranchId_PatientId",
                table: "CLINICAL_SOURCE_REFERENCE",
                columns: new[] { "ParentId", "BranchId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_CLINICAL_SOURCE_REFERENCE_PatientId",
                table: "CLINICAL_SOURCE_REFERENCE",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_RECEPTION_COMMAND_RECEIPT_VisitId",
                table: "RECEPTION_COMMAND_RECEIPT",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_VISIT_AppointmentId",
                table: "VISIT",
                column: "AppointmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VISIT_BranchId_QueueOrder",
                table: "VISIT",
                columns: new[] { "BranchId", "QueueOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_VISIT_Code",
                table: "VISIT",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VISIT_DentistId",
                table: "VISIT",
                column: "DentistId");

            migrationBuilder.CreateIndex(
                name: "IX_VISIT_EnteredBy",
                table: "VISIT",
                column: "EnteredBy");

            migrationBuilder.CreateIndex(
                name: "IX_VISIT_PatientId",
                table: "VISIT",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_VISIT_PriorityDentistId",
                table: "VISIT",
                column: "PriorityDentistId");

            migrationBuilder.CreateIndex(
                name: "IX_VISIT_AUDIT_ActorAccountId",
                table: "VISIT_AUDIT",
                column: "ActorAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_VISIT_AUDIT_DecidingDentistId",
                table: "VISIT_AUDIT",
                column: "DecidingDentistId");

            migrationBuilder.CreateIndex(
                name: "IX_VISIT_AUDIT_VisitId_OccurredAt",
                table: "VISIT_AUDIT",
                columns: new[] { "VisitId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CLINICAL_SOURCE_REFERENCE");

            migrationBuilder.DropTable(
                name: "RECEPTION_COMMAND_RECEIPT");

            migrationBuilder.DropTable(
                name: "VISIT_AUDIT");

            migrationBuilder.DropTable(
                name: "VISIT");
        }
    }
}
