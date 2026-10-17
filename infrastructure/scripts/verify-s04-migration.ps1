$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'import-development-env.ps1')

# Compare digests inside PostgreSQL: credentials and record contents never leave the database.
$snapshotSql = @'
SELECT json_build_object(
  'accounts', (SELECT md5(coalesce(string_agg((to_jsonb(u) - 'NormalizedPhone')::text, '' ORDER BY u."Id"), '')) FROM "USER_ACCOUNT" u),
  'roles', (SELECT md5(coalesce(string_agg(to_jsonb(r)::text, '' ORDER BY r."Id"), '')) FROM "ROLE" r),
  'assignedRoles', (SELECT md5(coalesce(string_agg(to_jsonb(r)::text, '' ORDER BY r."UserId", r."RoleId"), '')) FROM "USER_ACCOUNT_ROLE" r),
  'employees', (SELECT md5(coalesce(string_agg(to_jsonb(e)::text, '' ORDER BY e."Id"), '')) FROM "EMPLOYEE_PROFILE" e),
  'sessions', (SELECT md5(coalesce(string_agg(to_jsonb(s)::text, '' ORDER BY s."Id"), '')) FROM "AUTH_SESSION" s),
  'audits', (SELECT md5(coalesce(string_agg(to_jsonb(a)::text, '' ORDER BY a."Id"), '')) FROM "ACCOUNT_AUDIT" a)
);
'@
function Get-DataSnapshot {
    $result = $snapshotSql | docker compose --env-file (Join-Path $repositoryRoot '.env') -f (Join-Path $repositoryRoot 'infrastructure/docker/compose.yaml') exec -T postgres psql -X -v ON_ERROR_STOP=1 -At -U $settings['POSTGRES_USER'] -d $settings['POSTGRES_DB']
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($result)) { throw 'Cannot read development data digests.' }
    return $result
}
$before = Get-DataSnapshot
& (Join-Path $PSScriptRoot 'start-api.ps1') -Migrate
$after = Get-DataSnapshot
if ($before -ne $after) { throw 'Development data changed across migration; inspect before continuing.' }
Write-Output 'S04 migration verified: existing accounts (including password hashes), roles, employees, sessions and audit data are unchanged.'
