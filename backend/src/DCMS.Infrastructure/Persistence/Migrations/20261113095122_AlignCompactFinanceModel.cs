using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignCompactFinanceModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PUSH_DEVICE was created by FirebasePushDevices; this migration only aligns the EF snapshot.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Preserve device registrations and the existing 33-table schema.
        }

    }
}
