$serviceName = "ServicioRESTEjecucionComandos"

if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue)
{
    Stop-Service $serviceName -Force

    sc.exe delete $serviceName

    Write-Host "Servicio eliminado."
}