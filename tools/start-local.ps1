# Single local entry point: starts Docker/PostgreSQL and the protected API in their own window
# (or reuses them), ensures the web dev server is running, then opens /backoffice.
# The synthetic demo stays available at / on the same web server.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$web = Join-Path $root 'web'
$apiScript = Join-Path $PSScriptRoot 'start-backoffice-api.ps1'
$webUrl = 'http://127.0.0.1:3000/'
$backofficeUrl = 'http://127.0.0.1:3000/backoffice'
$healthUrl = 'http://127.0.0.1:5081/api/v1/health'

if (-not (Test-Path -LiteralPath (Join-Path $web 'package.json'))) {
    throw "No se encontró la vista local en $web. Conserve este acceso directo junto con el repositorio."
}
$node = Get-Command node.exe -ErrorAction SilentlyContinue
$pnpm = Get-Command pnpm.cmd -ErrorAction SilentlyContinue
$runner = $null
$prefix = @()
$startPrefix = @()
if ($node -and $pnpm) {
    $runner = $pnpm.Source
} else {
    $fnm = Join-Path $HOME 'scoop\shims\fnm.exe'
    $userPnpm = Join-Path $env:APPDATA 'npm\pnpm.cmd'
    if (-not (Test-Path -LiteralPath $fnm) -or -not (Test-Path -LiteralPath $userPnpm)) {
        throw 'No se encontró Node.js/pnpm ni la instalación local de fnm. Compruebe fnm y pnpm.'
    }
    $runner = $fnm
    $prefix = @('exec', '--using=default', '--', $userPnpm)
    $startPrefix = @('exec', '--using=default', '--', "`"$userPnpm`"")
    & $runner @prefix --version | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'fnm no pudo iniciar pnpm con la versión predeterminada de Node.js.' }
}

function Test-ApiHealth {
    try {
        $health = Invoke-WebRequest -UseBasicParsing -Uri $healthUrl -TimeoutSec 2
        return $health.StatusCode -eq 200 -and $health.Content -match '"status"\s*:\s*"healthy"'
    } catch { return $false }
}

$apiReady = Test-ApiHealth
if ($apiReady) {
    Write-Host 'La API privada ya está iniciada; se reutiliza esa instancia.'
} else {
    Write-Host 'Iniciando Docker, la base de datos y la API privada en una ventana separada...'
    Start-Process -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" `
        -ArgumentList @('-NoExit', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$apiScript`"") | Out-Null
}
# The first start may need to launch Docker Desktop and compile the API.
for ($i = 0; -not $apiReady -and $i -lt 400; $i++) {
    Start-Sleep -Milliseconds 500
    $apiReady = Test-ApiHealth
}
if (-not $apiReady) { throw 'La API privada no respondió. Revise la ventana que se abrió para ver el error.' }
Write-Host 'Para ver el usuario y la contraseña de administrador local, ejecute en una consola:'
Write-Host "  powershell -NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $PSScriptRoot 'show-backoffice-login.ps1')`""

$server = $null
$socket = [System.Net.Sockets.TcpClient]::new()
try {
    $attempt = $socket.ConnectAsync('127.0.0.1', 3000)
    if ($attempt.Wait(300) -and $socket.Connected) {
        $listener = Get-NetTCPConnection -LocalAddress '127.0.0.1' -LocalPort 3000 -State Listen -ErrorAction SilentlyContinue
        $expectedPath = (Join-Path $web 'node_modules') + '\'
        $nextServer = '\node_modules\next\dist\server\lib\start-server.js'
        $owned = @($listener | Where-Object {
            $process = Get-CimInstance Win32_Process -Filter "ProcessId=$($_.OwningProcess)"
            $commandLine = if ($process) { [string]$process.CommandLine } else { '' }
            $command = [regex]::Match($commandLine, '^(?:"[^"]*\\node\.exe"|\S*\\node\.exe)\s+(?:"(?<script>[^"]+)"|(?<script>\S+))$', 'IgnoreCase')
            $scriptPath = ''
            if ($command.Success) { try { $scriptPath = [IO.Path]::GetFullPath($command.Groups['script'].Value) } catch { } }
            $process.Name -eq 'node.exe' -and $command.Success -and
            $scriptPath.StartsWith($expectedPath, [StringComparison]::OrdinalIgnoreCase) -and
            $scriptPath.EndsWith($nextServer, [StringComparison]::OrdinalIgnoreCase)
        }).Count -gt 0
        # A dev server idle for a while can take a moment to recompile '/' on this first request.
        $response = $null
        for ($i = 0; $i -lt 5 -and -not ($response -and $response.StatusCode -eq 200 -and $response.Content -match 'DEMOSTRACI[ÓO]N LOCAL'); $i++) {
            try { $response = Invoke-WebRequest -UseBasicParsing -Uri $webUrl -TimeoutSec 5 } catch { $response = $null }
            if (-not ($response -and $response.StatusCode -eq 200 -and $response.Content -match 'DEMOSTRACI[ÓO]N LOCAL')) { Start-Sleep -Milliseconds 800 }
        }
        if ($owned -and $response -and $response.StatusCode -eq 200 -and $response.Content -match 'DEMOSTRACI[ÓO]N LOCAL') {
            Write-Host 'La vista local ya está iniciada; abriendo el backoffice en esa instancia.'
            Start-Process $backofficeUrl
            return
        }
        throw 'El puerto local 3000 está ocupado por otro servicio o no se pudo verificar la vista. No se abrirá ese servicio.'
    }
} catch [System.AggregateException] {
    # Connection refused means the port is available.
} finally {
    $socket.Dispose()
}

if (-not (Test-Path -LiteralPath (Join-Path $web 'node_modules/next/package.json'))) {
    Write-Host 'Instalando dependencias de la vista local...'
    & $runner @prefix --dir $web install --frozen-lockfile --ignore-scripts
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron instalar las dependencias locales.' }
}
$env:LOCAL_PREVIEW = '1'
$env:NODE_ENV = 'development'
Write-Host 'Iniciando la vista local; se abrirá el backoffice privado en http://127.0.0.1:3000/backoffice (demostración en /)'
Write-Host 'Para detener el servidor web, pulse Ctrl+C en esta ventana. La API privada queda en su propia ventana.'
$server = Start-Process -FilePath $runner -ArgumentList ($startPrefix + @('--dir', "`"$web`"", 'dev')) -WorkingDirectory $web -NoNewWindow -PassThru
try {
    $ready = $false
    for ($i = 0; $i -lt 80; $i++) {
        if ($server.HasExited) { throw "El servidor local terminó antes de responder (código $($server.ExitCode))." }
        $response = $null
        try { $response = Invoke-WebRequest -UseBasicParsing -Uri $webUrl -TimeoutSec 2 } catch { }
        if ($response -and $response.StatusCode -eq 200) {
            if ($response.Content -notmatch 'DEMOSTRACI[ÓO]N LOCAL') { throw 'El puerto 3000 respondió, pero no corresponde a esta vista local.' }
            $ready = $true
            break
        }
        Start-Sleep -Milliseconds 500
    }
    if (-not $ready) { throw 'La vista local no respondió en el puerto 3000 dentro del tiempo esperado.' }
    Start-Process $backofficeUrl
    Wait-Process -Id $server.Id
    if ($server.ExitCode -ne 0) { throw "El servidor local terminó con código $($server.ExitCode)." }
} finally {
    if (-not $server.HasExited) { & taskkill.exe /PID $server.Id /T /F 2>$null | Out-Null }
}
