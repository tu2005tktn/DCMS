# Dot-source in a dedicated terminal. Creates synthetic data in a new PostgreSQL schema only.
param([switch]$UseRealAntivirus)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'prepare-reception-e2e.ps1')
# Isolated browser fixtures cover PendingScan deterministically, regardless of local antivirus.
# Use -UseRealAntivirus only for a separate real-scanner proof. The manual API is untouched.
if (-not $UseRealAntivirus) { Remove-Item Env:ClinicalDocuments__ClamAvHost -ErrorAction SilentlyContinue }
$env:ASPNETCORE_URLS = 'http://localhost:5084'
$env:Cors__AllowedOrigins__0 = 'http://localhost:8084'
$env:Cors__AllowedOrigins__1 = 'http://127.0.0.1:8084'
$env:ClinicalDocuments__StoragePath = Join-Path $env:LOCALAPPDATA ('DCMS\clinical-proof\' + $env:DCMS_RECEPTION_E2E_SCHEMA)
$env:E2E_API_URL = 'http://localhost:5084'
$env:E2E_WEB_URL = 'http://localhost:8084'
$env:VITE_API_BASE_URL = $env:E2E_API_URL
Write-Output 'Clinical fixture ready: API 5084 / web 8084. Keep this environment when starting the API.'
