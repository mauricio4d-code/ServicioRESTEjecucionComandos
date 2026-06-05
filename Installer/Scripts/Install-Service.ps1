<#
.SYNOPSIS
    Installs ServicioRESTEjecucionComandos as a Windows Service and configures recovery actions.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InstallFolder,

    [string]$ServiceName = "ServicioRESTEjecucionComandos",

    [string]$DisplayName = "Servicio REST Ejecución Comandos",

    [string]$Description = "ASP.NET Core Web API service for ETL command execution"
)

$ErrorActionPreference = "Stop"

function Write-Log {
    param([string]$Message)
    Write-Host "Install-Service: $Message"
}

Write-Log "Installing Windows Service: $ServiceName"

$exePath = Join-Path $InstallFolder "ServicioRESTEjecucionComandos.exe"
if (-not (Test-Path $exePath)) {
    throw "Service executable not found: $exePath"
}

# Escape quotes for sc.exe
$escapedExePath = '"' + $exePath + '"'

Write-Log "Binary path: $escapedExePath"

# -----------------------------------------------------------------------
# Create the Windows Service
# -----------------------------------------------------------------------
$scResult = & sc.exe create $ServiceName `
    binPath= $escapedExePath `
    start= auto `
    displayName= $DisplayName `
    description= $Description 2>&1

Write-Log "sc.exe create output: $($scResult -join ' ')"

# -----------------------------------------------------------------------
# Configure service recovery actions
# -----------------------------------------------------------------------
Write-Log "Configuring service recovery actions"

$recoveryResult = & sc.exe failure $ServiceName `
    reset= 86400 `
    actions= restart/5000/restart/10000/restart/30000 2>&1

Write-Log "sc.exe failure output: $($recoveryResult -join ' ')"

Write-Log "Windows Service installation complete"
