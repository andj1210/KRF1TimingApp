# createdist.ps1 - Build KRF1 Timing App Release and create distribution zip

param(
    [string]$PlatformToolset = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = Resolve-Path "$scriptDir\.."

# --- Build Release ---
Write-Host "Building Release configuration..."

# Locate MSBuild via vswhere
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    Write-Error "vswhere not found. Is Visual Studio installed?"
    exit 1
}

$vsInstallPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
if (-not $vsInstallPath) {
    Write-Error "Could not find a Visual Studio installation with MSBuild."
    exit 1
}

$msbuild = Join-Path $vsInstallPath "MSBuild\Current\Bin\MSBuild.exe"
if (-not (Test-Path $msbuild)) {
    Write-Error "MSBuild.exe not found at: $msbuild"
    exit 1
}

$solutionFile = Join-Path $repoRoot "Krf1Timing.sln"
Write-Host "Using MSBuild: $msbuild"
Write-Host "Building: $solutionFile"

$msbuildArgs = @($solutionFile, "/p:Configuration=Release", '/p:Platform=Any CPU', "/m", "/verbosity:minimal")
if ($PlatformToolset) {
    Write-Host "Overriding PlatformToolset: $PlatformToolset"
    $msbuildArgs += "/p:PlatformToolset=$PlatformToolset"
}

& $msbuild @msbuildArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
    exit 1
}

Write-Host "Build succeeded."

# --- Create Distribution Zip ---
Write-Host "Creating distribution zip..."

Push-Location $scriptDir
try {
    # Read version from VERSION file at repo root (single source of truth, also used by the app)
    $versionFile = Join-Path $repoRoot "VERSION"
    if (-not (Test-Path $versionFile)) {
        Write-Error "VERSION file not found at: $versionFile"
        exit 1
    }
    $version = (Get-Content $versionFile -Raw).Trim()
    Write-Host "Version: $version"

    # Generate filename: <date>_krf1_timing_v<version>.zip
    $dateStr = Get-Date -Format "yyyy-MM-dd"
    $baseName = "${dateStr}_krf1_timing_v${version}"
    $fnam = "$baseName.zip"
    $nr = 0

    while (Test-Path $fnam) {
        $nr++
        $fnam = "${baseName}-${nr}.zip"
    }

    Write-Host "Output: $fnam"

    # Create temporary folder structure
    $tempFolder = "zipme_temp"
    if (Test-Path $tempFolder) {
        Remove-Item $tempFolder -Recurse -Force
    }
    New-Item -ItemType Directory -Path "$tempFolder\krf1timing" | Out-Null

    # Copy build artifacts
    $buildBin = Join-Path $repoRoot "_build\bin"
    $resDir = Join-Path $repoRoot "res"
    Copy-Item "$buildBin\Krf1Timing.exe"          "$tempFolder\krf1timing\"
    Copy-Item "$buildBin\adjsw.F1Udp.26.dll"      "$tempFolder\krf1timing\"
    Copy-Item "$buildBin\Newtonsoft.Json.dll"     "$tempFolder\krf1timing\"
    Copy-Item "$buildBin\Razorvine.Pickle.dll"    "$tempFolder\krf1timing\"
    Copy-Item "$buildBin\System.Buffers.dll"      "$tempFolder\krf1timing\"
    Copy-Item "$buildBin\System.Memory.dll"       "$tempFolder\krf1timing\"
    Copy-Item "$buildBin\System.Numerics.Vectors.dll" "$tempFolder\krf1timing\"
    Copy-Item "$buildBin\System.Runtime.CompilerServices.Unsafe.dll" "$tempFolder\krf1timing\"
    Copy-Item "$buildBin\kr1timing-update.ba_"    "$tempFolder\krf1timing\"

    # Copy documentation / config files from repo root
    Copy-Item "$repoRoot\changelog.txt"           "$tempFolder\krf1timing\"
    Copy-Item "$repoRoot\LICENSE.md"              "$tempFolder\krf1timing\"
    Copy-Item "$repoRoot\LICENSE.*.*"             "$tempFolder\krf1timing\" -ErrorAction SilentlyContinue
    Copy-Item "$repoRoot\namemappings.json.example" "$tempFolder\krf1timing\"
    Copy-Item "$repoRoot\README.md"               "$tempFolder\krf1timing\"

    # Copy resources
    Copy-Item "$resDir\*"                         "$tempFolder\krf1timing\" -Recurse -ErrorAction SilentlyContinue

    # Create zip using Compress-Archive
    Compress-Archive -Path "$tempFolder\*" -DestinationPath $fnam -Force

    # Clean up
    Remove-Item $tempFolder -Recurse -Force

    Write-Host "DONE: $fnam"
}
finally {
    Pop-Location
}
