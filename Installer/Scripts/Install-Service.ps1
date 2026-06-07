<#
.SYNOPSIS
    Installs ServicioRESTEjecucionComandos as a Windows Service and configures recovery actions.
#>

param(
    [string]$BaseFolder
)

$LogPrueba = "D:\debug_install_service.txt"

try {
    # 1. Limpiar la ruta por si llegó con la comilla rota o caracteres extraños de escape
    # Si la ruta llegó como: C:\Program Files\ServicioRESTEjecucionComandos" debido al escape, esto lo limpia:
    $BaseFolder = $BaseFolder.Trim('"', ' ')
    if ($BaseFolder.EndsWith('\')) {
        $BaseFolder = $BaseFolder.Substring(0, $BaseFolder.Length - 1)
    }

    $ExePath = Join-Path $BaseFolder "ServicioRESTEjecucionComandos.exe"
    $ServiceName = "ServicioRESTEjecucionComandos" # Cambia esto si tu servicio se llama distinto

    Out-File -FilePath $LogPrueba -InputObject "Registrando servicio desde: $ExePath" -Append

    # 2. Controlar si el ejecutable realmente existe en el disco antes de registrarlo
    if (-not (Test-Path $ExePath)) {
        throw "El archivo ejecutable no se encuentra en la ruta: $ExePath"
    }

    # 3. Validar si el servicio ya existe para evitar que New-Service rompa el script
    $ExistingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($ExistingService) {
        Out-File -FilePath $LogPrueba -InputObject "El servicio ya existía. Deteniendo y eliminando previo..." -Append
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        # Usamos sc.exe para asegurar un borrado limpio e inmediato
        & sc.exe delete $ServiceName | Out-Null
        Start-Sleep -Seconds 2 # Pequeña pausa para que Windows libere el handle
    }

    # 4. Registrar el nuevo servicio de manera nativa
    # Nota: Para aplicaciones .NET 8 Worker Service, se suele configurar el tipo como OwnProcess
    New-Service -Name $ServiceName `
                -BinaryPathName "`"$ExePath`"" `
                -DisplayName "Servicio REST Ejecución Comandos" `
                -StartupType Automatic `
                -Description "Servicio REST para la ejecución remota de ETLs" `
                -ErrorAction Stop

    Out-File -FilePath $LogPrueba -InputObject "Servicio registrado con éxito total." -Append

} catch {
    $ErrorActual = $_.Exception.Message
    Out-File -FilePath $LogPrueba -InputObject "Error en Install-Service: $ErrorActual" -Append
    exit 1 # Le dice a WiX que la Custom Action falló
}