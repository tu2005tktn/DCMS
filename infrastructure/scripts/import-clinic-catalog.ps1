<#
Nạp dữ liệu mẫu danh mục phòng khám (dịch vụ, kế hoạch điều trị, vật liệu, labo) vào PostgreSQL chạy bằng Docker.
Chạy lặp lại không nhân đôi. Cần đã chạy start-api.ps1 -Migrate -SeedDemo. Chỉ dùng cho Development.
Quy tắc: docs/specs/CLINIC-CATALOG-SQL-SEED.md.
#>
param([string] $Service = 'postgres')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$envFile = Join-Path $root '.env'
if (-not (Test-Path -LiteralPath $envFile)) { throw 'Thiếu .env. Tạo từ .env.example trước.' }
$s = @{}
foreach ($line in Get-Content -LiteralPath $envFile) { if ($line -match '^\s*(POSTGRES_DB|POSTGRES_USER)=(.*)$') { $s[$Matches[1]] = $Matches[2].Trim() } }
foreach ($k in 'POSTGRES_DB', 'POSTGRES_USER') { if (-not $s[$k]) { throw "Thiếu $k trong .env." } }
$sql = Join-Path $root 'infrastructure\seed\clinic-catalog.seed.sql'
$compose = Join-Path $root 'infrastructure\docker\compose.yaml'
Get-Content -LiteralPath $sql -Raw -Encoding UTF8 |
    docker compose --env-file $envFile -f $compose exec -T $Service psql -U $s['POSTGRES_USER'] -d $s['POSTGRES_DB'] -v ON_ERROR_STOP=1
if ($LASTEXITCODE -ne 0) { throw 'Nạp dữ liệu mẫu thất bại; không có gì được ghi (giao dịch đã hủy).' }
Write-Host 'Đã nạp dữ liệu mẫu danh mục phòng khám.'
