param([switch]$Migrate, [switch]$SeedDemo)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'import-development-env.ps1')
$runArguments = @('run', '--project', (Join-Path $repositoryRoot 'backend\src\DCMS.Api'), '--launch-profile', 'http')
if ($Migrate -or $SeedDemo) {
    $runArguments += '--'
    if ($Migrate) { $runArguments += '--migrate' }
    if ($SeedDemo) { $runArguments += '--seed-demo' }
}
& $dotnet @runArguments
if ($LASTEXITCODE -ne 0) { throw "API command failed with exit code $LASTEXITCODE." }
