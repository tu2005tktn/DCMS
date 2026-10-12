using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DCMS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DcmsDbContext))]
[Migration("20261012000000_EnsureGuestRole")]
public sealed class EnsureGuestRole : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        INSERT INTO "ROLE" ("Name", "NormalizedName", "ConcurrencyStamp")
        VALUES ('Guest', 'GUEST', 'uc33-guest-role')
        ON CONFLICT ("NormalizedName") DO NOTHING;
        """);

    // A rollback must not remove a role that might already be assigned to an account.
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
