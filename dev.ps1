# Sobe backend (.NET) e frontend (Vite) em janelas separadas.
# Uso:  .\dev.ps1
# Stop: .\dev.ps1 -Stop      ou      Ctrl+C em cada janela
# Flags:
#   -Stop         encerra processos nas portas 5062 e 5173 e sai
#   -NoBrowser    nao abre o browser automaticamente
#   -Bare         nao carrega o $PROFILE nas janelas filhas (evita conflito de oh-my-posh, posh-git, etc.)

[CmdletBinding()]
param(
    [switch]$Stop,
    [switch]$NoBrowser,
    [switch]$Bare
)

$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
$pastaApi = Join-Path $raiz 'src\PcaImporter.Api'
$pastaWeb = Join-Path $raiz 'src\PcaImporter.Web'

function Stop-PortaProcessos {
    param([int]$Porta, [string]$Rotulo)
    $procs = Get-NetTCPConnection -LocalPort $Porta -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique
    if (-not $procs) {
        Write-Host "  $Rotulo (porta $Porta): nada rodando." -ForegroundColor DarkGray
        return
    }
    foreach ($p in $procs) {
        try {
            Stop-Process -Id $p -Force -ErrorAction Stop
            Write-Host "  $Rotulo (porta $Porta): processo $p encerrado." -ForegroundColor Yellow
        } catch {
            Write-Host "  $Rotulo (porta $Porta): falha ao encerrar $p ($_)." -ForegroundColor Red
        }
    }
}

function Start-JanelaPwsh {
    param(
        [string]$Titulo,
        [string]$Pasta,
        [string]$ComandoFinal,
        [switch]$SemPerfil
    )
    # Se SemPerfil: nao carrega $PROFILE (evita race do oh-my-posh entre janelas).
    # Se nao: gera arquivo de inicializacao temporario unico por janela, evitando o lock.
    $headerTitulo = "`$Host.UI.RawUI.WindowTitle = '$Titulo'"
    $cdComando = "Set-Location '$Pasta'"
    $args = @('-NoExit')
    if ($SemPerfil) {
        $args += '-NoProfile'
        $args += '-Command'
        $args += "$headerTitulo; $cdComando; $ComandoFinal"
    } else {
        # Cria init temporaria unica para esta janela. Isola dela cache compartilhado de prompt.
        $tmp = [IO.Path]::Combine([IO.Path]::GetTempPath(), "pcaimporter-dev-init-$([guid]::NewGuid()).ps1")
        @"
`$Host.UI.RawUI.WindowTitle = '$Titulo'
Set-Location '$Pasta'
Write-Host ''
Write-Host '----- $Titulo -----' -ForegroundColor Cyan
$ComandoFinal
"@ | Set-Content -Path $tmp -Encoding UTF8
        $args += '-File'
        $args += $tmp
    }
    Start-Process powershell -ArgumentList $args | Out-Null
}

if ($Stop) {
    Write-Host '> Encerrando dev (portas 5062 e 5173)...' -ForegroundColor Cyan
    Stop-PortaProcessos -Porta 5062 -Rotulo 'API'
    Stop-PortaProcessos -Porta 5173 -Rotulo 'Web'
    Write-Host '> Pronto.' -ForegroundColor Cyan
    return
}

# Conferencias rapidas
if (-not (Test-Path (Join-Path $pastaApi 'PcaImporter.Api.csproj'))) {
    throw "Nao encontrei o projeto Api em $pastaApi"
}
if (-not (Test-Path (Join-Path $pastaWeb 'package.json'))) {
    throw "Nao encontrei o front em $pastaWeb"
}
if (-not (Test-Path (Join-Path $pastaWeb 'node_modules'))) {
    Write-Host '> node_modules ausente, rodando npm install...' -ForegroundColor Yellow
    Push-Location $pastaWeb
    try { npm install } finally { Pop-Location }
}

Write-Host '> Liberando portas 5062 e 5173 (se estiverem em uso)...' -ForegroundColor Cyan
Stop-PortaProcessos -Porta 5062 -Rotulo 'API'
Stop-PortaProcessos -Porta 5173 -Rotulo 'Web'

Write-Host '> Subindo backend...' -ForegroundColor Green
Start-JanelaPwsh -Titulo 'PcaImporter Api' -Pasta $pastaApi -ComandoFinal 'dotnet run' -SemPerfil:$Bare

# Da tempo da primeira janela carregar o profile antes da segunda, evitando race no oh-my-posh.
Start-Sleep -Seconds 2

Write-Host '> Subindo frontend...' -ForegroundColor Green
Start-JanelaPwsh -Titulo 'PcaImporter Web' -Pasta $pastaWeb -ComandoFinal 'npm run dev' -SemPerfil:$Bare

if (-not $NoBrowser) {
    Write-Host '> Aguardando o backend ficar pronto antes de abrir o browser...' -ForegroundColor DarkGray
    $deadline = (Get-Date).AddSeconds(40)
    $pronto = $false
    while ((Get-Date) -lt $deadline) {
        try {
            $resp = Invoke-WebRequest -UseBasicParsing -Uri 'http://localhost:5062/api/token/status' -TimeoutSec 2 -ErrorAction Stop
            if ($resp.StatusCode -ge 200 -and $resp.StatusCode -lt 500) { $pronto = $true; break }
        } catch {
            Start-Sleep -Milliseconds 800
        }
    }
    if (-not $pronto) {
        Write-Host '  (backend ainda nao respondeu, abrindo browser mesmo assim)' -ForegroundColor DarkYellow
    }
    Start-Process 'http://localhost:5173'
}

Write-Host ''
Write-Host '> Tudo no ar. Use .\dev.ps1 -Stop para encerrar tudo de uma vez.' -ForegroundColor Cyan
