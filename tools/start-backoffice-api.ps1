# Local-only protected API. Never serve customer data through the synthetic notebook.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$state = Join-Path $root '.local'
$settingsFile = Join-Path $state 'backoffice-credentials.json'
$compose = Join-Path $root 'compose.yaml'
$project = 'agro-operations-local'

function New-EncryptedPassword {
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    $plain = [BitConverter]::ToString($bytes).Replace('-', '')
    $secure = ConvertTo-SecureString $plain -AsPlainText -Force
    ConvertFrom-SecureString $secure
}
function Unprotect-Password([string]$ciphertext) {
    $secure = ConvertTo-SecureString $ciphertext
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}
function Invoke-Compose {
    & docker.exe compose -p $project -f $compose @args
    if ($LASTEXITCODE -ne 0) { throw "Local PostgreSQL command failed ($LASTEXITCODE)" }
}

if (-not (Get-Command docker.exe -ErrorAction SilentlyContinue)) { throw 'Docker is required for the private local database.' }
if (-not (Get-Command dotnet.exe -ErrorAction SilentlyContinue)) { throw '.NET SDK is required for the private local API.' }
if (-not (Test-Path -LiteralPath $state)) { New-Item -ItemType Directory -Path $state | Out-Null }
if (-not (Test-Path -LiteralPath $settingsFile)) {
    & docker.exe volume inspect "${project}_postgres_data" --format '{{.Name}}' 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) {
        throw 'Private database exists without local credentials. Restore the original credentials; no reset was attempted.'
    }
    $settings = [ordered]@{
        email = 'admin@localhost.invalid'
        adminPassword = New-EncryptedPassword
        databasePassword = New-EncryptedPassword
    }
    $json = ConvertTo-Json -InputObject $settings -Compress
    $stream = [IO.File]::Open($settingsFile, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($json)
        $stream.Write($bytes, 0, $bytes.Length)
    } finally { $stream.Dispose() }
    Write-Host 'Private local credentials created for this Windows user. See tools/show-backoffice-login.ps1.'
}
$settings = Get-Content -LiteralPath $settingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
$adminPassword = Unprotect-Password $settings.adminPassword
$databasePassword = Unprotect-Password $settings.databasePassword
if ($adminPassword.Length -lt 24 -or $databasePassword.Length -lt 24 -or $settings.email -ne 'admin@localhost.invalid') {
    throw 'Invalid private local credentials; no data has been changed.'
}
$env:POSTGRES_PASSWORD = $databasePassword
Invoke-Compose up -d postgres
$container = (& docker.exe compose -p $project -f $compose ps -q postgres).Trim()
if ($LASTEXITCODE -ne 0 -or -not $container) { throw 'Private PostgreSQL container was not created.' }
$ready = $false
for ($attempt = 0; $attempt -lt 80; $attempt++) {
    $health = & docker.exe inspect -f '{{.State.Health.Status}}' $container 2>$null
    if ($LASTEXITCODE -eq 0 -and $health -eq 'healthy') { $ready = $true; break }
    Start-Sleep -Milliseconds 500
}
if (-not $ready) { throw 'Private PostgreSQL did not become healthy.' }

function Table-Count([string]$names) {
    $query = "SELECT count(*) FROM pg_class WHERE oid IN (SELECT to_regclass(name) FROM unnest(string_to_array('$names', ',')) name)"
    $result = & docker.exe compose -p $project -f $compose exec -T postgres psql -U agro_dev -d agro_operations -tAc $query
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect local PostgreSQL schema.' }
    return [int]$result.Trim()
}
function Apply-Migration([string]$file) {
    $path = Join-Path $root "src\AgroOperations.Api\Persistence\Migrations\$file"
    Get-Content -LiteralPath $path -Raw -Encoding UTF8 | & docker.exe compose -p $project -f $compose exec -T postgres psql -v ON_ERROR_STOP=1 -1 -U agro_dev -d agro_operations | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Local schema migration failed: $file" }
}
$masters = Table-Count 'public.clients,public.fields,public.plots'
if ($masters -eq 0) { Apply-Migration '001_operations.sql' }
elseif ($masters -ne 3) { throw 'Incomplete local master schema: inspect it before continuing; no automatic reset.' }
if ((Table-Count 'public.crops') -eq 0) { Apply-Migration '003_crops.sql' }
$indexSql = "SELECT count(*) FROM pg_indexes WHERE schemaname='public' AND tablename='crops' AND indexname='crops_name_ci_idx' AND indexdef LIKE 'CREATE UNIQUE INDEX % (lower(name))'"
$indexCount = & docker.exe compose -p $project -f $compose exec -T postgres psql -U agro_dev -d agro_operations -tAc $indexSql
if ($LASTEXITCODE -ne 0 -or [int]$indexCount.Trim() -ne 1) {
    throw 'Incomplete local crop schema: inspect it before continuing; no automatic reset.'
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:AGRO_LOCAL_ADMIN_EMAIL = $settings.email
$env:AGRO_LOCAL_ADMIN_PASSWORD = $adminPassword
$env:AGRO_LOCAL_DATABASE = "Host=127.0.0.1;Port=5433;Database=agro_operations;Username=agro_dev;Password=$databasePassword"
Write-Host 'Protected local API at http://127.0.0.1:5081/ (no real-data UI enabled yet).'
Write-Host 'Stop this window with Ctrl+C. PostgreSQL data remains in its private Docker volume.'
Push-Location $root
try {
    & dotnet.exe run --project (Join-Path $root 'src\AgroOperations.Api\AgroOperations.Api.csproj') --no-launch-profile -- --urls http://127.0.0.1:5081
    if ($LASTEXITCODE -ne 0) { throw "Local API stopped with code $LASTEXITCODE" }
} finally { Pop-Location }
