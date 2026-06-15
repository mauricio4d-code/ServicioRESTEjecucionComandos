# ServicioRESTEjecucionComandos

Servicio REST desarrollado en C# con ASP.NET Core 8 que proporciona una interfaz web autenticada para ejecutar comandos de forma asíncrona mediante Hangfire con procesamiento paralelo configurable.

El sistema incluye autenticación JWT con tokens de acceso y refrescado (refresh tokens), validación contra una base de datos legacy, registro de auditoría, limpieza automática de tokens expirados, gestión de ejecuciones a través de las tablas `hist_etl_execution` y `hist_etl_execution_scheduled` en una base de datos de servicio (PostgreSQL, SQL Server o SQLite), programación de ejecuciones ETL recurrentes mediante Hangfire, monitoreo de salud con health checks, y reinicio automático de servicios Windows.

---

## Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (o superior)
- Windows (para ejecución de aplicaciones de consola nativas y funcionalidad de Windows Service)
- Base de datos legacy (SQLite, PostgreSQL o SQL Server) configurada con usuarios y roles
- Base de datos de servicio (PostgreSQL, SQL Server o SQLite) para las tablas `hist_etl_execution` y `hist_etl_execution_scheduled`

---

## Estructura del Proyecto

```
ServicioRESTEjecucionComandos/
├── Program.cs                          # Punto de entrada y configuración de dependencias
├── PublicProgram.cs                    # Hace Program público para WebApplicationFactory (integration tests)
├── ServicioRESTEjecucionComandos.csproj # Archivo de proyecto y paquetes NuGet
├── appsettings.json                    # Configuración base del servicio
├── appsettings.Development.json        # Configuración específica para ambiente Development
├── appsettings.Production.json         # Configuración específica para ambiente Production
├── Constants/
│   ├── AuditEventType.cs               # Constantes para tipos de eventos de auditoría
│   ├── ControllerAction.cs             # Constantes para acciones de controlador (ACTUALIZAR, REPROCESAR)
│   ├── DbProvider.cs                   # Constantes para identificadores de proveedor de BD
│   ├── DtxProcess.cs                   # Constantes para tipos de proceso DTX
│   ├── DtxProcessStatus.cs             # Constantes para estados de proceso DTX
│   ├── EtlStatus.cs                    # Constantes para estados ETL (PENDIENTE, EN PROCESO, EXITOSO, FALLIDO)
│   ├── Policy.cs                       # Constantes para nombres de políticas de autorización
│   ├── Role.cs                         # Constantes para nombres de roles
│   ├── Schedule.cs                     # Constantes para programación Hangfire
│   ├── SignalREvent.cs                 # Constantes para nombres de eventos SignalR
│   ├── TriggerType.cs                  # Constantes para tipos de disparador (MANUAL, REPROCESO)
│   └── UserState.cs                    # Constantes para estados de usuario
├── Controllers/
│   ├── AuthController.cs               # Endpoints de autenticación (login, refresh, logout)
│   ├── ETLExecutorController.cs        # Endpoints REST para ETL, base-datos, y consultas
│   └── SchedulesController.cs          # Endpoints CRUD para programación de ETLs
├── Data/
│   ├── AuthDbContext.cs                # Contexto EF Core para BD legacy (usuarios/roles)
│   ├── RefreshTokenDbContext.cs        # Contexto EF Core para BD SQLite (tokens/auditoría)
│   ├── ScheduleDbContext.cs            # Contexto EF Core para BD SQLite (programaciones ETL)
│   └── ServiceDbContext.cs             # Contexto EF Core para BD de servicio (hist_etl_execution)
├── DTOs/
│   ├── BaseDatosResponse.cs            # DTO para respuesta de lookup de base_datos (incluye IsDayBased)
│   ├── ErrorResponse.cs                # DTO para respuestas de error
│   ├── EtlScheduleDto.cs               # DTOs para crear/actualizar/leer programaciones ETL
│   ├── LoginRequest.cs                 # DTO para solicitud de inicio de sesión
│   ├── LoginResponse.cs                # DTO para respuesta con tokens JWT
│   ├── QueryResult.cs                  # DTO para resultados de consulta de seguimiento
│   ├── RefreshRequest.cs               # DTO para solicitud de refresco de token
│   └── RefreshResponse.cs              # DTO para respuesta con nuevos tokens
├── HealthChecks/
│   ├── AuthDbHealthCheck.cs            # Health check para base de datos legacy
│   ├── HangfireHealthCheck.cs          # Health check para Hangfire
│   ├── ServiceDbHealthCheck.cs         # Health check para base de datos de servicio
│   ├── SqliteDbHealthCheck.cs          # Health check para base de datos SQLite
│   └── UptimeHealthCheck.cs            # Health check para uptime y memoria
├── Hubs/
│   └── EtlNotificationHub.cs           # Hub de SignalR para notificaciones en tiempo real
├── Interfaces/
│   ├── IPasswordValidator.cs           # Interfaz para validación de contraseñas
│   └── LegacyPasswordValidator.cs      # Implementación para BD legacy
├── Models/
│   ├── AuthAuditLog.cs                 # Modelo de registro de auditoría
│   ├── BaseDatos.cs                    # Modelo para tabla base_datos (lookup)
│   ├── DtxProcess.cs                   # Modelo para tabla dtx_process (monitoreo de procesos)
│   ├── ETLExecutionHistory.cs          # Modelo para historial de ejecuciones ETL
│   ├── ETLExecutionHistoryScheduled.cs # Modelo para historial de ejecuciones programadas
│   ├── EtlSchedule.cs                  # Modelo para programaciones ETL recurrentes
│   ├── ExecutionQueueItem.cs           # Modelo para items de la cola
│   ├── RefreshToken.cs                 # Modelo de token de refresco
│   ├── User.cs                         # Modelo de usuario legacy
│   └── UserRole.cs                     # Modelo de rol de usuario
├── Repositories/
│   ├── AuthAuditLogRepository.cs       # Repositorio para registros de auditoría
│   ├── DtxProcessRepository.cs         # Repositorio para consultas de dtx_process
│   ├── ETLExecutionHistoryRepository.cs # Repositorio para CRUD de ETLExecutionHistory
│   ├── ETLExecutionHistoryScheduledRepository.cs # Repositorio para historial programado
│   ├── EtlScheduleRepository.cs        # Repositorio para CRUD de EtlSchedule
│   └── RefreshTokenRepository.cs       # Repositorio para persistencia de refresh tokens
├── Services/
│   ├── AuthService.cs                  # Orquestador de flujos de autenticación
│   ├── CommandExecutor.cs              # Ejecuta la aplicación de consola
│   ├── EtlJobService.cs                # Servicio central para ejecuciones ETL (Hangfire + CommandExecutor)
│   ├── ExecutionNotifier.cs            # Notificador SignalR para actualizaciones en tiempo real
│   ├── JwtService.cs                   # Generación de tokens JWT
│   ├── RefreshTokenCleanupService.cs   # Limpieza automática de tokens expirados
│   ├── RefreshTokenService.cs          # Generación y rotación de refresh tokens
│   ├── ScheduleSyncService.cs          # Sincroniza programaciones DB con Hangfire recurring jobs
│   └── ServiceRestartMonitorService.cs # Monitorea procesos y reinicia Windows Service
└── wwwroot/
    ├── auth.js                         # Cliente JavaScript para autenticación
    ├── favicon.ico                     # Icono de pestaña del navegador
    ├── index.html                      # Interfaz web con selector de BD y tabla de resultados
    ├── login.html                      # Interfaz de inicio de sesión
    ├── scheduler.html                  # Interfaz web para gestionar programaciones ETL
    ├── scheduler.js                    # Cliente JavaScript para la interfaz de programaciones
    ├── images/
    │   └── logo_datax_bolivia.svg      # Logo DataX Bolivia
    └── styles/
        ├── index.css                   # Estilos de la interfaz principal
        └── scheduler.css               # Estilos de la interfaz de programaciones
```

