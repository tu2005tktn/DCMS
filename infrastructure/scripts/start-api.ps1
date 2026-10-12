param([switch]$Migrate, [switch]$SeedDemo, [switch]$Lan)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'import-development-env.ps1')
$runArguments = @('run', '--project', (Join-Path $repositoryRoot 'backend\src\DCMS.Api'), '--launch-profile', 'http')
$appArguments = @()
if ($Migrate) { $appArguments += '--migrate' }
if ($SeedDemo) { $appArguments += '--seed-demo' }
# -Lan listens on all interfaces so a phone on the same Wi-Fi (Expo Go) can reach the API. Development only.
if ($Lan) { $appArguments += @('--urls', 'http://0.0.0.0:5080') }
if ($appArguments.Count -gt 0) { $runArguments += @('--') + $appArguments }
& $dotnet @runArguments
if ($LASTEXITCODE -ne 0) { throw "API command failed with exit code $LASTEXITCODE." }
