<#
.SYNOPSIS
    Registers Monitor-Health.ps1 as a Windows Scheduled Task.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InstallFolder,

    [string]$ServiceName = "ServicioRESTEjecucionComandos",

    [string]$TaskName = "ServicioRESTEjecucionComandos.HealthMonitor",

    [string]$HealthUrl = "http://localhost:5000/api/health",

    [string]$LogFile = "",

    [string]$EmailRecipients = "",

    [string]$SmtpServer = "",

    [int]$SmtpPort = 587,

    [string]$EmailFrom = "",

    [string]$EmailUsername = "",

    [string]$EmailPassword = ""
)

$ErrorActionPreference = "Stop"

function Write-Log {
    param([string]$Message)
    Write-Host "Install-ScheduledTask: $Message"
}

Write-Log "Registering health monitor scheduled task: $TaskName"

# -----------------------------------------------------------------------
# Build the task command line
# -----------------------------------------------------------------------
if (-not $LogFile) {
    $LogFile = Join-Path $InstallFolder "Logs\health_monitor.log"
}

$scriptPath = Join-Path $InstallFolder "Monitor-Health.ps1"
if (-not (Test-Path $scriptPath)) {
    Write-Log "WARNING: Monitor-Health.ps1 not found at $scriptPath — skipping scheduled task creation"
    return
}

# Build PowerShell arguments
$psArgs = "-ExecutionPolicy Bypass -File `"$scriptPath`""
$psArgs += " -RestartServiceOnFailure `\$true"
$psArgs += " -LogFile `"$LogFile`""
$psArgs += " -HealthUrl `"$HealthUrl`""
$psArgs += " -ServiceName `"$ServiceName`""

if ($EmailRecipients) {
    $recipientsArray = ($EmailRecipients -split ',' | ForEach-Object { "`"$($_.Trim())`"" }) -join ','
    $psArgs += " -EmailRecipients $recipientsArray"
    $psArgs += " -SmtpServer `"$SmtpServer`""
    $psArgs += " -SmtpPort $SmtpPort"
    $psArgs += " -EmailFrom `"$EmailFrom`""
    $psArgs += " -EmailUsername `"$EmailUsername`""
    $psArgs += " -EmailPassword `"$EmailPassword`""
}

$fullCommand = "powershell.exe $psArgs"
Write-Log "Task command: $fullCommand"

# -----------------------------------------------------------------------
# Create the scheduled task
# -----------------------------------------------------------------------
$schtasksArgs = @(
    "/create",
    "/TN", $TaskName,
    "/TR", "`"powershell.exe`" $psArgs",
    "/SC", "ONSTART",
    "/RU", "SYSTEM",
    "/RL", "HIGHEST",
    "/F",
    "/DL", "NONE"
)

Write-Log "Executing: schtasks.exe $($schtasksArgs -join ' ')"

$result = & schtasks.exe $schtasksArgs 2>&1
Write-Log "schtasks.exe output: $($result -join ' ')"

Write-Log "Scheduled task registration complete"
