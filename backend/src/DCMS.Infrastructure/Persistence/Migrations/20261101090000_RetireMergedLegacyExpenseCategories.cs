using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace DCMS.Infrastructure.Persistence.Migrations;
// docs/specs/S58-SETTINGS-MERGE.vi.md §3: MergeOperationalReferenceEntries copied the three seeded expense categories of the old
// module (Chi nhà cung cấp/Chi labo/Chi lương), which duplicate the reserved CHI-VATLIEU/CHI-LABO/CHI-LUONG under other names.
// Nothing references expense category ids, so the copies are only retired: kept for history, no longer reserved or selectable.
// Rows someone already edited (Version > 1) are left alone.
[DbContext(typeof(DcmsDbContext))]
[Migration("20261101090000_RetireMergedLegacyExpenseCategories")]
public sealed class RetireMergedLegacyExpenseCategories : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("""
            UPDATE "REFERENCE_ENTRY" SET "IsActive" = FALSE, "IsReserved" = FALSE, "Version" = "Version" + 1, "UpdatedAt" = now()
            WHERE "Kind" = 'expense-categories' AND "Version" = 1 AND "IsReserved"
              AND "Id" IN ('a158c8eb-4513-5435-8f6f-65b443e2aac4', '780d9a72-7507-5484-8893-de236b48ced9', 'cf1ed00f-e09d-554a-b97f-64d1dd0180fb');
            """);
    }

    // Reactivating would bring the duplicates back; leave the retired rows as they are.
    protected override void Down(MigrationBuilder m) { }
}
