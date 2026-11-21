# Starts the synthetic preview only; it never starts a business API or opens client data.
$ErrorActionPreference = 'Stop'
$web = Join-Path (Split-Path $PSScriptRoot -Parent) 'web'
$url = 'http://127.0.0.1:3000/'

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
    # Explorer can retain an older PATH than interactive shells. Resolve the user's fnm default.
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

# Never open a different service if the expected port is already occupied.
$socket = [System.Net.Sockets.TcpClient]::new()
try {
    $attempt = $socket.ConnectAsync('127.0.0.1', 3000)
    if ($attempt.Wait(300) -and $socket.Connected) {
        throw 'El puerto local 3000 está ocupado. Detenga ese servicio antes de abrir esta demostración.'
    }
} catch [System.AggregateException] {
    # Connection refused means the port is available.
} finally {
    $socket.Dispose()
}

if (-not (Test-Path -LiteralPath (Join-Path $web 'node_modules/next/package.json'))) {
    Write-Host 'Instalando dependencias de la demostración local...'
    & $runner @prefix --dir $web install --frozen-lockfile --ignore-scripts
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron instalar las dependencias locales.' }
}

$env:LOCAL_PREVIEW = '1'
$env:NODE_ENV = 'development'
Write-Host 'Iniciando vista con datos inventados en http://127.0.0.1:3000/'
Write-Host 'Para detener el servidor, pulse Ctrl+C en esta ventana.'
$server = Start-Process -FilePath $runner -ArgumentList ($startPrefix + @('--dir', "`"$web`"", 'dev')) -WorkingDirectory $web -NoNewWindow -PassThru
try {
    $ready = $false
    for ($i = 0; $i -lt 80; $i++) {
        if ($server.HasExited) { throw "El servidor local terminó antes de responder (código $($server.ExitCode))." }
        $response = $null
        try { $response = Invoke-WebRequest -UseBasicParsing -Uri $url -TimeoutSec 2 } catch { }
        if ($response -and $response.StatusCode -eq 200) {
            if ($response.Content -notmatch 'DEMOSTRACI[ÓO]N LOCAL') {
                throw 'El puerto 3000 respondió, pero no corresponde a esta demostración local.'
            }
            $ready = $true
            break
        }
        Start-Sleep -Milliseconds 500
    }
    if (-not $ready) { throw 'La demostración local no respondió en el puerto 3000 dentro del tiempo esperado.' }
    Start-Process $url
    Wait-Process -Id $server.Id
    if ($server.ExitCode -ne 0) { throw "El servidor local terminó con código $($server.ExitCode)." }
} finally {
    if (-not $server.HasExited) { & taskkill.exe /PID $server.Id /T /F 2>$null | Out-Null }
}
