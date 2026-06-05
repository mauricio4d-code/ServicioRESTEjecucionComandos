<#
.SYNOPSIS
    Stops and removes the ServicioRESTEjecucionComandos Windows Service.
#>

[CmdletBinding()]
param(
    [string]$ServiceName = "ServicioRESTEjecucionComandos"
)

$ErrorActionPreference = "SilentlyContinue"

function Write-Log {
    param([string]$Message)
    Write-Host "Uninstall-Service: $Message"
}

Write-Log "Removing Windows Service: $ServiceName"

# -----------------------------------------------------------------------
# Stop the service if running
# -----------------------------------------------------------------------
try {
    $service = Get-Service -Name $ServiceName -ErrorAction Stop
    if ($service.Status -ne "Stopped") {
        Write-Log "Stopping service (current state: $($service.Status))"
        Stop-Service -Name $ServiceName -Force
        # Wait for service to stop
        $timeout = 30
        $elapsed = 0
        while ((Get-Service -Name $ServiceName).Status -ne "Stopped" -and $elapsed -lt $timeout) {
            Start-Sleep -Seconds 2
            $elapsed += 2
        }
        Write-Log "Service stopped"
    }
}
catch {
    Write-Log "Service not found or already stopped: $_"
}

# -----------------------------------------------------------------------
# Delete the service
# -----------------------------------------------------------------------
try {
    $deleteResult = & sc.exe delete $ServiceName 2>&1
    Write-Log "sc.exe delete output: $($deleteResult -join ' ')"
}
catch {
    Write-Log "Error deleting service: $_"
}

Write-Log "Windows Service removal complete"
