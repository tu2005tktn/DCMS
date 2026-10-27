using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VisitExaminationsAndClinicalDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CLINICAL_DOCUMENT",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    DossierId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FileState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UploaderId = table.Column<int>(type: "integer", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CLINICAL_DOCUMENT", x => x.Id);
                    table.CheckConstraint("CK_CLINICAL_DOCUMENT_State", "\"FileState\" IN ('PendingScan','Ready','Rejected')");
                    table.ForeignKey(
                        name: "FK_CLINICAL_DOCUMENT_TREATMENT_DOSSIER_DossierId",
                        column: x => x.DossierId,
                        principalTable: "TREATMENT_DOSSIER",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CLINICAL_DOCUMENT_USER_ACCOUNT_UploaderId",
                        column: x => x.UploaderId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CLINICAL_DOCUMENT_VISIT_VisitId",
                        column: x => x.VisitId,
                        principalTable: "VISIT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DOCUMENT_RECEIPT",
                columns: table => new
                {
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DOCUMENT_RECEIPT", x => new { x.ActorId, x.RequestKey });
                });

            migrationBuilder.CreateTable(
                name: "EXAMINATION_RECEIPT",
                columns: table => new
                {
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXAMINATION_RECEIPT", x => new { x.ActorId, x.RequestKey });
                    table.ForeignKey(
                        name: "FK_EXAMINATION_RECEIPT_VISIT_VisitId",
                        column: x => x.VisitId,
                        principalTable: "VISIT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VISIT_EXAMINATION",
                columns: table => new
                {
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentJson = table.Column<string>(type: "jsonb", nullable: false),
                    Confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VISIT_EXAMINATION", x => x.VisitId);
                    table.ForeignKey(
                        name: "FK_VISIT_EXAMINATION_USER_ACCOUNT_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VISIT_EXAMINATION_VISIT_VisitId",
                        column: x => x.VisitId,
                        principalTable: "VISIT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DOCUMENT_AUDIT",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DOCUMENT_AUDIT", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DOCUMENT_AUDIT_CLINICAL_DOCUMENT_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "CLINICAL_DOCUMENT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DOCUMENT_REVIEW",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedBy = table.Column<int>(type: "integer", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DOCUMENT_REVIEW", x => new { x.DocumentId, x.VisitId });
                    table.ForeignKey(
                        name: "FK_DOCUMENT_REVIEW_CLINICAL_DOCUMENT_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "CLINICAL_DOCUMENT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DOCUMENT_REVIEW_USER_ACCOUNT_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DOCUMENT_REVIEW_VISIT_VisitId",
                        column: x => x.VisitId,
                        principalTable: "VISIT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CLINICAL_DOCUMENT_DossierId",
                table: "CLINICAL_DOCUMENT",
                column: "DossierId");

            migrationBuilder.CreateIndex(
                name: "IX_CLINICAL_DOCUMENT_PatientId_VisitId",
                table: "CLINICAL_DOCUMENT",
                columns: new[] { "PatientId", "VisitId" });

            migrationBuilder.CreateIndex(
                name: "IX_CLINICAL_DOCUMENT_UploaderId",
                table: "CLINICAL_DOCUMENT",
                column: "UploaderId");

            migrationBuilder.CreateIndex(
                name: "IX_CLINICAL_DOCUMENT_VisitId",
                table: "CLINICAL_DOCUMENT",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_DOCUMENT_AUDIT_DocumentId_OccurredAt",
                table: "DOCUMENT_AUDIT",
                columns: new[] { "DocumentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DOCUMENT_REVIEW_ReviewedBy",
                table: "DOCUMENT_REVIEW",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DOCUMENT_REVIEW_VisitId",
                table: "DOCUMENT_REVIEW",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_EXAMINATION_RECEIPT_VisitId",
                table: "EXAMINATION_RECEIPT",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_VISIT_EXAMINATION_UpdatedBy",
                table: "VISIT_EXAMINATION",
                column: "UpdatedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DOCUMENT_AUDIT");

            migrationBuilder.DropTable(
                name: "DOCUMENT_RECEIPT");

            migrationBuilder.DropTable(
                name: "DOCUMENT_REVIEW");

            migrationBuilder.DropTable(
                name: "EXAMINATION_RECEIPT");

            migrationBuilder.DropTable(
                name: "VISIT_EXAMINATION");

            migrationBuilder.DropTable(
                name: "CLINICAL_DOCUMENT");
        }
    }
}
