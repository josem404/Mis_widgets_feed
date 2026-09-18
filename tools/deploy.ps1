<#
.SYNOPSIS
    Compila y registra Mis Widgets Feed sin desinstalar el paquete.

.DESCRIPTION
    Alterna dos layouts de despliegue para no sobrescribir resources.pri de la carpeta
    actualmente registrada. La revisión MSIX solo se incrementa en la copia desplegada;
    el manifiesto versionado permanece estable.
#>
[CmdletBinding()]
param(
    [switch] $SkipBuild,
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Debug',
    [ValidateSet('x64', 'arm64')] [string] $Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$PackageName = 'MisWidgets.Feed.Provider'
$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root 'src\MisWidgets.Feed.Provider\MisWidgets.Feed.Provider.csproj'
$SourceManifest = Join-Path $Root 'src\MisWidgets.Feed.Provider\Package.appxmanifest'
$Rid = if ($Platform -eq 'x64') { 'win-x64' } else { 'win-arm64' }
$BinLayout = Join-Path $Root "src\MisWidgets.Feed.Provider\bin\$Platform\$Configuration\net10.0-windows10.0.26100.0\$Rid"
$DeployRoot = Join-Path $Root 'deploy'

function Write-Step([string] $Message) { Write-Host "`n==> $Message" -ForegroundColor Cyan }
function Write-Ok([string] $Message) { Write-Host "    $Message" -ForegroundColor Green }

function Assert-ContentUriConfigured {
    [xml] $manifest = Get-Content -LiteralPath $SourceManifest -Raw
    $namespace = [System.Xml.XmlNamespaceManager]::new($manifest.NameTable)
    $namespace.AddNamespace('f', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
    $namespace.AddNamespace('uap3', 'http://schemas.microsoft.com/appx/manifest/uap/windows10/3')
    $definition = $manifest.SelectSingleNode(
        '//uap3:AppExtension/uap3:Properties/f:FeedProvider/f:Definitions/f:Definition',
        $namespace)
    if (-not $definition) { throw 'El manifiesto no contiene la Definition del feed.' }

    $contentUri = [string] $definition.ContentUri
    $expected = 'https://josem404.github.io/Mis_widgets_feed/'
    if ($contentUri -cne $expected) {
        throw "ContentUri debe ser exactamente '$expected'; se encontró '$contentUri'."
    }
}

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere)) {
        throw 'No se encontró vswhere. Instala Visual Studio con la workload de desarrollo de apps de Windows.'
    }

    $found = & $vswhere -latest -prerelease -products '*' `
        -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
        Select-Object -First 1
    if (-not $found) {
        $installation = & $vswhere -all -prerelease -products '*' -property installationPath |
            Select-Object -First 1
        if ($installation) {
            $candidate = Join-Path $installation 'MSBuild\Current\Bin\MSBuild.exe'
            if (Test-Path -LiteralPath $candidate) { $found = $candidate }
        }
    }

    if (-not $found) { throw 'vswhere no encontró MSBuild.exe.' }
    return $found
}

Assert-ContentUriConfigured

if (-not $SkipBuild) {
    Write-Step 'Compilando el layout MSIX'
    $msbuild = Find-MSBuild
    & $msbuild $Project /restore /nologo /verbosity:minimal `
        "/p:Configuration=$Configuration" "/p:Platform=$Platform"
    if ($LASTEXITCODE -ne 0) { throw "MSBuild falló con código $LASTEXITCODE." }
    Write-Ok 'Compilación completada.'
}

$builtManifest = Join-Path $BinLayout 'AppxManifest.xml'
if (-not (Test-Path -LiteralPath $builtManifest)) {
    throw "No existe el layout esperado en '$BinLayout'."
}

Write-Step 'Seleccionando una ranura de despliegue no registrada'
$installed = Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue
$current = if ($installed) { [string] $installed.InstallLocation } else { $null }
$slotA = Join-Path $DeployRoot 'a'
$slotB = Join-Path $DeployRoot 'b'
$target = if ($current -and $current.TrimEnd('\') -ieq $slotA.TrimEnd('\')) { $slotB } else { $slotA }

if ($current -and $current.TrimEnd('\') -ieq $target.TrimEnd('\')) {
    throw 'La ranura elegida coincide con el paquete registrado.'
}
Write-Ok "Destino: $target"

Write-Step 'Copiando el layout'
New-Item -ItemType Directory -Force -Path $target | Out-Null
robocopy $BinLayout $target /MIR /R:2 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy falló con código $LASTEXITCODE." }
$global:LASTEXITCODE = 0
Write-Ok 'Layout copiado.'

Write-Step 'Incrementando la revisión de la copia desplegada'
$manifestPath = Join-Path $target 'AppxManifest.xml'
[xml] $manifest = Get-Content -LiteralPath $manifestPath -Raw
$identity = $manifest.Package.Identity
$sourceVersion = [Version] $identity.Version
$installedRevision = if ($installed) { ([Version] $installed.Version).Revision } else { -1 }
$revision = [Math]::Max($sourceVersion.Revision, $installedRevision) + 1
$newVersion = '{0}.{1}.{2}.{3}' -f `
    $sourceVersion.Major, $sourceVersion.Minor, $sourceVersion.Build, $revision
$identity.Version = $newVersion
$manifest.Save($manifestPath)
Write-Ok "$sourceVersion -> $newVersion"

Write-Step 'Registrando la actualización MSIX'
Add-AppxPackage -Register $manifestPath -ForceApplicationShutdown
$registered = Get-AppxPackage -Name $PackageName -ErrorAction Stop
if ($registered.InstallLocation.TrimEnd('\') -ine $target.TrimEnd('\')) {
    throw "Windows mantuvo otra ubicación registrada: '$($registered.InstallLocation)'."
}
Write-Ok "$($registered.PackageFullName) [$($registered.Status)]"
Write-Ok "InstallLocation: $($registered.InstallLocation)"