---

## Configuración

El archivo [`appsettings.json`](appsettings.json) contiene todas las configuraciones del servicio:

### DataxConfig

Configura la ruta de la aplicación de consola que se ejecutará:

| Clave | Descripción | Ejemplo |
|-------|-------------|---------|
| `ExePath` | Ruta completa a `Datax.SAFI.Downloader.exe` | `C:\ruta\a\la\app.exe` |

### QueueConfig

Configura el comportamiento de la cola de ejecución:

| Clave | Descripción | Valor por Defecto |
|-------|-------------|-------------------|
| `WaitSeconds` | Segundos de espera cuando la cola está vacía | `10` |
| `ProcessCheckWaitSeconds` | Segundos de espera entre verificaciones de proceso | `10` |
| `MaxParallelExecutions` | Máximo de comandos ejecutándose simultáneamente | `1` |
| `DailyCodes` | Lista de códigos que usan lógica de fechas basada en días (no meses) | `[]` |
| `ExcludedCodes` | Lista de códigos excluidos de la ejecución | `[]` |

### ServiceDb

Configura la base de datos de servicio donde se almacenan las tablas `hist_etl_execution` y `hist_etl_execution_scheduled`:

| Clave | Descripción | Valores Válidos |
|-------|-------------|-----------------|
| `Provider` | Proveedor de base de datos para ServiceDb | `postgres`, `sqlserver`, `sqlite` |

### Authentication

| Clave | Descripción | Valores Válidos |
|-------|-------------|-----------------|
| `Provider` | Proveedor de base de datos legacy | `sqlite`, `postgres`, `sqlserver` |

### ServiceRestart

Configura el monitoreo y reinicio automático del servicio Windows:

| Clave | Descripción | Valor por Defecto |
|-------|-------------|-------------------|
| `WindowsServiceName` | Nombre del servicio Windows a reiniciar | - |
| `CheckIntervalMinutes` | Intervalo entre verificaciones de procesos | `5` |
| `RunningMinutesThreshold` | Umbral de minutos para considerar un proceso como largo | `30` |

### ConnectionStrings

| Clave | Descripción |
|-------|-------------|
| `AuthDatabase` | Cadena de conexión a la BD legacy (usuarios/roles) |
| `RefreshTokenDatabase` | Cadena de conexión a la BD SQLite (tokens/auditoría/programaciones) |
| `ServiceDatabase` | Cadena de conexión a la BD de servicio (hist_etl_execution, base_datos) |

### Jwt

| Clave | Descripción | Valor por Defecto |
|-------|-------------|-------------------|
| `SecretKey` | Clave secreta para firmar tokens JWT (mínimo 32 caracteres) | - |
| `Issuer` | Emisor del token JWT | `ServicioRESTEjecucionComandos` |
| `Audience` | Audiencia del token JWT | `ServicioRESTEjecucionComandosClient` |
| `AccessTokenMinutes` | Vida útil del token de acceso (minutos) | `5` |
| `RefreshTokenDays` | Vida útil del token de refresco (días) | `30` |

### RefreshTokenCleanup

| Clave | Descripción | Valor por Defecto |
|-------|-------------|-------------------|
| `CleanupIntervalMinutes` | Intervalo entre limpiezas de tokens expirados | `60` |
| `AuditLogRetentionDays` | Días de retención para registros de auditoría | `90` |

### Hangfire

| Clave | Descripción | Valor por Defecto |
|-------|-------------|-------------------|
| `SyncIntervalSeconds` | Intervalo entre sincronizaciones de programaciones DB → Hangfire | `60` |

### RateLimiting

Configura la protección contra ataques de fuerza bruta en el endpoint de login:

| Clave | Descripción | Valor por Defecto |
|-------|-------------|-------------------|
| `LoginPolicy:WindowSeconds` | Ventana de tiempo para el límite de peticiones | `60` |
| `LoginPolicy:MaxRequests` | Máximo de peticiones permitidas en la ventana | `5` |
| `LoginPolicy:SegmentSeconds` | Segmento de tiempo para el límite secundario | `10` |
| `LoginPolicy:SegmentMaxRequests` | Máximo de peticiones por segmento | `1` |

### Kestrel

| Clave | Descripción | Valor por Defecto |
|-------|-------------|-------------------|
| `Endpoints:Http:Url` | URL de enlace del servidor | `http://localhost:5000` |

---

## Ejecutar el Servicio y Ambientes de Ejecución

ASP.NET Core determina el ambiente activo mediante la variable de entorno **`ASPNETCORE_ENVIRONMENT`**. Los archivos `appsettings.{Environment}.json` se cargan automáticamente y sobrescriben la configuración base.

### Cambiar ambiente y ejecutar el servicio

#### Windows PowerShell
```powershell
# Development
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run

# Production
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet run

# O usando el parámetro --environment
dotnet run --environment Development
dotnet run --environment Production
```

#### Windows CMD (Command Prompt)
```cmd
# Development
set ASPNETCORE_ENVIRONMENT=Development
dotnet run

# Development en una sola linea
set ASPNETCORE_ENVIRONMENT=Development && dotnet run

# Production
set ASPNETCORE_ENVIRONMENT=Production
dotnet run

# Production en una sola linea
set ASPNETCORE_ENVIRONMENT=Production && dotnet run
```

### Acceso al Servicio

Una vez iniciado el servicio, acceda a:

```
http://localhost:5000
```

(El puerto exacto se mostrará en la consola al iniciar)

---

