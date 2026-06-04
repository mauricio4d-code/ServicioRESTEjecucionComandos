<#
.SYNOPSIS
    Monitor script for ServicioRESTEjecucionComandos Windows Service.
    Periodically checks the /api/health endpoint and logs status.

.DESCRIPTION
    Polls the health endpoint at configurable intervals.
    Logs results to a file and optionally sends Windows EventLog entries on failure.

.PARAMETER HealthUrl
    URL of the health endpoint. Default: http://localhost:5000/api/health

.PARAMETER IntervalSeconds
    Seconds between health checks. Default: 30

.PARAMETER LogFile
    Path to the log file. Default: .\health_monitor.log

.PARAMETER RestartServiceOnFailure
    If $true, attempts to restart the Windows service when health check fails.

.PARAMETER ServiceName
    Windows Service name. Default: ServicioRESTEjecucionComandos

.EXAMPLE
    .\Monitor-Health.ps1
    .\Monitor-Health.ps1 -HealthUrl "http://localhost:5000/api/health" -IntervalSeconds 60
    .\Monitor-Health.ps1 -RestartServiceOnFailure $true
#>

[CmdletBinding()]
param(
    [string]$HealthUrl = "http://localhost:5000/api/health",
    [int]$IntervalSeconds = 30,
    [string]$LogFile = ".\health_monitor.log",
    [switch]$RestartServiceOnFailure,
    [string]$ServiceName = "ServicioRESTEjecucionComandos"
)

# -----------------------------------------------------------------------
# Logging helper
# -----------------------------------------------------------------------
function Write-Log {
    param(
        [string]$Message,
        [string]$Level = "INFO"
    )
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
    $logLine = "[$timestamp] [$Level] $Message"
    Add-Content -Path $LogFile -Value $logLine
    Write-Host $logLine
}

# -----------------------------------------------------------------------
# Health check function
# -----------------------------------------------------------------------
function Test-HealthEndpoint {
    param([string]$Url)

    try {
        $response = Invoke-RestMethod -Uri $Url -Method Get -TimeoutSec 10
        return [PSCustomObject]@{
            Success     = $true
            Status      = $response.status
            Timestamp   = $response.timestamp
            Components  = $response.components
            Raw         = $response
        }
    }
    catch {
        return [PSCustomObject]@{
            Success     = $false
            Status      = "Error"
            Timestamp   = (Get-Date -Format "o")
            Components  = $null
            Error       = $_.Exception.Message
            Raw         = $null
        }
    }
}

# -----------------------------------------------------------------------
# Analyze health result and return overall status
# -----------------------------------------------------------------------
function Get-OverallStatus {
    param($HealthResult)

    if (-not $HealthResult.Success) {
        return "CRITICAL"
    }

    if ($HealthResult.Status -ne "Healthy") {
        return "DEGRADED"
    }

    # Check individual components
    $unhealthyComponents = @()
    if ($HealthResult.Components) {
        foreach ($component in $HealthResult.Components.PSObject.Properties) {
            if ($component.Value.status -ne "Healthy") {
                $unhealthyComponents += "$($component.Name)=$($component.Value.status)"
            }
        }
    }

    if ($unhealthyComponents.Count -gt 0) {
        return "DEGRADED ($($unhealthyComponents -join ', '))"
    }

    return "HEALTHY"
}

# -----------------------------------------------------------------------
# Attempt to restart the Windows service
# -----------------------------------------------------------------------
function Restart-ServiceIfNeeded {
    param([string]$SvcName)

    try {
        $service = Get-Service -Name $SvcName -ErrorAction Stop
        Write-Log "Restarting service '$SvcName' (current state: $($service.Status))" "WARN"
        Restart-Service -Name $SvcName -Force
        Start-Sleep -Seconds 5
        $newState = (Get-Service -Name $SvcName).Status
        Write-Log "Service '$SvcName' restarted. New state: $newState" "INFO"
    }
    catch {
        Write-Log "Failed to restart service '$SvcName': $($_.Exception.Message)" "ERROR"
    }
}

# -----------------------------------------------------------------------
# Format health details for logging
# -----------------------------------------------------------------------
function Format-HealthDetails {
    param($HealthResult)

    $details = @()
    $details += "Overall: $(Get-OverallStatus -HealthResult $HealthResult)"
    $details += "Timestamp: $($HealthResult.Timestamp)"

    if ($HealthResult.Components) {
        foreach ($component in $HealthResult.Components.PSObject.Properties) {
            $name = $component.Name
            $status = $component.Value.status
            $desc = $component.Value.description
            $dataStr = ""
            if ($component.Value.data) {
                $dataEntries = @()
                foreach ($d in $component.Value.data.PSObject.Properties) {
                    $dataEntries += "$($d.Name)=$($d.Value)"
                }
                $dataStr = " [$($dataEntries -join ', ')]"
            }
            $details += "  Component '$name': $status - $desc$dataStr"
        }
    }

    if ($HealthResult.Error) {
        $details += "Error: $($HealthResult.Error)"
    }

    return ($details -join "`n")
}

# -----------------------------------------------------------------------
# Main monitoring loop
# -----------------------------------------------------------------------
Write-Log "========================================"
Write-Log "Health Monitor started"
Write-Log "  Endpoint: $HealthUrl"
Write-Log "  Interval: ${IntervalSeconds}s"
Write-Log "  Log File: $LogFile"
Write-Log "  Auto-Restart: $RestartServiceOnFailure"
Write-Log "========================================"

$consecutiveFailures = 0
$maxConsecutiveFailures = 3

try {
    while ($true) {
        $healthResult = Test-HealthEndpoint -Url $HealthUrl
        $statusDetails = Format-HealthDetails -HealthResult $healthResult
        $overallStatus = Get-OverallStatus -HealthResult $healthResult

        if ($overallStatus -eq "HEALTHY") {
            Write-Log "Health check PASSED - $overallStatus" "INFO"
            Write-Log $statusDetails "DEBUG"
            $consecutiveFailures = 0
        }
        else {
            $consecutiveFailures++
            Write-Log "Health check FAILED ($consecutiveFailures/$maxConsecutiveFailures) - $overallStatus" "WARN"
            Write-Log $statusDetails "WARN"

            if ($RestartServiceOnFailure -and $consecutiveFailures -ge $maxConsecutiveFailures) {
                Write-Log "Max consecutive failures reached. Attempting service restart..." "ERROR"
                Restart-ServiceIfNeeded -SvcName $ServiceName
                $consecutiveFailures = 0
            }
        }

        Start-Sleep -Seconds $IntervalSeconds
    }
}
catch {
    Write-Log "Monitor crashed: $($_.Exception.Message)" "ERROR"
    throw
}
finally {
    Write-Log "Health Monitor stopped" "INFO"
}