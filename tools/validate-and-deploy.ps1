<#
.SYNOPSIS
    Sincroniza WSL con Windows, valida, despliega y ejecuta el self-test empaquetado.
#>
[CmdletBinding()]
param(
    [switch] $SkipSync,
    [switch] $SkipTests,
    [switch] $SkipPagesCheck,
    [switch] $SkipSelfTest,
    [switch] $RestartWidgetHost,
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Debug',
    [ValidateSet('x64', 'arm64')] [string] $Platform = 'x64',
    [string] $WslRepository = '/home/ubunewsys/Proyectos_feed_WSL'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$WindowsRoot = Split-Path -Parent $PSScriptRoot
$PackageName = 'MisWidgets.Feed.Provider'
$FeedUrl = 'https://josem404.github.io/Mis_widgets_feed/'

function Write-Step([string] $Message) { Write-Host "`n==> $Message" -ForegroundColor Cyan }
function Write-Ok([string] $Message) { Write-Host "    $Message" -ForegroundColor Green }

function Assert-CleanWorktree([string] $Repository, [string] $Label) {
    $changes = @(git -C $Repository status --porcelain)
    if ($LASTEXITCODE -ne 0) { throw "No se pudo consultar $Label en '$Repository'." }
    if ($changes.Count -gt 0) {
        throw "$Label tiene cambios sin confirmar:`n$($changes -join [Environment]::NewLine)"
    }
}

function ConvertTo-BashSingleQuoted([string] $Value) {
    $escapedSingleQuote = "'`"'`"'"
    return "'" + $Value.Replace("'", $escapedSingleQuote) + "'"
}

$wslRepositoryQuoted = ConvertTo-BashSingleQuoted $WslRepository
function Invoke-Wsl([string] $Script) {
    $command = "set -euo pipefail; cd -- $wslRepositoryQuoted; git rev-parse --is-inside-work-tree >/dev/null; $Script"
    & wsl.exe -- bash -lc $command
    if ($LASTEXITCODE -ne 0) { throw "El comando WSL falló con código $LASTEXITCODE." }
}

Write-Step 'Comprobando requisitos nativos'
$minimumBuild = [Version] '10.0.22631.2787'
$windowsVersion = [Environment]::OSVersion.Version
if ($windowsVersion -lt $minimumBuild) {
    throw "Windows $minimumBuild o posterior es obligatorio; se detectó $windowsVersion."
}

$developerMode = Get-ItemPropertyValue `
    -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' `
    -Name 'AllowDevelopmentWithoutDevLicense' `
    -ErrorAction SilentlyContinue
if ($developerMode -ne 1) {
    throw 'Activa Modo para desarrolladores en Configuración antes de registrar el layout MSIX.'
}

$dotnetSdks = @(dotnet --list-sdks)
if (-not ($dotnetSdks | Where-Object { $_ -match '^10\.' })) {
    throw "No se encontró un SDK .NET 10. SDKs detectados:`n$($dotnetSdks -join [Environment]::NewLine)"
}
Write-Ok "Windows $windowsVersion, Modo para desarrolladores y .NET 10 disponibles."

if (-not $SkipSync) {
    Write-Step 'Comprobando ambos árboles antes de sincronizar'
    Assert-CleanWorktree $WindowsRoot 'La copia Windows'
    Invoke-Wsl 'test -z "$(git status --porcelain)" || { git status --short; exit 2; }'
    Write-Ok 'Ambas copias están limpias.'

    Write-Step 'Sincronizando WSL hacia Windows'
    Invoke-Wsl 'git push origin master'
    & git -C $WindowsRoot reset --hard HEAD
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo actualizar la copia Windows.' }

    $windowsHead = (& git -C $WindowsRoot rev-parse HEAD).Trim()
    $wslHead = (Invoke-Wsl 'git rev-parse HEAD').Trim()
    if ($windowsHead -ne $wslHead) {
        throw "Los commits no coinciden: Windows=$windowsHead; WSL=$wslHead."
    }
    Write-Ok "Sincronizadas en $windowsHead"
}
else {
    Assert-CleanWorktree $WindowsRoot 'La copia Windows'
}

if (-not $SkipPagesCheck) {
    Write-Step 'Comprobando GitHub Pages'
    & (Join-Path $PSScriptRoot 'verify-pages.ps1') -Uri $FeedUrl
    if ($LASTEXITCODE -ne 0) { throw 'La verificación de GitHub Pages falló.' }
}

if (-not $SkipTests) {
    Write-Step 'Ejecutando pruebas .NET'
    & dotnet test (Join-Path $WindowsRoot 'tests\MisWidgets.Feed.Tests') `
        --configuration $Configuration --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw "dotnet test falló con código $LASTEXITCODE." }
    Write-Ok 'Pruebas .NET correctas.'
}

if ($RestartWidgetHost) {
    Write-Step 'Reiniciando el host de Widgets'
    Stop-Process -Name WidgetBoard, WidgetService -Force -ErrorAction SilentlyContinue
    Write-Ok 'Procesos del host detenidos; Windows los reactivará bajo demanda.'
}

Write-Step 'Desplegando el provider'
& (Join-Path $PSScriptRoot 'deploy.ps1') -Configuration $Configuration -Platform $Platform
if ($LASTEXITCODE -ne 0) { throw 'deploy.ps1 falló.' }

if (-not $SkipSelfTest) {
    Write-Step 'Ejecutando --selftest bajo la identidad del paquete'
    $package = Get-AppxPackage -Name $PackageName -ErrorAction Stop
    $executable = Join-Path $package.InstallLocation 'MisWidgets.Feed.Provider.exe'
    $reportPath = Join-Path $env:LOCALAPPDATA "Packages\$($package.PackageFamilyName)\LocalState\selftest.txt"
    Remove-Item -LiteralPath $reportPath -Force -ErrorAction SilentlyContinue

    Invoke-CommandInDesktopPackage -PackageFamilyName $package.PackageFamilyName -AppId App `
        -Command $executable -Args '--selftest'

    $deadline = (Get-Date).AddSeconds(60)
    while (-not (Test-Path -LiteralPath $reportPath) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 250
    }
    if (-not (Test-Path -LiteralPath $reportPath)) {
        throw "El self-test no creó '$reportPath' en 60 segundos."
    }

    $report = Get-Content -LiteralPath $reportPath -Raw
    if ($report -notmatch 'Sin problemas detectados\.') {
        throw "El self-test informó problemas:`n$report"
    }
    Write-Ok "Self-test correcto: $reportPath"
}

Write-Host "`nValidación y despliegue completados." -ForegroundColor Green
