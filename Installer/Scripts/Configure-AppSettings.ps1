<#
.SYNOPSIS
    Transforms the appsettings.json template into a configured file during installation.

.DESCRIPTION
    Reads MSI custom action data, replaces placeholders in appsettings.json template,
    and writes the final configuration file. Supports upgrade scenarios by preserving
    existing values when appsettings.json already exists.

.PARAMETER InstallFolder
    Root installation directory.

.PARAMETER ServicePort
    Kestrel HTTP port.

.PARAMETER AuthDbProvider
    Authentication database provider (postgres/sqlserver/sqlite).

.PARAMETER AuthDbConnectionString
    Connection string for the authentication database.

.PARAMETER ServiceDbProvider
    Service database provider (postgres/sqlserver).

.PARAMETER ServiceDbConnectionString
    Connection string for the service database.

.PARAMETER DataxExePath
    Full path to Datax.SAFI.Downloader.exe.

.PARAMETER DailyCodes
    Comma-separated list of daily codes.

.PARAMETER ExcludedCodes
    Comma-separated list of excluded codes.

.PARAMETER JwtSecretKey
    JWT signing secret key.

.PARAMETER JwtAccessTokenMinutes
    JWT access token lifetime in minutes.

.PARAMETER JwtRefreshTokenDays
    JWT refresh token lifetime in days.

.PARAMETER LogsPath
    Full path to the logs directory.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InstallFolder,

    [string]$ServicePort = "5000",

    [string]$AuthDbProvider = "sqlite",

    [string]$AuthDbConnectionString = "",

    [string]$ServiceDbProvider = "postgres",

    [string]$ServiceDbConnectionString = "",

    [string]$DataxExePath = "",

    [string]$DailyCodes = "",

    [string]$ExcludedCodes = "",

    [string]$JwtSecretKey = "",

    [string]$JwtAccessTokenMinutes = "5",

    [string]$JwtRefreshTokenDays = "30",

    [string]$LogsPath = ""
)

$ErrorActionPreference = "Stop"

# -----------------------------------------------------------------------
# Logging helper — writes to MSI log stream via Write-Host
# -----------------------------------------------------------------------
function Write-Log {
    param([string]$Message)
    Write-Host "Configure-AppSettings: $Message"
}

Write-Log "Starting appsettings.json configuration"
Write-Log "  InstallFolder: $InstallFolder"

# -----------------------------------------------------------------------
# Determine paths
# -----------------------------------------------------------------------
$templatePath = Join-Path $InstallFolder "appsettings.json.template"
$configPath   = Join-Path $InstallFolder "appsettings.json"

if (-not $LogsPath) {
    $LogsPath = Join-Path $InstallFolder "Logs"
}

# -----------------------------------------------------------------------
# Handle upgrade: if appsettings.json exists, preserve it and merge new keys
# -----------------------------------------------------------------------
if (Test-Path $configPath) {
    Write-Log "Existing appsettings.json found — merging configuration"
    $existingJson = Get-Content $configPath -Raw | ConvertFrom-Json

    # Update values from installer parameters
    if ($ServicePort -ne "5000" -or $existingJson.Kestrel.Endpoints.Http.Url -notlike "*:$ServicePort*") {
        $existingJson.Kestrel.Endpoints.Http.Url = "http://localhost:$ServicePort"
    }
    if ($DataxExePath) {
        $existingJson.DataxConfig.ExePath = $DataxExePath
    }
    if ($AuthDbProvider -ne "sqlite") {
        $existingJson.Authentication.Provider = $AuthDbProvider
    }
    if ($ServiceDbProvider) {
        $existingJson.ServiceDb.Provider = $ServiceDbProvider
    }
    if ($JwtSecretKey) {
        $existingJson.Jwt.SecretKey = $JwtSecretKey
    }
    if ($JwtAccessTokenMinutes -ne "5") {
        $existingJson.Jwt.AccessTokenMinutes = [int]$JwtAccessTokenMinutes
    }
    if ($JwtRefreshTokenDays -ne "30") {
        $existingJson.Jwt.RefreshTokenDays = [int]$JwtRefreshTokenDays
    }

    # Convert back to JSON and write
    $updatedJson = $existingJson | ConvertTo-Json -Depth 10
    Set-Content -Path $configPath -Value $updatedJson -Encoding UTF8
    Write-Log "Configuration merged successfully"
}
else {
    # -----------------------------------------------------------------------
    # Fresh install: transform template
    # -----------------------------------------------------------------------
    if (-not (Test-Path $templatePath)) {
        # Template may already be named appsettings.json (copied from publish)
        $templatePath = $configPath
    }

    if (-not (Test-Path $templatePath)) {
        throw "appsettings.json template not found at $templatePath"
    }

    Write-Log "Transforming appsettings.json template"

    $content = Get-Content $templatePath -Raw

    # Replace placeholders
    $content = $content -replace '__SERVICE_PORT__', $ServicePort
    $content = $content -replace '__AUTH_DB_PROVIDER__', $AuthDbProvider
    $content = $content -replace '__SERVICE_DB_PROVIDER__', $ServiceDbProvider
    $content = $content -replace '__DATAEXEPATH__', $DataxExePath
    $content = $content -replace '__JWT_SECRET_KEY__', $JwtSecretKey
    $content = $content -replace '__JWT_ACCESS_TOKEN_MINUTES__', $JwtAccessTokenMinutes
    $content = $content -replace '__JWT_REFRESH_TOKEN_DAYS__', $JwtRefreshTokenDays

    # Handle logs path — escape backslashes for JSON
    $escapedLogsPath = $LogsPath -replace '\\', '\\\\'
    $content = $content -replace '__LOGS_PATH__', $escapedLogsPath

    # Handle connection strings
    $content = $content -replace '__AUTH_DB_CONNECTION_STRING__', $AuthDbConnectionString
    $content = $content -replace '__SERVICE_DB_CONNECTION_STRING__', $ServiceDbConnectionString

    # Handle array placeholders — convert comma-separated to JSON arrays
    if ($DailyCodes) {
        $dailyArray = ($DailyCodes -split ',' | ForEach-Object { '"$($_.Trim())"' }) -join ','
        $content = $content -replace '\["__DAILYCODES__"\]', "[$dailyArray]"
    }
    else {
        $content = $content -replace '\["__DAILYCODES__"\]', '[]'
    }

    if ($ExcludedCodes) {
        $excludedArray = ($ExcludedCodes -split ',' | ForEach-Object { '"$($_.Trim())"' }) -join ','
        $content = $content -replace '\["__EXCLUDEDCODES__"\]', "[$excludedArray]"
    }
    else {
        $content = $content -replace '\["__EXCLUDEDCODES__"\]', '[]'
    }

    Set-Content -Path $configPath -Value $content -Encoding UTF8
    Write-Log "appsettings.json created from template"

    # Remove template if it exists separately
    if ($templatePath -ne $configPath -and (Test-Path $templatePath)) {
        Remove-Item $templatePath -Force
        Write-Log "Removed template file"
    }
}

Write-Log "appsettings.json configuration complete"