### Configuración por ambiente

| Configuración | Default | Development | Production |
|---|---|---|---|
| `MinimumLevel.Default` | Information | **Debug** | Information |
| `retainedFileCountLimit` | 30 días | **7 días** | **90 días** |
| `outputTemplate` | Sin propiedades | **Con `{EventProperties}`** | Sin propiedades |

### Flujo de carga de configuración

```
appsettings.json
    ├──→ appsettings.Development.json  (si ASPNETCORE_ENVIRONMENT = Development)
    └──→ appsettings.Production.json   (si ASPNETCORE_ENVIRONMENT = Production)
```

**Nota:** La sección `Serilog` de cada archivo de ambiente **sobrescribe completamente** la sección base. Cada archivo debe incluir la configuración completa de Serilog.

### Modo Production

Cuando `ASPNETCORE_ENVIRONMENT=Production`:

- La base de datos SQLite se almacena en `%ProgramData%\ServicioRESTEjecucionComandos\`
- Los archivos de log se almacenan en `%ProgramData%\ServicioRESTEjecucionComandos\Logs\`
- Se habilita HSTS (HTTP Strict Transport Security)

---

## Windows Service (Modo Dual)

El servicio puede ejecutarse como aplicación de consola o como servicio de Windows gracias a `builder.Host.UseWindowsService()`. En modo Production, el servicio puede instalarse como un servicio de Windows que se inicia automáticamente con el sistema.

### Scripts de Instalación

La carpeta [`InstallerSimplified/Scripts/`](InstallerSimplified/Scripts/) contiene scripts de PowerShell para la instalación, configuración y desinstalación del servicio:

| Script | Descripción |
|--------|-------------|
| [`Install.ps1`](InstallerSimplified/Scripts/Install.ps1) | Instala el servicio de Windows |
| [`Uninstall.ps1`](InstallerSimplified/Scripts/Uninstall.ps1) | Desinstala el servicio de Windows |
| [`FirewallRule.ps1`](InstallerSimplified/Scripts/FirewallRule.ps1) | Añade la regla de firewall para el puerto Kestrel |
| [`FirewallCheck.ps1`](InstallerSimplified/Scripts/FirewallCheck.ps1) | Verifica que las reglas de firewall están configuradas |
| [`KestrelCheck.ps1`](InstallerSimplified/Scripts/KestrelCheck.ps1) | Valida que Kestrel está escuchando en todas las interfaces |

**Requisitos:** Ejecutar PowerShell como Administrador.

```powershell
# Instalar el servicio
powershell.exe -ExecutionPolicy Bypass -File .\Install.ps1

# Configurar firewall
powershell.exe -ExecutionPolicy Bypass -File .\FirewallRule.ps1

# Verificar configuración
powershell.exe -ExecutionPolicy Bypass -File .\FirewallCheck.ps1
powershell.exe -ExecutionPolicy Bypass -File .\KestrelCheck.ps1
```

Para validar la conectividad desde otra máquina de la red:
```powershell
Test-NetConnection IP_DEL_SERVIDOR -Port 5000
```

## Resumen de Configuración al Inicio

Al iniciar el servicio, se imprime un resumen de configuración en los logs que incluye los valores activos de:

| Parámetro | Descripción |
|-----------|-------------|
| `QueueConfig:DailyCodes` | Códigos diarios permitidos |
| `QueueConfig:ExcludedCodes` | Códigos excluidos de la cola |
| `ServiceDb:Provider` | Proveedor de base de datos para ETL (PostgreSQL/SQL Server/SQLite) |
| `Authentication:Provider` | Proveedor de base de datos para autenticación (PostgreSQL/SQL Server/SQLite) |
| `Jwt:AccessTokenMinutes` | Duración del token de acceso |
| `Jwt:RefreshTokenDays` | Duración del token de refresco |
| `RefreshTokenCleanup:CleanupIntervalMinutes` | Intervalo de limpieza de tokens |
| `RefreshTokenCleanup:AuditLogRetentionDays` | Retención de logs de auditoría |
| `ServiceRestart:WindowsServiceName` | Nombre del servicio Windows a monitorear |
| `ServiceRestart:CheckIntervalMinutes` | Intervalo de verificación del servicio |
| `ServiceRestart:RunningMinutesThreshold` | Umbral de tiempo de ejecución para reinicio |
| `QueueConfig:ProcessCheckWaitSeconds` | Tiempo de espera para verificación de proceso |

El servicio también resuelve las direcciones IP accesibles cuando Kestrel está vinculado a `0.0.0.0`, mostrando todas las URLs disponibles en la red local.

---

## Publicación Autocontenida

Para generar un despliegue autocontenido (sin requerir el SDK de .NET en el servidor destino), use el comando `dotnet publish` con la opción `--self-contained`. Esto empaqueta el runtime de .NET junto con la aplicación, produciendo un directorio ejecutable de forma independiente.

### PowerShell y CMD (Command Prompt)

```powershell
# Publicar como autocontenido para Windows x64
dotnet publish ServicioRESTEjecucionComandos.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish

