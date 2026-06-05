<#
.SYNOPSIS
    Removes the health monitor Windows Scheduled Task.
#>

[CmdletBinding()]
param(
    [string]$TaskName = "ServicioRESTEjecucionComandos.HealthMonitor"
)

$ErrorActionPreference = "SilentlyContinue"

function Write-Log {
    param([string]$Message)
    Write-Host "Uninstall-ScheduledTask: $Message"
}

Write-Log "Removing scheduled task: $TaskName"

try {
    $result = & schtasks.exe /delete /TN $TaskName /F 2>&1
    Write-Log "schtasks.exe output: $($result -join ' ')"
}
catch {
    Write-Log "Error removing scheduled task: $_"
}

Write-Log "Scheduled task removal complete"
