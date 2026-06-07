$serviceName = "ServicioRESTEjecucionComandos"
$installPath = "C:\Program Files\ServicioRESTEjecucionComandos"
$exePath = Join-Path $installPath "ServicioRESTEjecucionComandos.exe"

Write-Host "Registrando servicio..."

New-Service `
    -Name $serviceName `
    -BinaryPathName $exePath `
    -DisplayName "ServicioRESTEjecucionComandos" `
    -Description "Servicio REST de ejecucion de ETLs" `
    -StartupType Automatic

Start-Service $serviceName

Write-Host "Instalación finalizada."