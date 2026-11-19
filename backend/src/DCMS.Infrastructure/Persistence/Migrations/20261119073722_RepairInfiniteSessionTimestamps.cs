using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Data-only repair: S58StaffIdleRuntime defaulted AUTH_SESSION.LastInteractiveActivityAt to
    /// DateTimeOffset.MinValue (stored by PostgreSQL as -infinity) and ConsolidateAccountStorage copied
    /// sessions into USER_ACCOUNT.AuthData with to_jsonb, writing the string "-infinity". System.Text.Json
    /// cannot read that as DateTimeOffset, so loading the account (sign-in) failed with 503.
    /// See docs/specs/ACCOUNT-TWO-TABLES.vi.md.
    /// </summary>
    public partial class RepairInfiniteSessionTimestamps : Migration
    {
        // A jsonb value that is the string "infinity" or "-infinity".
        private static string IsInfinite(string value) =>
            $"(jsonb_typeof({value}) = 'string' AND ({value} #>> '{{}}') IN ('infinity', '-infinity'))";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Per affected session: LastSeenAt <- CreatedAt; LastInteractiveActivityAt <- repaired LastSeenAt
            // (or CreatedAt); any other infinite timestamp in the session or its refresh tokens <- CreatedAt.
            // A broken CreatedAt falls back to the Unix epoch so no session becomes active again. Idempotent.
            migrationBuilder.Sql($"""
                WITH fixed AS (
                    SELECT a."Id",
                        jsonb_agg(
                            CASE WHEN s::text ILIKE '%infinity%' THEN (
                                SELECT jsonb_object_agg(e.key,
                                    CASE
                                        WHEN e.key = 'LastInteractiveActivityAt' AND {IsInfinite("e.value")}
                                            THEN CASE WHEN s->'LastSeenAt' IS NULL OR {IsInfinite("s->'LastSeenAt'")}
                                                THEN c.created ELSE s->'LastSeenAt' END
                                        WHEN {IsInfinite("e.value")} THEN c.created
                                        WHEN e.key = 'RefreshTokens' AND jsonb_typeof(e.value) = 'array' THEN (
                                            SELECT COALESCE(jsonb_agg(
                                                CASE WHEN jsonb_typeof(r) = 'object' THEN (
                                                    SELECT jsonb_object_agg(rk.key,
                                                        CASE WHEN {IsInfinite("rk.value")} THEN c.created ELSE rk.value END)
                                                    FROM jsonb_each(r) rk)
                                                ELSE r END
                                                ORDER BY ri), '[]'::jsonb)
                                            FROM jsonb_array_elements(e.value) WITH ORDINALITY rt(r, ri))
                                        ELSE e.value
                                    END)
                                FROM jsonb_each(s) e)
                            ELSE s END
                            ORDER BY i) AS sessions
                    FROM "USER_ACCOUNT" a
                    CROSS JOIN LATERAL jsonb_array_elements(a."AuthData"->'Sessions') WITH ORDINALITY t(s, i)
                    CROSS JOIN LATERAL (
                        SELECT CASE
                            WHEN jsonb_typeof(s->'CreatedAt') = 'string' AND NOT {IsInfinite("s->'CreatedAt'")}
                                THEN s->'CreatedAt'
                            ELSE to_jsonb('1970-01-01T00:00:00+00:00'::text)
                        END AS created) c
                    WHERE jsonb_typeof(a."AuthData"->'Sessions') = 'array'
                        AND (a."AuthData"->'Sessions')::text ILIKE '%infinity%'
                    GROUP BY a."Id")
                UPDATE "USER_ACCOUNT" a
                SET "AuthData" = jsonb_set(a."AuthData", ARRAY['Sessions'], f.sessions)
                FROM fixed f
                WHERE a."Id" = f."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty: the infinite timestamps were invalid data and are not restored.
        }
    }
}
