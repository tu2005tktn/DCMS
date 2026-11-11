param([switch]$NoExpo)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$envFile = Join-Path $repo '.env'
if (-not (Test-Path -LiteralPath $envFile)) { throw 'Missing root .env.' }
$remote = @{}
foreach ($line in Get-Content -LiteralPath $envFile) {
    if ($line -match '^\s*(KEY_NGROK|LINK_NGROK)=(.*)$') { $remote[$Matches[1]] = $Matches[2].Trim() }
}
if ([string]::IsNullOrWhiteSpace($remote['KEY_NGROK'])) { throw 'Missing KEY_NGROK in root .env.' }
$uri = $null
if (-not [Uri]::TryCreate($remote['LINK_NGROK'], [UriKind]::Absolute, [ref]$uri) -or
    $uri.Scheme -ne 'https' -or $uri.Host -notmatch '^[a-z0-9.-]+\.ngrok(-free)?\.(app|dev)$' -or
    $uri.AbsolutePath -ne '/' -or $uri.Query -or $uri.Fragment) {
    throw 'LINK_NGROK must be a reserved HTTPS ngrok root URL.'
}
$publicUrl = $uri.GetLeftPart([UriPartial]::Authority)
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker CLI is required.' }
if (-not $NoExpo -and -not (Get-Command npm.cmd -ErrorAction SilentlyContinue)) { throw 'npm is required.' }

$artifactDir = Join-Path $repo '.artifacts'
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
$apiProcess = $null
$ngrokProcess = $null
try {
    & docker compose --env-file $envFile -f (Join-Path $repo 'infrastructure/docker/compose.yaml') up -d
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL failed to start.' }
    try { $null = Invoke-RestMethod 'http://127.0.0.1:5080/health' -TimeoutSec 3 }
    catch {
        $apiProcess = Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'start-api.ps1')) -WorkingDirectory $repo -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $artifactDir 'remote-api.out.log') -RedirectStandardError (Join-Path $artifactDir 'remote-api.err.log')
    }
    $healthy = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        try {
            $health = Invoke-RestMethod 'http://127.0.0.1:5080/health' -TimeoutSec 3
            if ($health.api -eq 'healthy' -and $health.database -eq 'healthy') { $healthy = $true; break }
        } catch { Start-Sleep -Seconds 2 }
        if ($apiProcess -and $apiProcess.HasExited) { break }
    }
    if (-not $healthy) { throw 'DCMS API or database did not become healthy. Check .artifacts/remote-api.err.log.' }

    # Docker passes this environment variable to the container without putting the token in command-line arguments.
    $env:NGROK_AUTHTOKEN = $remote['KEY_NGROK']
    $ngrokProcess = Start-Process -FilePath 'docker.exe' -ArgumentList @('run', '--rm', '--name', 'dcms-ngrok', '-e', 'NGROK_AUTHTOKEN', '-p', '127.0.0.1:4040:4040', 'ngrok/ngrok:latest', 'http', 'host.docker.internal:5080', '--url', $publicUrl) -WorkingDirectory $repo -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $artifactDir 'remote-ngrok.out.log') -RedirectStandardError (Join-Path $artifactDir 'remote-ngrok.err.log')
    Remove-Item Env:NGROK_AUTHTOKEN -ErrorAction SilentlyContinue
    $ready = $false
    for ($attempt = 0; $attempt -lt 45; $attempt++) {
        try {
            $tunnels = Invoke-RestMethod 'http://127.0.0.1:4040/api/tunnels' -TimeoutSec 3
            if ($tunnels.tunnels | Where-Object { $_.public_url -eq $publicUrl }) { $ready = $true; break }
        } catch { }
        if ($ngrokProcess.HasExited) { break }
        Start-Sleep -Seconds 2
    }
    if (-not $ready) { throw 'ngrok tunnel did not become ready. Check .artifacts/remote-ngrok.err.log.' }
    $response = Invoke-WebRequest "$publicUrl/health" -Headers @{ 'ngrok-skip-browser-warning' = 'true' } -TimeoutSec 15 -UseBasicParsing
    if ($response.StatusCode -ne 200) { throw 'Public API health check failed.' }

    $mobileEnv = Join-Path $repo 'mobile/.env.local'
    Set-Content -LiteralPath $mobileEnv -Value "EXPO_PUBLIC_API_URL=$publicUrl" -Encoding utf8
    Write-Host "API ready: $publicUrl"
    Write-Host 'Mobile API URL written to mobile/.env.local. Keep this terminal open.'
    if (-not $NoExpo) {
        $expoPort = $null
        $listeners = @(netstat -ano -p tcp | Where-Object { $_ -match '\sLISTENING\s' })
        foreach ($candidate in 8081..8090) {
            if ($listeners | Where-Object { $_ -match ":$candidate\s" }) { continue }
            try {
                $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Any, $candidate)
                $listener.Start()
                $listener.Stop()
                $expoPort = $candidate
                break
            } catch { }
        }
        if (-not $expoPort) { throw 'No available Expo port between 8081 and 8090.' }
        Push-Location (Join-Path $repo 'mobile')
        try {
            & npm.cmd run start -- --tunnel --port $expoPort
            if ($LASTEXITCODE -ne 0) { throw "Expo failed with exit code $LASTEXITCODE." }
        }
        finally { Pop-Location }
    }
} finally {
    Remove-Item Env:NGROK_AUTHTOKEN -ErrorAction SilentlyContinue
    if ($ngrokProcess) { & docker stop dcms-ngrok 2>$null | Out-Null }
    if ($apiProcess -and -not $apiProcess.HasExited) { Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue }
}
