<#
.SYNOPSIS
    Transforms the appsettings.json template into a configured file during installation.

.DESCRIPTION
    Reads MSI custom action data, replaces placeholders in appsettings.json template,
    and writes the final configuration file. Supports upgrade scenarios by preserving
    existing values when appsettings.json already exists.
#>

$ErrorActionPreference = "Stop"

# -----------------------------------------------------------------------
# Logging helper — writes to MSI log stream via Write-Host
# -----------------------------------------------------------------------
function Write-Log {
    param([string]$Message)
    Write-Host "Configure-AppSettings: $Message"
}

Write-Log "Starting appsettings.json configuration via CustomActionData"

# -----------------------------------------------------------------------
# PARSEO SEGURO DE CUSTOMACTIONDATA (SOLUCIÓN AL DESBORDAMIENTO)
# -----------------------------------------------------------------------

$LogPrueba = "D:\debug_ps.txt"

try {
    # At the beginning of the script, force it to be located in the folder where the script resides.
    # or at the root, depending on your needs
    $PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
    Set-Location $PSScriptRoot
    
    # Capture the plain text block sent by the installer
    #$caData = $env:CustomActionData
    $caData = $args[0]
    if (-not $caData) {
        throw "Error crítico: El instalador no transfirió los parámetros a CustomActionData."
    }

    # Robust parsing tolerant to empty or null values
    $params = @{}
    if ($caData) {
        $caData -split ';' | ForEach-Object {
            if ($_ -and $_.Contains('=')) {
                $idx = $_.IndexOf('=')
                $key = $_.Substring(0, $idx).Trim()
                $val = $_.Substring($idx + 1)
                $params[$key] = $val
            }
        }
    }

    # Safe mapping: If the parameter does not exist or is null, it is assigned an empty string or a default string.
    $InstallFolder             = $params["INSTALLFOLDER"]
    $ServicePort               = if ($params["ServicePort"]) { $params["ServicePort"] } else { "5000" }
    $AuthDbProvider            = if ($params["AuthDbProvider"]) { $params["AuthDbProvider"] } else { "sqlite" }
    $AuthDbConnectionString    = if ($params["AuthDbConnectionString"]) { $params["AuthDbConnectionString"] } else { "" }
    $ServiceDbProvider         = if ($params["ServiceDbProvider"]) { $params["ServiceDbProvider"] } else { "postgres" }
    $ServiceDbConnectionString = if ($params["ServiceDbConnectionString"]) { $params["ServiceDbConnectionString"] } else { "" }
    $DataxExePath              = if ($params["DataxExePath"]) { $params["DataxExePath"] } else { "" }
    $DailyCodes                = if ($params["DailyCodes"]) { $params["DailyCodes"] } else { "" }
    $ExcludedCodes             = if ($params["ExcludedCodes"]) { $params["ExcludedCodes"] } else { "" }
    $JwtSecretKey              = if ($params["JwtSecretKey"]) { $params["JwtSecretKey"] } else { "" }
    $JwtAccessTokenMinutes     = if ($params["JwtAccessTokenMinutes"]) { $params["JwtAccessTokenMinutes"] } else { "5" }
    $JwtRefreshTokenDays       = if ($params["JwtRefreshTokenDays"]) { $params["JwtRefreshTokenDays"] } else { "30" }
    $LogsPath                  = if ($params["LogsPath"]) { $params["LogsPath"] } else { "" }

    Out-File -FilePath $LogPrueba -InputObject "InstallFolder: $InstallFolder" -Append
    Out-File -FilePath $LogPrueba -InputObject "ServicePort: $ServicePort" -Append
    Out-File -FilePath $LogPrueba -InputObject "AuthDbProvider: $AuthDbProvider" -Append
    Out-File -FilePath $LogPrueba -InputObject "AuthDbConnectionString: $AuthDbConnectionString" -Append
    Out-File -FilePath $LogPrueba -InputObject "ServiceDbProvider: $ServiceDbProvider" -Append
    Out-File -FilePath $LogPrueba -InputObject "ServiceDbConnectionString: $ServiceDbConnectionString" -Append
    Out-File -FilePath $LogPrueba -InputObject "DataxExePath: $DataxExePath" -Append
    Out-File -FilePath $LogPrueba -InputObject "DailyCodes: $DailyCodes" -Append
    Out-File -FilePath $LogPrueba -InputObject "ExcludedCodes: $ExcludedCodes" -Append
    Out-File -FilePath $LogPrueba -InputObject "JwtSecretKey: $JwtSecretKey" -Append
    Out-File -FilePath $LogPrueba -InputObject "JwtAccessTokenMinutes: $JwtAccessTokenMinutes" -Append
    Out-File -FilePath $LogPrueba -InputObject "JwtRefreshTokenDays: $JwtRefreshTokenDays" -Append
    Out-File -FilePath $LogPrueba -InputObject "LogsPath: $LogsPath" -Append

    if (-not $InstallFolder) {
        throw "Error crítico: El parámetro INSTALLFOLDER no puede estar vacío."
    }

    Write-Log "  InstallFolder: $InstallFolder"
    Write-Log "  ServicePort: $ServicePort"

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
            $dailyArray = ($DailyCodes -split ',' | ForEach-Object { "`"$($_.Trim())`"" }) -join ','
            $content = $content -replace '\["__DAILYCODES__"\]', "[$dailyArray]"
        }
        else {
            $content = $content -replace '\["__DAILYCODES__"\]', '[]'
        }

        if ($ExcludedCodes) {
            $excludedArray = ($ExcludedCodes -split ',' | ForEach-Object { "`"$($_.Trim())`"" }) -join ','
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

} catch {
    # Si el script falla, esto escribirá el error exacto en la carpeta de Logs
    $ErrorActual = $_.Exception.Message
    Out-File -FilePath $LogPrueba -InputObject "Error en script: $ErrorActual" -Append
    exit 1 # Le sigue diciendo a WiX que falló
}



