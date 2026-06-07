$ruleName = "ServicioRESTEjecucionComandos"
$exePath  = "C:\Program Files\ServicioRESTEjecucionComandos\ServicioRESTEjecucionComandos.exe"

$existingRule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue

if (-not $existingRule)
{
    New-NetFirewallRule `
        -DisplayName $ruleName `
        -Direction Inbound `
        -Program $exePath `
        -Action Allow `
        -Profile Domain,Private `
        -Enabled True

    Write-Host "Regla de firewall creada."
}
else
{
    Write-Host "La regla ya existe."
}