# Ejecutar el servicio publicado
.\publish\ServicioRESTEjecucionComandos.exe
```

### Opciones de Publicación

| Opción | Descripción | Valor por Defecto |
|--------|-------------|-------------------|
| `-c Release` | Configuración de compilación optimizada | `Debug` |
| `-r win-x64` | Identificador de runtime destino | `win-x64` (definido en `.csproj`) |
| `--self-contained` | Incluye el runtime de .NET en el paquete | No incluido |
| `-o ./publish` | Directorio de salida | `bin/Release/net8.0/win-x64/publish` |

---
## Monitoreo de Salud

El servicio expone un endpoint de health checks en `/api/health` (sin autenticación) que reporta el estado de los siguientes componentes:

| Health Check | Nombre | Tags | Descripción |
|---|---|---|---|
| [`AuthDbHealthCheck`](HealthChecks/AuthDbHealthCheck.cs) | `auth_database` | `database` | Verifica conectividad a la base de datos legacy |
| [`ServiceDbHealthCheck`](HealthChecks/ServiceDbHealthCheck.cs) | `service_database` | `database` | Verifica conectividad a la base de datos de servicio |
| [`SqliteDbHealthCheck`](HealthChecks/SqliteDbHealthCheck.cs) | `sqlite_database` | `database` | Verifica conectividad a la base de datos SQLite |
| [`HangfireHealthCheck`](HealthChecks/HangfireHealthCheck.cs) | `hangfire` | `background-jobs` | Verifica que Hangfire esté operativo |
| [`UptimeHealthCheck`](HealthChecks/UptimeHealthCheck.cs) | `uptime` | `system` | Reporta uptime y uso de memoria |

**Respuesta de ejemplo:**
```json
{
  "status": "Healthy",
  "timestamp": "2026-06-11T08:00:00Z",
  "components": {
    "auth_database": {
      "status": "Healthy",
      "description": "Auth database connection OK",
      "data": { "database": "AuthDatabase", "provider": "Microsoft.EntityFrameworkCore.Sqlite" }
    },
    "service_database": {
      "status": "Healthy",
      "description": "Service database connection OK",
      "data": { "database": "ServiceDatabase", "provider": "Microsoft.EntityFrameworkCore.PostgreSQL" }
    },
    "sqlite_database": {
      "status": "Healthy",
      "description": "SQLite database connection OK",
      "data": { "database": "RefreshTokenDatabase" }
    },
    "hangfire": {
      "status": "Healthy",
      "description": "Hangfire server is running",
      "data": {}
    },
    "uptime": {
      "status": "Healthy",
      "description": "Service running for 01:23:45",
      "data": { "uptime_seconds": 5025, "memory_mb": 45.2 }
    }
  }
}
```

Además del endpoint `/api/health`, el proyecto incluye [`Monitor-Health.ps1`](Monitor-Health.ps1), un script de PowerShell para verificar el estado del servicio de forma remota:

```powershell
.\Monitor-Health.ps1 -Url "http://localhost:5000/api/health"
```

El script verifica:
- Conectividad HTTP al endpoint de salud
- Estado de los componentes (base de datos, Hangfire, tiempo de actividad)
- Respuesta JSON detallada con el estado de cada componente

---

## Sistemas de Logging

### Serilog

El servicio utiliza **Serilog** como proveedor de logging estructurado, reemplazando el logger por defecto de ASP.NET Core. Los logs se escriben simultáneamente a **consola** y a **archivos de texto** con rotación diaria.

#### Configuración

La sección `Serilog` en [`appsettings.json`](appsettings.json) define:

- **Sinks activos:** Consola y Archivo
- **Nivel mínimo:** `Information` (por defecto)
- **Rotación:** Diaria (`rollingInterval: Day`)
- **Retención:** 30 días (por defecto)
- **Formato:** `{Timestamp} [{Level}] {Message}{NewLine}{Exception}`

#### Archivos de log

Los archivos se generan en la carpeta `Logs/` (creada automáticamente al iniciar):

```
Logs/
├── log-2026-05-31.txt
├── log-2026-06-01.txt
└── ...
```

#### Ejemplo de salida

```
2026-05-31 06:34:30.123 -04:00 [INF] Successful login for user: admin@example.com from IP 192.168.1.100
2026-05-31 06:34:30.456 [ERR] Error executing command for item 42
   System.InvalidOperationException: Command failed
      at ServicioRESTEjecucionComandos.Services.CommandExecutor.ExecuteAsync()
```

#### Compatibilidad con `ILogger<T>`

Todo el código existente que inyecta `ILogger<T>` funciona sin cambios. Serilog se registra como proveedor de `Microsoft.Extensions.Logging`, por lo que las llamadas a `LogInformation()`, `LogWarning()`, `LogError()`, etc., se redirigen automáticamente a los sinks configurados.

---

## Autenticación

El servicio utiliza autenticación JWT con tokens de acceso de corta duración y tokens de refresco de larga duración. La página principal redirige automáticamente a la interfaz de inicio de sesión.

### Flujo de Autenticación

1. El usuario ingresa correo electrónico y contraseña en [`login.html`](wwwroot/login.html)
2. Se envía una solicitud POST al endpoint `/api/auth/login`
3. El sistema valida las credenciales contra la base de datos legacy
4. Se verifica que el usuario esté activo (`Userstate = "Activo"`) y tenga un rol asignado
5. Se generan un token de acceso JWT y un token de refresco
6. Los tokens se almacenan en `localStorage` del navegador
7. El usuario es redirigido a la interfaz principal

### Endpoints de Autenticación

#### POST /api/auth/login

Inicia sesión y devuelve los tokens JWT.

**Solicitud:**
```json
{
  "email": "usuario@ejemplo.com",
  "password": "contraseña_secreta"
}
```

**Respuesta (éxito - 200 OK):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "expiresIn": 300,
  "tokenType": "Bearer"
}
```

**Respuesta (error - 401 Unauthorized):**
```json
{
  "message": "Invalid email, password, or user account is not active."
}
```

#### POST /api/auth/refresh

Refresca el token de acceso utilizando un token de refresco válido.

**Solicitud:**
```json
{
  "token": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

**Respuesta (éxito - 200 OK):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "nuevo_token_de_refresco",
  "expiresIn": 300,
  "tokenType": "Bearer"
}
```

#### POST /api/auth/logout

Cierra sesión revocando el token de refresco.

