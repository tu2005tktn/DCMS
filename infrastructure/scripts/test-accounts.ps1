param([switch]$Browser)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'import-development-env.ps1')
if ($Browser) {
    $env:E2E_DEMO_PASSWORD = $settings['DEV_SEED_PASSWORD']
    if ([string]::IsNullOrWhiteSpace($env:E2E_DEMO_PASSWORD)) { throw 'DEV_SEED_PASSWORD is required for browser tests.' }
    Push-Location (Join-Path $repositoryRoot 'web')
    try { npm.cmd run test:e2e; if ($LASTEXITCODE -ne 0) { throw 'Browser tests failed.' } }
    finally { Pop-Location; Remove-Item Env:E2E_DEMO_PASSWORD -ErrorAction SilentlyContinue }
} else {
    & $dotnet test (Join-Path $repositoryRoot 'backend\DCMS.sln') --logger 'console;verbosity=minimal'
    if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed.' }
}
