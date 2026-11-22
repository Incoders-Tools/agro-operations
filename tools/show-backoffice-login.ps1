# Run interactively as the Windows user who created the private local credentials.
$ErrorActionPreference = 'Stop'
if (-not [Environment]::UserInteractive -or [Console]::IsOutputRedirected) {
    throw 'The local password can only be shown in an interactive console; do not redirect or capture its output.'
}
$root = Split-Path $PSScriptRoot -Parent
$file = Join-Path $root '.local\backoffice-credentials.json'
if (-not (Test-Path -LiteralPath $file)) { throw 'No local administrator exists. Start tools/start-backoffice-api.ps1 first.' }
$settings = Get-Content -LiteralPath $file -Raw -Encoding UTF8 | ConvertFrom-Json
$secure = ConvertTo-SecureString $settings.adminPassword
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    # Console.WriteLine bypasses PowerShell streams (including redirected information stream 6).
    [Console]::WriteLine("Local login: $($settings.email)")
    [Console]::WriteLine("Local password: $([Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr))")
} finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
[Console]::WriteLine('Do not copy this password into chat, logs or the public repository.')
