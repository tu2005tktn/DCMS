# Runs the backend integration tests with code coverage (docs/testing/INTEGRATION-TEST-COVERAGE.vi.md).
# Needs PostgreSQL from infrastructure/docker/compose.yaml and the repository .env.
param(
    [string]$Name = (Get-Date -Format 'yyyyMMdd-HHmmss'),
    [string]$Filter = ''
)
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repositoryRoot
. (Join-Path $repositoryRoot 'infrastructure\scripts\import-development-env.ps1') | Out-Null
$ErrorActionPreference = 'Continue'

$dotnet = Join-Path $repositoryRoot '.tools\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }
$results = Join-Path $repositoryRoot "backend\tests\DCMS.IntegrationTests\TestResults\$Name"
# Start clean: ReportGenerator merges every coverage file under the folder, so leftovers from an earlier run would count.
Remove-Item $results -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $results | Out-Null
$env:DCMS_IT_TRACE = Join-Path $results 'it-trace.json'
$env:DCMS_IT_HTTP_LOG = Join-Path $results 'http-exchanges.jsonl'

$arguments = @('test', 'backend/tests/DCMS.IntegrationTests', '--settings', 'backend/tests/coverage.runsettings',
    '--collect:XPlat Code Coverage', '--results-directory', $results, '--logger', 'trx;LogFileName=integration.trx',
    '--logger', 'console;verbosity=minimal')
if ($Filter) { $arguments += @('--filter', $Filter) }
& $dotnet @arguments
$testExit = $LASTEXITCODE

& $dotnet tool restore | Out-Null
& $dotnet reportgenerator "-reports:$results\**\coverage.cobertura.xml" "-targetdir:$results\coverage-report" `
    '-reporttypes:Html;TextSummary;JsonSummary' | Out-Null
Get-Content (Join-Path $results 'coverage-report\Summary.txt') -TotalCount 20
Write-Host "Results: $results (exit $testExit)"
exit $testExit
