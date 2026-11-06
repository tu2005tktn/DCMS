using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace DCMS.Infrastructure.Persistence.Migrations;
// S58 đợt 3: nullable catalog fields on PATIENT_PROFILE; existing records are never backfilled (GBR-176).
[DbContext(typeof(DcmsDbContext))]
[Migration("20261027233000_PatientCatalogFields")]
public sealed class PatientCatalogFields : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<DateOnly>("ProfileDate", "PATIENT_PROFILE", "date", nullable: true);
        m.AddColumn<Guid>("SourceId", "PATIENT_PROFILE", "uuid", nullable: true);
        m.AddColumn<Guid>("CustomerGroupId", "PATIENT_PROFILE", "uuid", nullable: true);
        m.AddColumn<Guid>("OccupationId", "PATIENT_PROFILE", "uuid", nullable: true);
        m.AddColumn<Guid[]>("IntakeHistoryIds", "PATIENT_PROFILE", "uuid[]", nullable: false, defaultValueSql: "'{}'::uuid[]");
        m.AddColumn<string>("IntakeHistoryOther", "PATIENT_PROFILE", "character varying(1000)", maxLength: 1000, nullable: true);
        m.AddColumn<DateTimeOffset>("IntakeRecordedAt", "PATIENT_PROFILE", "timestamp with time zone", nullable: true);
        m.AddColumn<int>("IntakeRecordedBy", "PATIENT_PROFILE", "integer", nullable: true);
        foreach (var column in new[] { "SourceId", "CustomerGroupId", "OccupationId" })
        {
            m.CreateIndex($"IX_PATIENT_PROFILE_{column}", "PATIENT_PROFILE", column);
            m.AddForeignKey($"FK_PATIENT_PROFILE_REFERENCE_ENTRY_{column}", "PATIENT_PROFILE", column, "REFERENCE_ENTRY", principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
    protected override void Down(MigrationBuilder m)
    {
        foreach (var column in new[] { "SourceId", "CustomerGroupId", "OccupationId" })
        {
            m.DropForeignKey($"FK_PATIENT_PROFILE_REFERENCE_ENTRY_{column}", "PATIENT_PROFILE");
            m.DropIndex($"IX_PATIENT_PROFILE_{column}", "PATIENT_PROFILE");
        }
        foreach (var column in new[] { "ProfileDate", "SourceId", "CustomerGroupId", "OccupationId", "IntakeHistoryIds", "IntakeHistoryOther", "IntakeRecordedAt", "IntakeRecordedBy" })
            m.DropColumn(column, "PATIENT_PROFILE");
    }
}
