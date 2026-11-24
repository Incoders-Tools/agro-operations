# Imports contractors and inputs from the owner's workbook into the private local database.
# Start the database first with tools/start-local.ps1. The screen shows counts and row numbers only.
param(
    [Parameter(Mandatory = $true)][string]$Workbook,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$settingsFile = Join-Path (Join-Path $root '.local') 'backoffice-credentials.json'
$project = Join-Path $root 'tools\AgroOperations.WorkbookImport\AgroOperations.WorkbookImport.csproj'

function Unprotect-Password([string]$ciphertext) {
    $secure = ConvertTo-SecureString $ciphertext
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

if (-not (Get-Command dotnet.exe -ErrorAction SilentlyContinue)) { throw '.NET SDK is required for the workbook import.' }
if (-not (Test-Path -LiteralPath $Workbook -PathType Leaf)) { throw 'Workbook file not found.' }
if (-not (Test-Path -LiteralPath $settingsFile)) { throw 'Private local credentials not found; run tools/start-local.ps1 first.' }
$settings = Get-Content -LiteralPath $settingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
$databasePassword = Unprotect-Password $settings.databasePassword
if ($databasePassword.Length -lt 24) { throw 'Invalid private local credentials; no data has been changed.' }

$arguments = @('run', '--project', $project, '--', '--workbook', (Resolve-Path -LiteralPath $Workbook).ProviderPath)
if ($DryRun) { $arguments += '--dry-run' }
$env:AGRO_LOCAL_DATABASE = "Host=127.0.0.1;Port=5433;Database=agro_operations;Username=agro_dev;Password=$databasePassword"
# A running local API locks bin\Debug; build the import into its own output folder instead.
$env:OutputPath = 'bin\import\'
Push-Location $root
try {
    # Native stderr is not redirected here: under Stop, Windows PowerShell would turn redirected stderr into a terminating error.
    & dotnet.exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "Workbook import failed with code $LASTEXITCODE" }
} finally {
    Pop-Location
    Remove-Item Env:AGRO_LOCAL_DATABASE -ErrorAction SilentlyContinue
    Remove-Item Env:OutputPath -ErrorAction SilentlyContinue
}
