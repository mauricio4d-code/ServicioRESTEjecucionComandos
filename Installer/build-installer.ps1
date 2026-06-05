<#
.SYNOPSIS
    Build script for ServicioRESTEjecucionComandos WiX v4 MSI + Burn Bootstrapper.

.DESCRIPTION
    Publishes the ASP.NET Core application, then builds the WiX v4 MSI package
    and the Burn bootstrapper executable in sequence.

.PARAMETER Configuration
    Build configuration (Release/Debug). Default: Release.

.PARAMETER Architecture
    Target architecture (x64/arm64). Default: x64.

.PARAMETER SkipPublish
    Skip the publish step (useful for iterative installer development).

.PARAMETER SkipInstaller
    Skip the installer build (useful for quick publish testing).

.EXAMPLE
    .\build-installer.ps1
    .\build-installer.ps1 -Configuration Debug
    .\build-installer.ps1 -SkipPublish
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Architecture = "x64",
    [switch]$SkipPublish,
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projectDir = Split-Path -Parent $scriptDir
$installerDir = $scriptDir
$publishDir = Join-Path $installerDir "publish"
$outputDir = Join-Path $installerDir "bin\$Configuration"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  ServicioRESTEjecucionComandos Installer Build" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration: $Configuration"
Write-Host "Architecture:  $Architecture"
Write-Host "Project Dir:   $projectDir"
Write-Host "Installer Dir: $installerDir"
Write-Host ""

# -----------------------------------------------------------------------
# Step 1: Restore NuGet packages
# -----------------------------------------------------------------------
Write-Host "[1/4] Restoring NuGet packages..." -ForegroundColor Yellow
& dotnet restore "$projectDir\ServicioRESTEjecucionComandos.sln"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE"
}
Write-Host "  Packages restored successfully." -ForegroundColor Green
Write-Host ""

# -----------------------------------------------------------------------
# Step 2: Publish the application
# -----------------------------------------------------------------------
if (-not $SkipPublish) {
    Write-Host "[2/4] Publishing application..." -ForegroundColor Yellow

    # Clean publish directory
    if (Test-Path $publishDir) {
        Remove-Item -Path $publishDir -Recurse -Force
        Write-Host "  Cleaned publish directory."
    }
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

    # Publish with framework-dependent (requires .NET 8 runtime on target)
    $publishArgs = @(
        "publish",
        "$projectDir\ServicioRESTEjecucionComandos.csproj",
        "-c", $Configuration,
        "-r", "win-$Architecture",
        "-p:PublishSingleFile=false",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-o", $publishDir,
        "--no-restore"
    )

    & dotnet $publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    # Copy Monitor-Health.ps1 to publish directory
    $monitorScript = Join-Path $projectDir "Monitor-Health.ps1"
    if (Test-Path $monitorScript) {
        Copy-Item $monitorScript -Destination $publishDir -Force
        Write-Host "  Copied Monitor-Health.ps1 to publish directory."
    }
    else {
        Write-Warning "Monitor-Health.ps1 not found at $monitorScript"
    }

    Write-Host "  Application published to $publishDir" -ForegroundColor Green
    Write-Host ""
}
else {
    Write-Host "[2/4] Skipping publish step." -ForegroundColor DarkGray
    Write-Host ""
}

# -----------------------------------------------------------------------
# Step 3: Build the MSI package
# -----------------------------------------------------------------------
if (-not $SkipInstaller) {
    Write-Host "[3/4] Building MSI package..." -ForegroundColor Yellow

    $msbuildArgs = @(
        "$installerDir\Installer.wixproj",
        "-t:Build",
        "-p:Configuration=$Configuration",
        "-p:PublishOutputPath=$publishDir\",
        "-p:AppVersion=1.0.0",
        "-p:Platform=$Architecture"
    )

    & dotnet msbuild $msbuildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild failed with exit code $LASTEXITCODE"
    }

    $msiPath = Join-Path $outputDir "ServicioRESTEjecucionComandos.msi"
    if (Test-Path $msiPath) {
        Write-Host "  MSI built: $msiPath" -ForegroundColor Green
    }
    else {
        Write-Warning "MSI not found at expected path: $msiPath"
    }
    Write-Host ""

    # -----------------------------------------------------------------------
    # Step 4: Build the Burn bootstrapper
    # -----------------------------------------------------------------------
    Write-Host "[4/4] Building Burn bootstrapper..." -ForegroundColor Yellow

    $bundleArgs = @(
        "$installerDir\Installer.wixproj",
        "-t:Bundle",
        "-p:Configuration=$Configuration",
        "-p:PublishOutputPath=$publishDir\",
        "-p:AppVersion=1.0.0",
        "-p:Platform=$Architecture"
    )

    & dotnet msbuild $bundleArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Bundle build failed. This may be expected if Bundle.wxs references local .NET runtime files."
        Write-Host "  You can manually build the bundle after placing .NET 8 runtime installers."
    }
    else {
        $exePath = Join-Path $outputDir "ServicioRESTEjecucionComandos-Setup.exe"
        if (Test-Path $exePath) {
            Write-Host "  Bootstrapper built: $exePath" -ForegroundColor Green
        }
        else {
            Write-Host "  Bundle built successfully (check $outputDir for output)." -ForegroundColor Green
        }
    }
    Write-Host ""
}
else {
    Write-Host "[3/4] Skipping installer build step." -ForegroundColor DarkGray
    Write-Host ""
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Build Complete!" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output directory: $outputDir"
if (Test-Path $outputDir) {
    Get-ChildItem -Path $outputDir -File | Format-Table Name, Length, LastWriteTime
}
