<#
Nạp danh mục dụng cụ/vật liệu ban đầu từ infrastructure/seed/material-catalog.vi.json qua API hiện có.
Chạy lặp lại không tạo trùng: nhóm/mục đã có theo tên (không phân biệt hoa thường) thì bỏ qua.
Mặc định chỉ liệt kê việc sẽ làm; thêm -Apply để ghi thật. Cần tài khoản Dentist hiệu lực; mật khẩu nhập khi chạy, không lưu.
Quy tắc: docs/specs/SYSTEM-SETTINGS-MATERIAL-CATALOG.md.
#>
param(
    [Parameter(Mandatory)] [string] $UserName,
    [string] $ApiBase = 'http://localhost:5080',
    [switch] $Apply
)
$ErrorActionPreference = 'Stop'
$dataPath = Join-Path $PSScriptRoot '..\seed\material-catalog.vi.json'
$data = Get-Content -LiteralPath $dataPath -Raw -Encoding UTF8 | ConvertFrom-Json

$securePassword = Read-Host -AsSecureString 'Mật khẩu'
$password = [System.Net.NetworkCredential]::new('', $securePassword).Password
$signIn = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/auth/sign-in" -ContentType 'application/json; charset=utf-8' `
    -Body (@{ userName = $UserName; password = $password } | ConvertTo-Json)
$password = $null
$headers = @{ Authorization = "Bearer $($signIn.accessToken)" }

function Get-AllPages([string] $path) {
    $page = 1; $all = @()
    do {
        $result = Invoke-RestMethod -Headers $headers -Uri "$ApiBase$path$(if ($path.Contains('?')) { '&' } else { '?' })page=$page&pageSize=100"
        $all += @($result.items); $page++
    } while ($all.Count -lt $result.total)
    return $all
}
function Post-Json([string] $path, $body) {
    $json = $body | ConvertTo-Json
    Invoke-RestMethod -Method Post -Headers $headers -Uri "$ApiBase$path" -ContentType 'application/json; charset=utf-8' `
        -Body ([System.Text.Encoding]::UTF8.GetBytes($json))
}
function Key([string] $name) { $name.Trim().Normalize([Text.NormalizationForm]::FormC).ToUpperInvariant() }

$categories = @{}
foreach ($c in Get-AllPages '/api/system-settings/material-categories') { $categories[(Key $c.name)] = $c }
$created = 0; $skipped = 0
foreach ($group in $data.categories) {
    $category = $categories[(Key $group.name)]
    if (-not $category) {
        Write-Host "+ Nhóm: $($group.name)"
        if ($Apply) { $category = Post-Json '/api/system-settings/material-categories' @{ name = $group.name; requestKey = [guid]::NewGuid() }; $categories[(Key $group.name)] = $category }
        $created++
    }
    $existing = @{}
    if ($category) { foreach ($m in Get-AllPages "/api/system-settings/materials?categoryId=$($category.id)") { $existing[(Key $m.name)] = $true } }
    foreach ($name in $group.items) {
        if ($existing.ContainsKey((Key $name))) { $skipped++; continue }
        Write-Host "  + $($group.name) / $name"
        if ($Apply) { Post-Json '/api/system-settings/materials' @{ name = $name; categoryId = $category.id; requestKey = [guid]::NewGuid() } | Out-Null }
        $created++
    }
}
Write-Host "$(if ($Apply) { 'Đã tạo' } else { 'Sẽ tạo' }): $created; đã có sẵn: $skipped."
if (-not $Apply) { Write-Host 'Chạy lại với -Apply để ghi vào hệ thống.' }
