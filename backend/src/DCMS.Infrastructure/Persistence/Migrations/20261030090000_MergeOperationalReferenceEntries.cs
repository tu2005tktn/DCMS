using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace DCMS.Infrastructure.Persistence.Migrations;
// docs/specs/S58-SETTINGS-MERGE.vi.md: copy the operational catalog into REFERENCE_ENTRY keeping Id/Version so examination
// diagnosis snapshots stay valid. Insert-only; rows that clash with an existing entry stay in the legacy table untouched.
[DbContext(typeof(DcmsDbContext))]
[Migration("20261030090000_MergeOperationalReferenceEntries")]
public sealed class MergeOperationalReferenceEntries : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("""
            INSERT INTO "REFERENCE_ENTRY" ("Id","Kind","Code","Name","NormalizedName","Description","IsReserved","IsActive","Version","CreatedAt","CreatedBy","UpdatedAt","UpdatedBy")
            SELECT o."Id", o."Kind", NULLIF(upper(btrim(o."Code")), ''), o."Name", o."NormalizedName", o."Description", o."Reserved", o."Active", o."Version",
                   o."CreatedAt", NULLIF(o."CreatedBy", 0), o."CreatedAt", NULLIF(o."CreatedBy", 0)
            FROM "OPERATIONAL_REFERENCE_ENTRY" o
            WHERE NOT EXISTS (SELECT 1 FROM "REFERENCE_ENTRY" r WHERE r."Id" = o."Id"
                    OR (r."Kind" = o."Kind" AND r."NormalizedName" = o."NormalizedName")
                    OR (r."Kind" = o."Kind" AND r."Code" IS NOT NULL AND r."Code" = upper(btrim(o."Code"))))
              -- Two legacy rows can collapse to the same upper-case code; keep the first so the unique index holds.
              AND NOT EXISTS (SELECT 1 FROM "OPERATIONAL_REFERENCE_ENTRY" p WHERE p."Kind" = o."Kind" AND p."Id" < o."Id"
                    AND upper(btrim(p."Code")) = upper(btrim(o."Code")))
            ON CONFLICT DO NOTHING;
            """);
    }

    // Copied rows may already be referenced by examinations; leave them in place.
    protected override void Down(MigrationBuilder m) { }
}
