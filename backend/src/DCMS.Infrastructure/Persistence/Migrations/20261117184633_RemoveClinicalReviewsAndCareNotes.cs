using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveClinicalReviewsAndCareNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // S63 reviews and care notes are removed; their rows cannot be restored by Down.
            migrationBuilder.Sql("DELETE FROM \"CLINICAL_ACTIVITY\" WHERE \"ProcessKind\" IN ('ClinicalReview', 'ClinicalCareNote');");

            migrationBuilder.DropIndex(
                name: "IX_CLINICAL_ACTIVITY_SourceActivityId_SourceItemId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropIndex(
                name: "IX_CLINICAL_ACTIVITY_VisitId_RecordedAt",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "ActorId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "Basis",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "Complaint",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "Content",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "Coverage",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "CoveredComponent",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "DentistAccountId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "Exclusions",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "Findings",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "NextAction",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "Policy",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "PolicyVersion",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "RecordedAt",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "Response",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "ReviewId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "SourceItemId",
                table: "CLINICAL_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "ReviewId",
                table: "CARE_TASK");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActorId",
                table: "CLINICAL_ACTIVITY",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Basis",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Complaint",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Coverage",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoveredComponent",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DentistAccountId",
                table: "CLINICAL_ACTIVITY",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Exclusions",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Findings",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextAction",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Policy",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyVersion",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RecordedAt",
                table: "CLINICAL_ACTIVITY",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Response",
                table: "CLINICAL_ACTIVITY",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewId",
                table: "CLINICAL_ACTIVITY",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceItemId",
                table: "CLINICAL_ACTIVITY",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewId",
                table: "CARE_TASK",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CLINICAL_ACTIVITY_SourceActivityId_SourceItemId",
                table: "CLINICAL_ACTIVITY",
                columns: new[] { "SourceActivityId", "SourceItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_CLINICAL_ACTIVITY_VisitId_RecordedAt",
                table: "CLINICAL_ACTIVITY",
                columns: new[] { "VisitId", "RecordedAt" });
        }
    }
}
