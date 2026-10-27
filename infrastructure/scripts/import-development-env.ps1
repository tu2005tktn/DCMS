$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$environmentFile = Join-Path $repositoryRoot '.env'
if (-not (Test-Path -LiteralPath $environmentFile)) {
    throw "Missing .env. Create it from .env.example first."
}
$settings = @{}
foreach ($line in Get-Content -LiteralPath $environmentFile) {
    if ($line -match '^\s*(POSTGRES_DB|POSTGRES_USER|POSTGRES_PASSWORD|POSTGRES_PORT|JWT_SIGNING_KEY|DEV_SEED_PASSWORD|GOOGLE_WEB_CLIENT_ID|SMTP_HOST|SMTP_PORT|SMTP_ENABLE_SSL|SMTP_USERNAME|SMTP_PASSWORD|SMTP_FROM_ADDRESS|SMTP_FROM_NAME|CLINICAL_DOCUMENTS_STORAGE_PATH|CLAMAV_HOST|CLAMAV_PORT)=(.*)$') {
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
$env:ClinicalDocuments__StoragePath = if ($settings['CLINICAL_DOCUMENTS_STORAGE_PATH']) { $settings['CLINICAL_DOCUMENTS_STORAGE_PATH'] } else { Join-Path $env:LOCALAPPDATA 'DCMS\clinical-documents' }
if ($settings['CLAMAV_HOST']) { $env:ClinicalDocuments__ClamAvHost = $settings['CLAMAV_HOST'] }
if ($settings['CLAMAV_PORT']) { $env:ClinicalDocuments__ClamAvPort = $settings['CLAMAV_PORT'] }
if ($settings['JWT_SIGNING_KEY']) { $env:Jwt__SigningKey = $settings['JWT_SIGNING_KEY'] }
if ($settings['DEV_SEED_PASSWORD']) { $env:DevSeed__Password = $settings['DEV_SEED_PASSWORD'] }
if ($settings['GOOGLE_WEB_CLIENT_ID']) { $env:Google__WebClientId = $settings['GOOGLE_WEB_CLIENT_ID'] }
# Optional SMTP for S03 recovery email; values stay in the untracked .env.
$smtp = @{ SMTP_HOST = 'Host'; SMTP_PORT = 'Port'; SMTP_ENABLE_SSL = 'EnableSsl'; SMTP_USERNAME = 'UserName'
    SMTP_PASSWORD = 'Password'; SMTP_FROM_ADDRESS = 'FromAddress'; SMTP_FROM_NAME = 'FromName' }
foreach ($key in $smtp.Keys) { if ($settings[$key]) { Set-Item -Path "env:Smtp__$($smtp[$key])" -Value $settings[$key] } }
$localDotnet = Join-Path $repositoryRoot '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
