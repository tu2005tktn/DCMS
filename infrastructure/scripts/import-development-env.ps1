$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$environmentFile = Join-Path $repositoryRoot '.env'
if (-not (Test-Path -LiteralPath $environmentFile)) {
    throw "Missing .env. Create it from .env.example first."
}
$settings = @{}
foreach ($line in Get-Content -LiteralPath $environmentFile) {
    if ($line -match '^\s*(POSTGRES_DB|POSTGRES_USER|POSTGRES_PASSWORD|POSTGRES_PORT|JWT_SIGNING_KEY|DEV_SEED_PASSWORD|GOOGLE_WEB_CLIENT_ID)=(.*)$') {
        $settings[$Matches[1]] = $Matches[2].Trim()
    }
}
foreach ($key in @('POSTGRES_DB', 'POSTGRES_USER', 'POSTGRES_PASSWORD', 'POSTGRES_PORT')) {
    if ([string]::IsNullOrWhiteSpace($settings[$key])) { throw "Missing $key in .env." }
}
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
$connection['Host'] = 'localhost'
$connection['Port'] = $settings['POSTGRES_PORT']
$connection['Database'] = $settings['POSTGRES_DB']
$connection['Username'] = $settings['POSTGRES_USER']
$connection['Password'] = $settings['POSTGRES_PASSWORD']
$env:ConnectionStrings__DefaultConnection = $connection.ConnectionString
if ($settings['JWT_SIGNING_KEY']) { $env:Jwt__SigningKey = $settings['JWT_SIGNING_KEY'] }
if ($settings['DEV_SEED_PASSWORD']) { $env:DevSeed__Password = $settings['DEV_SEED_PASSWORD'] }
if ($settings['GOOGLE_WEB_CLIENT_ID']) { $env:Google__WebClientId = $settings['GOOGLE_WEB_CLIENT_ID'] }
$localDotnet = Join-Path $repositoryRoot '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
