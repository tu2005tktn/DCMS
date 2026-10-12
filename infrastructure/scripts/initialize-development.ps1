$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$environmentFile = Join-Path $repositoryRoot '.env'
if (-not (Test-Path -LiteralPath $environmentFile)) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot '.env.example') -Destination $environmentFile
}
$content = [IO.File]::ReadAllText($environmentFile)
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
try {
    foreach ($key in @('JWT_SIGNING_KEY', 'DEV_SEED_PASSWORD')) {
        $pattern = '(?m)^' + $key + '=(.*)\r?$'
        $match = [regex]::Match($content, $pattern)
        if ($match.Success -and -not [string]::IsNullOrWhiteSpace($match.Groups[1].Value)) { continue }
        $bytes = New-Object byte[] 48
        $rng.GetBytes($bytes)
        $value = [Convert]::ToBase64String($bytes)
        if ($key -eq 'DEV_SEED_PASSWORD') { $value = 'Aa1!' + $value }
        $entry = $key + '=' + $value
        if ($match.Success) { $content = [regex]::Replace($content, $pattern, $entry) }
        else { $content = $content.TrimEnd() + "`r`n" + $entry + "`r`n" }
    }
    [IO.File]::WriteAllText($environmentFile, $content, [Text.UTF8Encoding]::new($false))
} finally { $rng.Dispose() }
Write-Output 'Development auth settings initialized in ignored .env. Existing values were preserved. Set POSTGRES_PASSWORD before first database startup.'
