### Ejecutar el PS:

# Instalar:
powershell.exe -ExecutionPolicy Bypass -File .\Install.ps1


# Añadir regla para el firewall:
powershell.exe -ExecutionPolicy Bypass -File .\FirewallRule.ps1


# Verificar firewall:
powershell.exe -ExecutionPolicy Bypass -File .\FirewallCheck.ps1

(Se deberia ver la ruta del ejecutable autorizada)


# Recomendable validar desde el servidor que Kestrel realmente está escuchando en todas las interfaces:
powershell.exe -ExecutionPolicy Bypass -File .\KestrelCheck.ps1

La salida correcta debería ser algo parecido a:

	TCP    0.0.0.0:5000     0.0.0.0:0     LISTENING

o en IPv6:

	TCP    [::]:5000        [::]:0         LISTENING


# Luego, validar desde otra máquina de la LAN

PS: Test-NetConnection IP_DEL_SERVIDOR -Port 5000

Si TcpTestSucceeded devuelve True, el firewall y Kestrel están configurados correctamente. Si devuelve False, habría que revisar el perfil de red de Windows (Pública/Privada/Dominio) o alguna política de seguridad adicional.


# Desinstalar:
powershell.exe -ExecutionPolicy Bypass -File .\Uninstall.ps1
