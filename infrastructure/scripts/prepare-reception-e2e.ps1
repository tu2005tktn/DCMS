# Dot-source in a dedicated test terminal before starting the API on an isolated port.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'import-development-env.ps1')
$env:DCMS_RECEPTION_E2E_SCHEMA = 'dcms_reception_e2e_' + [Guid]::NewGuid().ToString('N')
$testConnection = [System.Data.Common.DbConnectionStringBuilder]::new()
$testConnection.set_ConnectionString($env:ConnectionStrings__DefaultConnection)
$testConnection['Search Path'] = $env:DCMS_RECEPTION_E2E_SCHEMA
$env:ConnectionStrings__DefaultConnection = $testConnection.ConnectionString
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Cors__AllowedOrigins__0 = 'http://localhost:8083'
$env:Cors__AllowedOrigins__1 = 'http://127.0.0.1:8083'
# Keep third-party delivery disabled in the isolated test environment.
Get-ChildItem Env:Smtp__* | Remove-Item
& $dotnet run --project (Join-Path $repositoryRoot 'backend/tools/DCMS.ReceptionE2E')
if ($LASTEXITCODE -ne 0) { throw 'E2E fixture preparation failed.' }
$env:E2E_DEMO_PASSWORD = $settings['DEV_SEED_PASSWORD']
$env:E2E_API_URL = 'http://127.0.0.1:5082'
$env:E2E_BASE_URL = 'http://127.0.0.1:8083'
Write-Output 'Isolated reception fixture ready; start API without re-importing the normal .env.'
