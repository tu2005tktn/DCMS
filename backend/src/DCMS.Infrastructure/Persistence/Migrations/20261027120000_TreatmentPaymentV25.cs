using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DCMS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DcmsDbContext))]
[Migration("20261027120000_TreatmentPaymentV25")]
public sealed class TreatmentPaymentV25 : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<string>("Unit", "SCHEDULING_SERVICE", type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "lần");
        m.AddColumn<Guid>("SourceExaminationId", "TREATMENT_DOSSIER", type: "uuid", nullable: true);
        m.Sql("ALTER TABLE \"TREATMENT_DOSSIER\" ADD COLUMN IF NOT EXISTS \"SourceExaminationVersion\" integer;");
        m.AddColumn<string>("Note", "TREATMENT_PLAN_ITEM", type: "character varying(1000)", maxLength: 1000, nullable: true);
        m.AddColumn<int>("ResponsibleDentistId", "TREATMENT_PLAN_ITEM", type: "integer", nullable: true);
        m.AddColumn<int>("AssistantId", "TREATMENT_PLAN_ITEM", type: "integer", nullable: true);
        m.AddColumn<int>("AssistantId", "CLINICAL_ACTIVITY", type: "integer", nullable: true);
        m.AddColumn<int>("RecordedBy", "CLINICAL_ACTIVITY", type: "integer", nullable: true);
        m.AddColumn<string>("FeeStatus", "CLINICAL_ACTIVITY", type: "character varying(24)", maxLength: 24, nullable: false, defaultValue: "Unavailable");
        m.AddColumn<long>("DiscountSnapshotVnd", "CLINICAL_ACTIVITY", type: "bigint", nullable: false, defaultValue: 0L);
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropColumn("Unit", "SCHEDULING_SERVICE");
        foreach (var c in new[] { "SourceExaminationId", "SourceExaminationVersion" }) m.DropColumn(c, "TREATMENT_DOSSIER");
        foreach (var c in new[] { "Note", "ResponsibleDentistId", "AssistantId" }) m.DropColumn(c, "TREATMENT_PLAN_ITEM");
        foreach (var c in new[] { "AssistantId", "RecordedBy", "FeeStatus", "DiscountSnapshotVnd" }) m.DropColumn(c, "CLINICAL_ACTIVITY");
    }
}
