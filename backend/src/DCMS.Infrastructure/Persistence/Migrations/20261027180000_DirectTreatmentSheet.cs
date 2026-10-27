using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DCMS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DcmsDbContext))]
[Migration("20261027180000_DirectTreatmentSheet")]
public sealed class DirectTreatmentSheet : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<string>("EntrySource", "TREATMENT_PLAN_ITEM", type: "character varying(24)", maxLength: 24, nullable: true);
        m.AddColumn<int>("EntryRecordedBy", "TREATMENT_PLAN_ITEM", type: "integer", nullable: true);
        m.AddColumn<DateTimeOffset>("EntryRecordedAt", "TREATMENT_PLAN_ITEM", type: "timestamp with time zone", nullable: true);
        foreach (var name in new[] { "DisplayCompleted", "SheetDeleted", "HiddenFromSheet" })
            m.AddColumn<bool>(name, "TREATMENT_PLAN_ITEM", type: "boolean", nullable: false, defaultValue: false);
        m.AlterColumn<Guid>("VisitId", "CLINICAL_ACTIVITY", type: "uuid", nullable: true, oldClrType: typeof(Guid), oldType: "uuid");
        m.AddColumn<string>("SourceKind", "CLINICAL_ACTIVITY", type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Visit");
        m.AddColumn<string>("ServiceNameSnapshot", "CLINICAL_ACTIVITY", type: "character varying(160)", maxLength: 160, nullable: true);
        m.AddColumn<string>("LabWorkSnapshot", "CLINICAL_ACTIVITY", type: "character varying(1000)", maxLength: 1000, nullable: true);
        m.AddColumn<bool>("IsPlanTransfer", "CLINICAL_ACTIVITY", type: "boolean", nullable: false, defaultValue: false);
        m.CreateIndex("IX_CLINICAL_ACTIVITY_ItemId_PlanVersion", "CLINICAL_ACTIVITY", new[] { "ItemId", "PlanVersion" }, unique: true, filter: "\"IsPlanTransfer\" = TRUE");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM \"CLINICAL_ACTIVITY\" WHERE \"VisitId\" IS NULL) THEN RAISE EXCEPTION 'Cannot remove dossier activity support while such activities exist'; END IF; END $$;");
        m.DropIndex("IX_CLINICAL_ACTIVITY_ItemId_PlanVersion", "CLINICAL_ACTIVITY");
        foreach (var name in new[] { "SourceKind", "ServiceNameSnapshot", "LabWorkSnapshot", "IsPlanTransfer" }) m.DropColumn(name, "CLINICAL_ACTIVITY");
        m.AlterColumn<Guid>("VisitId", "CLINICAL_ACTIVITY", type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
        foreach (var name in new[] { "EntrySource", "EntryRecordedBy", "EntryRecordedAt", "DisplayCompleted", "SheetDeleted", "HiddenFromSheet" }) m.DropColumn(name, "TREATMENT_PLAN_ITEM");
    }
}
