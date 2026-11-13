$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'import-development-env.ps1')

# Compare digests inside PostgreSQL: credentials and record contents never leave the database.
$snapshotSql = @'
SELECT json_build_object(
  'accounts', (SELECT md5(coalesce(string_agg((to_jsonb(u) - ARRAY['NormalizedPhone','EmployeeId','AssignedRoles','GoogleSubject','AccessVersion','EmployeeCode','EmployeeFullName','GroupId','BranchIds','Claims','EmailVerifications','IdentityTokens','Logins','Recoveries','RecoveryDeliveries','Sessions'])::text, '' ORDER BY u."Id"), '')) FROM "USER_ACCOUNT" u),
  'roles', (SELECT md5(coalesce(string_agg(to_jsonb(r)::text, '' ORDER BY r."Id"), '')) FROM (__ROLES__) r),
  'assignedRoles', (SELECT md5(coalesce(string_agg(to_jsonb(r)::text, '' ORDER BY r."UserId", r."RoleId"), '')) FROM (__ASSIGNED_ROLES__) r),
  'employees', (SELECT md5(coalesce(string_agg(to_jsonb(e)::text, '' ORDER BY e."Id"), '')) FROM (__EMPLOYEES__) e),
  'sessions', (SELECT md5(coalesce(string_agg(s.value::text, '' ORDER BY s.value->>'Id'), '')) FROM (__SESSIONS__) s),
  'audits', (SELECT md5(coalesce(string_agg(to_jsonb(a)::text, '' ORDER BY a."Id"), '')) FROM (__ACCOUNT_AUDITS__) a)
);
'@
function Get-DataSnapshot {
    $databaseArguments = @('compose', '--env-file', (Join-Path $repositoryRoot '.env'),
        '-f', (Join-Path $repositoryRoot 'infrastructure/docker/compose.yaml'), 'exec', '-T', 'postgres',
        'psql', '-X', '-v', 'ON_ERROR_STOP=1', '-At', '-U', $settings['POSTGRES_USER'], '-d', $settings['POSTGRES_DB'])
    $sharedAudit = 'SELECT to_regclass(''"AUDIT_LOG"'') IS NOT NULL;' | docker @databaseArguments
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the audit store.' }
    $auditSource = if ($sharedAudit.Trim() -eq 't') {
        'SELECT "LegacyNumber" AS "Id", "ActorId", "TargetId", "OccurredAt", "Action", "Reason", "BeforeJson", "AfterJson" FROM "AUDIT_LOG" WHERE "AuditType" = ''AccountAudit'''
    } else {
        'SELECT "Id", "ActorId", "TargetId", "OccurredAt", "Action", "Reason", "BeforeJson", "AfterJson" FROM "ACCOUNT_AUDIT"'
    }
    $legacyIdentity = 'SELECT to_regclass(''"ROLE"'') IS NOT NULL;' | docker @databaseArguments
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the account store.' }
    if ($legacyIdentity.Trim() -eq 't') {
        $roleSource = 'SELECT * FROM "ROLE"'
        $assignedSource = 'SELECT * FROM "USER_ACCOUNT_ROLE"'
        $employeeSource = 'SELECT * FROM "EMPLOYEE_PROFILE"'
        $sessionSource = 'SELECT to_jsonb(s) AS value FROM "AUTH_SESSION" s'
    } else {
        $roleSource = 'SELECT "IdentityRoleId" AS "Id", "Name", "NormalizedName", "RoleConcurrencyStamp" AS "ConcurrencyStamp" FROM "ACCESS_GROUP" WHERE "RecordKind"=''IdentityRole'''
        $assignedSource = 'SELECT u."Id" AS "UserId", g."IdentityRoleId" AS "RoleId" FROM "USER_ACCOUNT" u CROSS JOIN LATERAL unnest(u."AssignedRoles") n JOIN "ACCESS_GROUP" g ON g."Name"=n AND g."RecordKind"=''IdentityRole'''
        $employeeSource = 'SELECT "EmployeeId" AS "Id", "Id" AS "AccountId", "EmployeeCode", "EmployeeFullName" AS "FullName" FROM "USER_ACCOUNT" WHERE "EmployeeCode" IS NOT NULL'
        $sessionSource = 'SELECT s.value - ''RefreshTokens'' AS value FROM "USER_ACCOUNT" u CROSS JOIN LATERAL jsonb_array_elements(u."Sessions") s'
    }
    $query = $snapshotSql.Replace('__ACCOUNT_AUDITS__', $auditSource).Replace('__ROLES__', $roleSource).Replace('__ASSIGNED_ROLES__', $assignedSource).Replace('__EMPLOYEES__', $employeeSource).Replace('__SESSIONS__', $sessionSource)
    $result = $query | docker @databaseArguments
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($result)) { throw 'Cannot read development data digests.' }
    return $result
}
$before = Get-DataSnapshot
& (Join-Path $PSScriptRoot 'start-api.ps1') -Migrate
$after = Get-DataSnapshot
if ($before -ne $after) { throw 'Development data changed across migration; inspect before continuing.' }
Write-Output 'S04 migration verified: existing accounts (including password hashes), roles, employees, sessions and audit data are unchanged.'
