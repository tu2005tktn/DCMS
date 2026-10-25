param([string]$Schema = ('dcms_reception_e2e_' + [Guid]::NewGuid().ToString('N')))
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'import-development-env.ps1')
if ($Schema -notmatch '^dcms_reception_e2e_[a-z0-9]+$') { throw 'Invalid synthetic test schema.' }
$env:DCMS_RECEPTION_E2E_SCHEMA = $Schema
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
$connection.set_ConnectionString($env:ConnectionStrings__DefaultConnection)
$connection['Search Path'] = $Schema
$env:ConnectionStrings__DefaultConnection = $connection.get_ConnectionString()
# The tool creates only a fresh synthetic schema. No development data is changed or removed.
Push-Location $repositoryRoot
try {
    & $dotnet run --project (Join-Path $repositoryRoot 'backend/tools/DCMS.ReceptionE2E') -- --prepare
    if ($LASTEXITCODE -ne 0) { throw 'Reception fixture failed.' }
} finally { Pop-Location }
Write-Output "Synthetic reception schema ready: $Schema"