**Solicitud:**
```json
{
  "token": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

**Respuesta (éxito - 200 OK):**
```json
{
  "message": "Logout successful."
}
```

### Protección de Endpoints

Todos los endpoints de `/api/etlexecutor/*` y `/api/schedules/*` están protegidos con el atributo `[Authorize]`. Las solicitudes sin un token JWT válido recibirán una respuesta `401 Unauthorized` con una respuesta JSON personalizada.

El endpoint `/api/schedules/*` requiere adicionalmente la política `AdminOnly`, que acepta los roles `"Administrador"` o `"Administador"` (con tolerancia a la falta de la 'r' final).

### Rate Limiting en Login

El endpoint `/api/auth/login` está protegido contra ataques de fuerza bruta mediante **Rate Limiting** con una política de ventana fija (`FixedWindowLimiter`). La configuración se define en la sección `RateLimiting:LoginPolicy` de [`appsettings.json`](appsettings.json).

- **Ventana:** 60 segundos (por defecto)
- **Máximo de peticiones:** 5 por ventana
- **Segmento:** 10 segundos con máximo 1 petición por segmento
- **Respuesta:** `429 Too Many Requests` con mensaje JSON personalizado

Cuando se excede el límite, el cliente recibe:
```json
{
  "error": "Demasiadas Peticiones",
  "message": "Se han realizado demasiados intentos de inicio de sesión. Inténtelo de nuevo más tarde."
}
```

---

## Endpoints de Ejecución de Comandos

### GET /api/etlexecutor/base-datos

Devuelve todos los registros de la tabla `base_datos` para poblar el selector de base de datos. Cada registro incluye el flag `IsDayBased` que indica si el código usa lógica de fechas basada en días (según la configuración `QueueConfig:DailyCodes`).

**Respuesta:**
```json
[
  { "codigo": "XYZ", "nombre": "Base de Datos XYZ", "isDayBased": false },
  { "codigo": "S_BOEIF_99_00012", "nombre": "Base de Datos Diaria", "isDayBased": true }
]
```

### GET /api/etlexecutor/query-results?codigo=XYZ

Ejecuta la consulta de seguimiento para el código de base de datos especificado, incluyendo el estado de ejecución más reciente desde `hist_etl_execution`.

**Respuesta:**
```json
[
  {
    "tipoEntidad": "ENTIDAD_1",
    "codEnvio": "ENV001",
    "fechaDatos": "2026-05-15",
    "estadoEjecucion": "EXITOSO",
    "triggerType": "MANUAL",
    "ultimaFechaEjecucion": "2026-05-20T10:05:00Z",
    "output": "Command output...",
    "error": ""
  }
]
```

### POST /api/etlexecutor/execute

Encola una nueva ejecución de comando vinculada a un registro `ETLExecutionHistory`.

**Solicitud:**
```json
{
  "action": "Actualizar",
  "tipoEntidad": "ENTIDAD_1",
  "codEnvio": "ENV001",
  "fechaDatos": "2026-05-15",
  "codigo": "XYZ",
  "isDayBased": false
}
```

**Respuesta:**
```json
{
  "queueItemId": "123e4567-e89b-12d3-a456-426614174000",
  "historyId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "PENDIENTE",
  "message": "Command enqueued successfully. Action: Actualizar"
}
```

### GET /api/etlexecutor/status/{historyId}

Devuelve el estado actual de un registro `ETLExecutionHistory` por su `HistoryId`. Usado para polling del progreso de ejecución.

**Respuesta:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "codEnvio": "ENV001",
  "tipoEntidad": "ENTIDAD_1",
  "fechaDatos": "2026-05-15",
  "codigo": "XYZ",
  "status": "EXITOSO",
  "triggerType": "MANUAL",
  "exitCode": 0,
  "output": "Command output...",
  "error": "",
  "executedAt": "2026-05-20T10:00:00Z",
  "completedAt": "2026-05-20T10:05:00Z"
}
```

---

## Programación de ETLs (Hangfire)

El servicio incluye un sistema de programación de ejecuciones ETL recurrentes basado en **Hangfire**, que permite definir programaciones con expresiones Cron y sincronizarlas automáticamente con los jobs de Hangfire.

### Componentes

| Componente | Descripción |
|---|---|
| [`EtlSchedule`](Models/EtlSchedule.cs) | Modelo que representa una programación ETL almacenada en la tabla `etl_schedule` |
| [`SchedulesController`](Controllers/SchedulesController.cs) | API REST para CRUD de programaciones |
| [`EtlScheduleRepository`](Repositories/EtlScheduleRepository.cs) | Repositorio para operaciones CRUD en `etl_schedule` |
| [`ScheduleSyncService`](Services/ScheduleSyncService.cs) | Servicio de fondo que sincroniza programaciones DB con Hangfire |
| [`EtlJobService`](Services/EtlJobService.cs) | Servicio central que ejecuta los jobs de Hangfire |
| [`ScheduleDbContext`](Data/ScheduleDbContext.cs) | DbContext para la tabla `etl_schedule` (SQLite) |
| [`scheduler.html`](wwwroot/scheduler.html) | Interfaz web para gestionar programaciones |

### Configuración de Hangfire

| Opción | Valor |
|---|---|
| `WorkerCount` | `5` |
| `Queues` | `["default"]` |
| `ShutdownTimeout` | `1 minuto` |
| Storage | InMemory (los jobs se pierden al reiniciar) |
| Dashboard | No habilitado |

### Flujo de programación

1. El usuario crea una programación a través de la interfaz [`scheduler.html`](wwwroot/scheduler.html) o la API `/api/schedules`
2. Se almacena un registro `EtlSchedule` en la tabla `etl_schedule` (SQLite)
3. [`ScheduleSyncService`](Services/ScheduleSyncService.cs) detecta cambios periódicamente (cada `Hangfire:SyncIntervalSeconds`)
4. Las programaciones activas se sincronizan como **recurring jobs** en Hangfire
5. Hangfire ejecuta [`EtlJobService.ExecuteAsync()`](Services/EtlJobService.cs) según la expresión Cron
6. Cada ejecución crea un registro `ETLExecutionHistoryScheduled` con `TriggerType = "PROGRAMADO"`

### Endpoints de Programación

#### GET /api/schedules

Devuelve todas las programaciones ETL.

#### POST /api/schedules

Crea una nueva programación ETL.

**Solicitud:**
```json
{
  "codEnvio": "ENV001",
  "tipoEntidad": "ENTIDAD_1",
  "codigo": "XYZ",
  "cronExpression": "0 2 * * *"
}
```

#### PUT /api/schedules/{id}

Actualiza una programación existente.

#### DELETE /api/schedules/{id}

Elimina una programación y su job asociado en Hangfire.

#### PATCH /api/schedules/{id}/toggle

Activa o desactiva una programación sin eliminarla.

### Tabla `etl_schedule`

| Columna | Tipo | Descripción |
|---------|------|-------------|
| `Id` | GUID | Identificador único |
| `Params` | TEXT | Parámetros serializados para la ejecución |
| `CronExpression` | VARCHAR | Expresión Cron (ej: `0 2 * * *`) |
| `IsActive` | BOOLEAN | Activa/desactivada |
| `CreatedAt` | DATETIME | Fecha de creación |
| `UpdatedAt` | DATETIME | Fecha de última actualización |

---

## SignalR - Notificaciones en Tiempo Real

El servicio utiliza **SignalR** para enviar notificaciones en tiempo real sobre el estado de las ejecuciones ETL programadas. El hub se encuentra en `/etlNotifications`.

### Componentes

| Componente | Descripción |
|---|---|
| [`EtlNotificationHub`](Hubs/EtlNotificationHub.cs) | Hub de SignalR en `/etlNotifications` |
| [`ExecutionNotifier`](Services/ExecutionNotifier.cs) | Servicio singleton que.broadcasta eventos `TaskStarted` y `TaskCompleted` |

### Eventos

| Evento | Descripción |
|---|---|
| `TaskStarted` | Se dispara cuando comienza una ejecución programada |
| `TaskCompleted` | Se dispara cuando finaliza una ejecución programada |

Las interfaces web [`index.html`](wwwroot/index.html) y [`scheduler.html`](wwwroot/scheduler.html) se conectan automáticamente al hub para mostrar actualizaciones en tiempo real.

---

## Flujo de Ejecución

1. El usuario inicia sesión a través de la interfaz HTML
2. Se cargan automáticamente las bases de datos disponibles desde la tabla `base_datos`
3. El usuario selecciona una base de datos del combo box
4. Se muestra una tabla con los resultados de la consulta de seguimiento (`dtx_seguimiento` + `dim_entidad_asfi`), enriquecida con el estado de ejecución desde `hist_etl_execution`
5. Cada fila de la tabla incluye botones "Actualizar" y "Reprocesar"
6. Al presionar uno de estos botones:
   - Se crea un registro `ETLExecutionHistory` con estado `PENDIENTE`
   - Se calculan las fechas `Start`/`End` según si el código es day-based o no:
     - **Day-based:** `Start` = día siguiente a `FechaDatos`, `End` = dos días después
     - **Month-based:** `Start` = primer día del mes siguiente a `FechaDatos`, `End` = último día de ese mes
   - Se encola un job en Hangfire a través de [`EtlJobService`](Services/EtlJobService.cs)
   - Se inicia polling automático del estado
7. Hangfire ejecuta [`EtlJobService.ExecuteAsync()`](Services/EtlJobService.cs) respetando el semáforo `MaxParallelExecutions`
8. `CommandExecutor` ejecuta `Datax.SAFI.Downloader.exe` con los parámetros `-code`, `-start`, `-end`, `-codesend`
9. Al finalizar, el estado se actualiza a `EXITOSO` (si `ExitCode == 0`) o `FALLIDO` (si `ExitCode != 0`)
10. Los campos `Output`, `Error`, `ExitCode` y `CompletedAt` se actualizan en la tabla `hist_etl_execution`

---

## Tabla `hist_etl_execution`

La tabla `hist_etl_execution` se crea automáticamente al iniciar el servicio (si no existe). Contiene:

| Columna | Tipo | Descripción |
|---------|------|-------------|
| `Id` | GUID | Identificador único del registro |
| `CodEnvio` | VARCHAR(100) | Código de envío de la entidad |
| `TipoEntidad` | VARCHAR | Tipo de entidad asociado a la ejecución |
| `FechaDatos` | DATE | Fecha de datos asociada a la ejecución |
| `Codigo` | VARCHAR | Código de base de datos ejecutado |
| `Status` | VARCHAR(20) | Estado actual: `PENDIENTE`, `EN PROCESO`, `EXITOSO`, `FALLIDO` |
| `TriggerType` | VARCHAR(20) | Tipo de disparador: `MANUAL`, `PROGRAMADO`, `REPROCESO` |
| `ExitCode` | INT (nullable) | Código de salida del comando ejecutado |
| `Output` | TEXT (nullable) | Salida estándar del comando |
| `Error` | TEXT (nullable) | Mensaje de error si la ejecución falló |
| `ExecutedAt` | DATETIME (nullable) | Fecha/hora de inicio de ejecución |
| `CompletedAt` | DATETIME (nullable) | Fecha/hora de finalización de ejecución |

---

## Tabla `hist_etl_execution_scheduled`

La tabla `hist_etl_execution_scheduled` se crea automáticamente al iniciar el servicio (si no existe). Almacena el historial de ejecuciones originadas por programaciones Hangfire:

| Columna | Tipo | Descripción |
|---------|------|-------------|
| `Id` | GUID | Identificador único del registro |
| `ScheduleId` | GUID | Identificador de la programación asociada |
| `Params` | TEXT | Parámetros serializados de la ejecución |
| `Status` | VARCHAR(20) | Estado actual: `PENDIENTE`, `EN PROCESO`, `EXITOSO`, `FALLIDO` |
| `ExitCode` | INT (nullable) | Código de salida del comando ejecutado |
| `Output` | TEXT (nullable) | Salida estándar del comando |
| `Error` | TEXT (nullable) | Mensaje de error si la ejecución falló |
| `ExecutedAt` | DATETIME (nullable) | Fecha/hora de inicio de ejecución |
| `CompletedAt` | DATETIME (nullable) | Fecha/hora de finalización de ejecución |
| `CreatedAt` | DATETIME | Fecha/hora de creación del registro |

---

## Ejecución Paralela

El servicio permite ejecutar múltiples comandos en paralelo. El número máximo de ejecuciones simultáneas se configura mediante `MaxParallelExecutions` en el archivo [`appsettings.json`](appsettings.json) (valor por defecto: 1).

Este límite se implementa usando un `SemaphoreSlim` compartido en [`EtlJobService`](Services/EtlJobService.cs) que garantiza que nunca haya más instancias de `CommandExecutor` ejecutándose al mismo tiempo que el número configurado. Tanto las ejecuciones manuales como las programadas comparten el mismo semáforo.

---

## Códigos Day-Based

Los códigos configurados en `QueueConfig:DailyCodes` usan una lógica de cálculo de fechas diferente:

- **Day-based:** Las fechas `Start`/`End` se calculan como los días siguientes a `FechaDatos`
- **Month-based (default):** Las fechas `Start`/`End` se calculan como el rango completo del mes siguiente a `FechaDatos`

El flag `IsDayBased` se incluye en la respuesta del endpoint `/api/etlexecutor/base-datos` para que la interfaz pueda determinar el comportamiento esperado.

---

## Códigos Excluidos

Los códigos configurados en `QueueConfig:ExcludedCodes` son excluidos de la ejecución. Estos códigos no aparecerán en los resultados de consulta ni podrán ejecutarse a través de la interfaz o la API.

---

## Monitoreo y Reinicio del Analyze

El servicio [`ServiceRestartMonitorService`](Services/ServiceRestartMonitorService.cs) se ejecuta en segundo plano y realiza las siguientes tareas periódicamente:

- **Monitorea la tabla `dtx_process`:** Consulta los procesos en estado `RUNNING` que superan el umbral configurado `RunningMinutesThreshold`
- **Reinicia el servicio Windows:** Cuando se detecta un proceso de larga duración, reinicia el servicio Windows configurado en `ServiceRestart:WindowsServiceName`
- **Actualiza el estado del proceso:** Cambia el estado del proceso en la tabla `dtx_process` después del reinicio

Esta funcionalidad es exclusiva de Windows. En otros sistemas operativos, el servicio registra una advertencia y omite las verificaciones.

---

## Limpieza Automática de Tokens

El servicio [`RefreshTokenCleanupService`](Services/RefreshTokenCleanupService.cs) se ejecuta en segundo plano y realiza las siguientes tareas periódicamente:

- **Revoca tokens expirados:** Los refresh tokens cuya fecha de expiración ha pasado se marcan como revocados
- **Limpia registros de auditoría antiguos:** Los registros de `AuthAuditLog` más antiguos que el período de retención configurado se eliminan

La frecuencia de limpieza y el período de retención se configuran en la sección `RefreshTokenCleanup` del archivo [`appsettings.json`](appsettings.json).

---

## Bases de Datos

### Base de Datos Legacy (AuthDatabase)

Almacena usuarios y roles del sistema legacy. Soporta los siguientes proveedores:

| Proveedor | Paquete | Configuración |
|-----------|---------|---------------|
| SQLite | `Microsoft.EntityFrameworkCore.Sqlite` | `Authentication.Provider = "sqlite"` |
| PostgreSQL | `Npgsql.EntityFrameworkCore.PostgreSQL` | `Authentication.Provider = "postgres"` |
| SQL Server | `Microsoft.EntityFrameworkCore.SqlServer` | `Authentication.Provider = "sqlserver"` |

### Base de Datos SQLite (RefreshTokenDatabase)

Almacena tokens de refresco, registros de auditoría y programaciones ETL. Se crea automáticamente en la raíz del proyecto si no existe. [`RefreshTokenDbContext`](Data/RefreshTokenDbContext.cs) y [`ScheduleDbContext`](Data/ScheduleDbContext.cs) comparten el mismo archivo SQLite.

| Tabla | Descripción | DbContext |
|-------|-------------|-----------|
| `RefreshTokens` | Tokens de refresco y estado de revocación | `RefreshTokenDbContext` |
| `AuthAuditLogs` | Registros de auditoría de eventos de autenticación | `RefreshTokenDbContext` |
| `etl_schedule` | Programaciones ETL recurrentes | `ScheduleDbContext` |

### Base de Datos de Servicio (ServiceDatabase)

Almacena las tablas `hist_etl_execution` y `hist_etl_execution_scheduled` (creadas automáticamente) y proporciona acceso a las tablas existentes `base_datos`, `dim_entidad_asfi`, `dtx_seguimiento` y `dtx_process`. Soporta los siguientes proveedores:

| Proveedor | Paquete | Configuración |
|-----------|---------|---------------|
| PostgreSQL | `Npgsql.EntityFrameworkCore.PostgreSQL` | `ServiceDb.Provider = "postgres"` |
| SQL Server | `Microsoft.EntityFrameworkCore.SqlServer` | `ServiceDb.Provider = "sqlserver"` |
| SQLite | `Microsoft.EntityFrameworkCore.Sqlite` | `ServiceDb.Provider = "sqlite"` |

---

## Servicios de Fondo

El servicio ejecuta los siguientes background services concurrentemente:

| Servicio | Descripción |
|----------|-------------|
| [`RefreshTokenCleanupService`](Services/RefreshTokenCleanupService.cs) | Limpia tokens expirados y logs de auditoría antiguos |
| [`ScheduleSyncService`](Services/ScheduleSyncService.cs) | Sincroniza programaciones DB con Hangfire recurring jobs |
| [`ServiceRestartMonitorService`](Services/ServiceRestartMonitorService.cs) | Monitorea procesos largos y reinicia el servicio Windows |

---

## Pruebas

El proyecto incluye dos proyectos de pruebas:

### Unit Tests

Ubicados en `ServicioRESTEjecucionComandos.UnitTests/`, utilizan **xUnit** con **Moq** para mocking y **FluentAssertions** para assertions.

Cubren:
- [`CommandExecutorTests`](ServicioRESTEjecucionComandos.UnitTests/Services/CommandExecutorTests.cs)
- [`EtlJobServiceTests`](ServicioRESTEjecucionComandos.UnitTests/Services/EtlJobServiceTests.cs)
- [`ExecutionNotifierTests`](ServicioRESTEjecucionComandos.UnitTests/Services/ExecutionNotifierTests.cs)
- [`JwtServiceTests`](ServicioRESTEjecucionComandos.UnitTests/Services/JwtServiceTests.cs)
- [`LegacyPasswordValidatorTests`](ServicioRESTEjecucionComandos.UnitTests/Services/LegacyPasswordValidatorTests.cs)
- [`RefreshTokenServiceTests`](ServicioRESTEjecucionComandos.UnitTests/Services/RefreshTokenServiceTests.cs)
- [`RefreshTokenCleanupServiceTests`](ServicioRESTEjecucionComandos.UnitTests/Services/RefreshTokenCleanupServiceTests.cs)
- [`ETLExecutionHistoryRepositoryTests`](ServicioRESTEjecucionComandos.UnitTests/Repositories/ETLExecutionHistoryRepositoryTests.cs)
- [`EtlScheduleRepositoryTests`](ServicioRESTEjecucionComandos.UnitTests/Repositories/EtlScheduleRepositoryTests.cs)

### Integration Tests

Ubicados en `ServicioRESTEjecucionComandos.IntegrationTests/`, utilizan **xUnit** con **Testcontainers** para PostgreSQL.

Cubren:
- [`AuthControllerIntegrationTests`](ServicioRESTEjecucionComandos.IntegrationTests/Controllers/AuthControllerIntegrationTests.cs)
- [`EtlExecutorControllerIntegrationTests`](ServicioRESTEjecucionComandos.IntegrationTests/Controllers/EtlExecutorControllerIntegrationTests.cs)
- [`SchedulesControllerIntegrationTests`](ServicioRESTEjecucionComandos.IntegrationTests/Controllers/SchedulesControllerIntegrationTests.cs)
- [`RateLimitingIntegrationTests`](ServicioRESTEjecucionComandos.IntegrationTests/Controllers/RateLimitingIntegrationTests.cs)
- [`EtlNotificationHubIntegrationTests`](ServicioRESTEjecucionComandos.IntegrationTests/Hubs/EtlNotificationHubIntegrationTests.cs)

### Ejecutar pruebas

```bash
dotnet test
```

---

## Notas Importantes

- Asegúrese de que la ruta `ExePath` apunte a una ubicación válida donde exista `Datax.SAFI.Downloader.exe`
- La clave secreta JWT (`Jwt:SecretKey`) debe tener al menos 32 caracteres
- La base de datos legacy debe contener las tablas `user` y `userrole` con la estructura esperada
- La base de datos de servicio debe contener la tabla `base_datos` con columnas `codigo` y `nombre`
- Las tablas `hist_etl_execution` y `hist_etl_execution_scheduled` se crean automáticamente al iniciar el servicio
- El servicio no incluye Swagger; solo la interfaz HTML está disponible
- Los resultados de ejecución se almacenan en las tablas `hist_etl_execution` / `hist_etl_execution_scheduled` (no se escriben archivos en disco)
- El estado del usuario se valida con la comparación `Userstate == "Activo"`
- Cada evento de autenticación se registra en `ILogger` (→ Serilog → consola + archivo) y en el repositorio `AuthAuditLogRepository` (→ SQLite)
- Los logs de Serilog se almacenan en la carpeta `Logs/` con rotación diaria
- El ambiente de ejecución se controla con la variable `ASPNETCORE_ENVIRONMENT`
- En Production, la base de datos SQLite y los logs se almacenan en `%ProgramData%\ServicioRESTEjecucionComandos\`
- Hangfire usa almacenamiento en memoria; los jobs programados se pierden al reiniciar (se re-sincronizan mediante `ScheduleSyncService`)
- No hay endpoint de dashboard de Hangfire habilitado
- **Protección contra condiciones de carrera:** La tabla `hist_etl_execution` tiene un índice único parcial (`IX_hist_etl_execution_unique_active`) sobre `(CodEnvio, Codigo)` para registros con `Status IN ('PENDIENTE', 'EN PROCESO')`, lo que previene ejecuciones duplicadas simultáneas a nivel de base de datos (PostgreSQL y SQL Server)
- `AuthDbContext` es de solo lectura (base de datos legacy); no ejecutar migraciones EF contra él
- `EtlJobService` y `CommandExecutor` son singletons que usan `IServiceScopeFactory` para acceso scoped a la base de datos
- Los modelos `BaseDatos` y `DtxProcess` se ignoran con `modelBuilder.Ignore<T>()` y se consultan mediante SQL raw
- Todos los comentarios y código fuente están en inglés, excepto este archivo de documentación

---

## Docker

El servicio puede ejecutarse en contenedores Docker Windows Server Core:

### Construir la imagen

```bash
docker build -t servicio-rest-ejecucion .
```

### Ejecutar con docker-compose

El archivo [`docker-compose.yml`](docker-compose.yml) incluye:

- **servicio-rest**: El servicio principal con health check integrado
- **uptime-kuma**: Monitor de disponibilidad en `http://localhost:3001`

```bash
docker-compose up -d
```

### Health Check

El Dockerfile incluye un health check nativo que verifica `/api/health` cada 30 segundos. Adicionalmente, [`Monitor-Health.ps1`](Monitor-Health.ps1) permite verificar la salud del servicio de forma remota:

```powershell
.\Monitor-Health.ps1 -Url http://localhost:5000/api/health
```

### Variables de entorno Docker

| Variable | Descripción |
|----------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Ambiente de ejecución (`Production`, `Development`) |
| `ASPNETCORE_URLS` | URLs de escucha (por defecto `http://0.0.0.0:5000`) |
| `ConnectionStrings__AuthDatabase` | Connection string para la base de datos legacy |
| `ConnectionStrings__RefreshTokenDatabase` | Connection string para SQLite (tokens y auditoría) |
| `ConnectionStrings__ServiceDatabase` | Connection string para la base de datos de servicio |
| `DataxConfig__ExePath` | Ruta al ejecutable dentro del contenedor |

---

## Constantes del Sistema

El proyecto centraliza valores mágicos en la carpeta [`Constants/`](Constants/) para mantener consistencia y facilitar el mantenimiento:

### [`EtlStatus`](Constants/EtlStatus.cs)

Estados posibles de una ejecución ETL:

| Constante | Valor | Descripción |
|-----------|-------|-------------|
| `Pending` | `PENDIENTE` | Esperando ejecución |
| `InProgress` | `EN PROCESO` | En ejecución activa |
| `Successful` | `EXITOSO` | Completada con éxito |
| `Failed` | `FALLIDO` | Falló durante la ejecución |

### [`DtxProcessStatus`](Constants/DtxProcessStatus.cs)

Estados de los procesos DTX monitoreados:

| Constante | Valor | Descripción |
|-----------|-------|-------------|
| `Running` | `Running` | Proceso en ejecución |
| `Stopped` | `Stopped` | Proceso detenido |
| `Error` | `Error` | Proceso en estado de error |

### [`TriggerType`](Constants/TriggerType.cs)

Tipos de disparador para las ejecuciones:

| Constante | Valor | Descripción |
|-----------|-------|-------------|
| `Manual` | `MANUAL` | Ejecución iniciada manualmente |
| `Reprocess` | `REPROCESO` | Re-procesamiento de ejecución previa |

### [`UserState`](Constants/UserState.cs)

| Constante | Valor | Descripción |
|-----------|-------|-------------|
| `Active` | `Activo` | Usuario activo y habilitado para login |

### [`Role`](Constants/Role.cs)

| Constante | Valor | Descripción |
|-----------|-------|-------------|
| `Administrator` | `Administrador` | Rol con acceso completo al sistema |

### [`Policy`](Constants/Policy.cs)

| Constante | Valor | Descripción |
|-----------|-------|-------------|
| `AdminOnly` | `AdminOnly` | Política de autorización para administradores |
| `LoginPolicy` | `LoginPolicy` | Política de rate limiting para el endpoint de login |

### [`AuditEventType`](Constants/AuditEventType.cs)

Tipos de eventos registrados en la auditoría de autenticación:

| Constante | Valor | Descripción |
|-----------|-------|-------------|
| `LoginSuccess` | `LoginSuccess` | Inicio de sesión exitoso |
| `LoginFailed` | `LoginFailed` | Intento de inicio de sesión fallido |
| `RefreshSuccess` | `RefreshSuccess` | Refresco de token exitoso |
| `RefreshFailed` | `RefreshFailed` | Intento de refresco fallido |
| `Logout` | `Logout` | Cierre de sesión |
| `TokenRotated` | `TokenRotated` | Rotación de refresh token |
| `TokenRevoked` | `TokenRevoked` | Revocación de refresh token |
| `BulkTokenRevoked` | `BulkTokenRevoked` | Revocación masiva de tokens |

### [`ControllerAction`](Constants/ControllerAction.cs)

| Constante | Valor | Descripción |
|-----------|-------|-------------|
| `Actualizar` | `ACTUALIZAR` | Acción para crear/actualizar ejecución ETL |
| `Reprocesar` | `REPROCESAR` | Acción para re-procesar ejecución existente |

### [`SignalREvent`](Constants/SignalREvent.cs)

| Constante | Valor | Descripción |
|-----------|-------|-------------|
| `TaskStarted` | `TaskStarted` | Evento SignalR al iniciar tarea programada |
| `TaskCompleted` | `TaskCompleted` | Evento SignalR al completar tarea programada |

### [`Schedule`](Constants/Schedule.cs)

| Constante | Valor | Descripción |
|-----------|-------|-------------|
| `JobIdPrefix` | `etl-schedule-` | Prefijo para IDs de jobs recurrentes en Hangfire |

### [`DbProvider`](Constants/DbProvider.cs)

| Constante | Valor | Descripción |
|-----------|-------|-------------|
| `Sqlite` | `sqlite` | Proveedor SQLite |
| `Postgres` | `postgres` | Proveedor PostgreSQL |
| `Sqlserver` | `sqlserver` | Proveedor SQL Server |

### [`DtxProcess`](Constants/DtxProcess.cs)

| Constante | Valor | Descripción |
|-----------|-------|-------------|
| `ProcessName` | `Datax.SAFI.Downloader` | Nombre del proceso DTX monitoreado |

---