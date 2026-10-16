$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
. (Join-Path $PSScriptRoot 'import-development-env.ps1')
$localDotnet = Join-Path $repositoryRoot '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
Push-Location $PSScriptRoot\SeedTestPatients
try {
    & $dotnet run --project . 2>&1 | Select-Object -Last 20
} finally {
    Pop-Location
}
