# ServicioRESTEjecucionComandos

Servicio REST desarrollado en C# con ASP.NET Core 8 que proporciona una interfaz web autenticada para ejecutar comandos de forma asíncrona mediante una cola de ejecución con procesamiento paralelo configurable.

El sistema incluye autenticación JWT con tokens de acceso y refrescado (refresh tokens), validación contra una base de datos legacy, registro de auditoría, limpieza automática de tokens expirados, y gestión de ejecuciones a través de la tabla `hist_etl_execution` en una base de datos de servicio (PostgreSQL o SQL Server).

---

## Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (o superior)
- Windows (para ejecución de aplicaciones de consola nativas)
- Base de datos legacy (SQLite, PostgreSQL o SQL Server) configurada con usuarios y roles
- Base de datos de servicio (PostgreSQL o SQL Server) para la tabla `hist_etl_execution` y consultas a `base_datos`

---

## Estructura del Proyecto

```
ServicioRESTEjecucionComandos/
├── Program.cs                          # Punto de entrada y configuración de dependencias
├── ServicioRESTEjecucionComandos.csproj # Archivo de proyecto y paquetes NuGet
├── appsettings.json                    # Configuración del servicio
├── Controllers/
│   ├── AuthController.cs               # Endpoints de autenticación (login, refresh, logout)
│   └── ETLExecutorController.cs        # Endpoints REST para ETL, base-datos, y consultas
├── Data/
│   ├── AuthDbContext.cs                # Contexto EF Core para BD legacy (usuarios/roles)
│   ├── RefreshTokenDbContext.cs        # Contexto EF Core para BD SQLite (tokens/auditoría)
│   └── ServiceDbContext.cs             # Contexto EF Core para BD de servicio (hist_etl_execution)
├── DTOs/
│   ├── BaseDatosResponse.cs            # DTO para respuesta de lookup de base_datos (incluye IsDayBased)
│   ├── ErrorResponse.cs                # DTO para respuestas de error
│   ├── LoginRequest.cs                 # DTO para solicitud de inicio de sesión
│   ├── LoginResponse.cs                # DTO para respuesta con tokens JWT
│   ├── QueryResult.cs                  # DTO para resultados de consulta de seguimiento
│   ├── RefreshRequest.cs               # DTO para solicitud de refresco de token
│   └── RefreshResponse.cs              # DTO para respuesta con nuevos tokens
├── Interfaces/
│   ├── IPasswordValidator.cs           # Interfaz para validación de contraseñas
│   └── LegacyPasswordValidator.cs      # Implementación para BD legacy
├── Models/
│   ├── AuthAuditLog.cs                 # Modelo de registro de auditoría
│   ├── BaseDatos.cs                    # Modelo para tabla base_datos (lookup)
│   ├── ETLExecutionHistory.cs          # Modelo para historial de ejecuciones ETL
│   ├── ExecutionQueueItem.cs           # Modelo para items de la cola
│   ├── RefreshToken.cs                 # Modelo de token de refresco
│   ├── User.cs                         # Modelo de usuario legacy
│   └── UserRole.cs                     # Modelo de rol de usuario
├── Repositories/
│   ├── AuthAuditLogRepository.cs       # Repositorio para registros de auditoría
│   ├── ETLExecutionHistoryRepository.cs # Repositorio para CRUD de ETLExecutionHistory
│   └── RefreshTokenRepository.cs       # Repositorio para persistencia de refresh tokens
├── Services/
│   ├── AuthService.cs                  # Orquestador de flujos de autenticación
│   ├── CommandExecutor.cs              # Ejecuta la aplicación de consola
│   ├── ExecutionQueue.cs               # Cola thread-safe para items
│   ├── JwtService.cs                   # Generación de tokens JWT
│   ├── QueuedExecutionService.cs       # Servicio de fondo que procesa la cola
│   ├── RefreshTokenCleanupService.cs   # Limpieza automática de tokens expirados
│   └── RefreshTokenService.cs          # Generación y rotación de refresh tokens
└── wwwroot/
    ├── auth.js                         # Cliente JavaScript para autenticación
    ├── index.html                      # Interfaz web con selector de BD y tabla de resultados
    └── login.html                      # Interfaz de inicio de sesión
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
| `MaxParallelExecutions` | Máximo de comandos ejecutándose simultáneamente | `1` |
| `DailyCodes` | Lista de códigos que usan lógica de fechas basada en días (no meses) | `[]` |

### ServiceDb

Configura la base de datos de servicio donde se almacena la tabla `hist_etl_execution`:

| Clave | Descripción | Valores Válidos |
|-------|-------------|-----------------|
| `Provider` | Proveedor de base de datos para ServiceDb | `postgres`, `sqlserver` |

### Authentication

| Clave | Descripción | Valores Válidos |
|-------|-------------|-----------------|
| `Provider` | Proveedor de base de datos legacy | `sqlite`, `postgres`, `sqlserver` |

### ConnectionStrings

| Clave | Descripción |
|-------|-------------|
| `AuthDatabase` | Cadena de conexión a la BD legacy (usuarios/roles) |
| `RefreshTokenDatabase` | Cadena de conexión a la BD SQLite (tokens/auditoría) |
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

---

## Cómo Ejecutar el Servicio

### Opción 1: Usando dotnet CLI

```bash
cd ServicioRESTEjecucionComandos
dotnet run
```

### Opción 2: Usando Visual Studio

1. Abra el archivo `ServicioRESTEjecucionComandos.sln` en Visual Studio
2. Presione F5 o haga clic en "Iniciar depuración"

### Acceso al Servicio

Una vez iniciado el servicio, acceda a:

```
http://localhost:5000
```

o

```
http://localhost:5001
```

(El puerto exacto se mostrará en la consola al iniciar)

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

Todos los endpoints de `/api/etlexecutor/*` están protegidos con el atributo `[Authorize]`. Las solicitudes sin un token JWT válido recibirán una respuesta `401 Unauthorized`.

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

**Consulta ejecutada:**
```sql
WITH latest_exec AS (
    SELECT DISTINCT ON ("CodEnvio", "TipoEntidad", "FechaDatos")
        "CodEnvio",
        "TipoEntidad",
        "FechaDatos",
        "Status" AS estado_ejecucion,
        "TriggerType" AS trigger_type,
        "CompletedAt" AS ultima_fecha_ejecucion,
        "Output" AS "output",
        "Error" AS "error"
    FROM hist_etl_execution
    ORDER BY "CodEnvio", "TipoEntidad", "FechaDatos", "CompletedAt" DESC NULLS LAST
)
SELECT DISTINCT ON (e.cod_envio)
    s.tipoentidad AS "TipoEntidad",
    e.cod_envio AS "CodEnvio",
    s.fechadatos AS "FechaDatos",
    le.estado_ejecucion AS "EstadoEjecucion",
    le.trigger_type AS "TriggerType",
    le.ultima_fecha_ejecucion AS "UltimaFechaEjecucion",
    le."output" AS "Output",
    le."error" AS "Error"
FROM dim_entidad_asfi e
JOIN dtx_seguimiento s
    ON s.cod_envio = e.cod_envio
LEFT JOIN latest_exec le
    ON le."CodEnvio" = e.cod_envio
    AND le."TipoEntidad" = s.tipoentidad
    AND le."FechaDatos" = s.fechadatos
WHERE e.cod_envio IS NOT NULL
    AND e.cod_envio <> ''
    AND s.codigo = XYZ
ORDER BY e.cod_envio, s.fechadatos DESC
```

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
   - Se encola un `ExecutionQueueItem` vinculado al `HistoryId`
   - Se inicia polling automático del estado
7. `QueuedExecutionService` descola el item y actualiza el estado a `EN PROCESO`
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

## Ejecución Paralela

El servicio permite ejecutar múltiples comandos en paralelo. El número máximo de ejecuciones simultáneas se configura mediante `MaxParallelExecutions` en el archivo [`appsettings.json`](appsettings.json) (valor por defecto: 1).

Este límite se implementa usando un `SemaphoreSlim` que garantiza que nunca haya más instancias de `CommandExecutor` ejecutándose al mismo tiempo que el número configurado.

---

## Códigos Day-Based

Los códigos configurados en `QueueConfig:DailyCodes` usan una lógica de cálculo de fechas diferente:

- **Day-based:** Las fechas `Start`/`End` se calculan como los días siguientes a `FechaDatos`
- **Month-based (default):** Las fechas `Start`/`End` se calculan como el rango completo del mes siguiente a `FechaDatos`

El flag `IsDayBased` se incluye en la respuesta del endpoint `/api/etlexecutor/base-datos` para que la interfaz pueda determinar el comportamiento esperado.

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

Almacena tokens de refresco y registros de auditoría. Se crea automáticamente en la raíz del proyecto si no existe.

### Base de Datos de Servicio (ServiceDatabase)

Almacena la tabla `hist_etl_execution` (creada automáticamente) y proporciona acceso a las tablas existentes `base_datos`, `dim_entidad_asfi` y `dtx_seguimiento`. Soporta los siguientes proveedores:

| Proveedor | Paquete | Configuración |
|-----------|---------|---------------|
| PostgreSQL | `Npgsql.EntityFrameworkCore.PostgreSQL` | `ServiceDb.Provider = "postgres"` |
| SQL Server | `Microsoft.EntityFrameworkCore.SqlServer` | `ServiceDb.Provider = "sqlserver"` |

---

## Notas Importantes

- Asegúrese de que la ruta `ExePath` apunte a una ubicación válida donde exista `Datax.SAFI.Downloader.exe`
- La clave secreta JWT (`Jwt:SecretKey`) debe tener al menos 32 caracteres
- La base de datos legacy debe contener las tablas `user` y `userrole` con la estructura esperada
- La base de datos de servicio debe contener la tabla `base_datos` con columnas `codigo` y `nombre`
- La tabla `hist_etl_execution` se crea automáticamente al iniciar el servicio
- El servicio no incluye Swagger; solo la interfaz HTML está disponible
- Los resultados de ejecución se almacenan en la tabla `hist_etl_execution` (no se escriben archivos en disco)
- El estado del usuario se valida con la comparación `Userstate == "Activo"`
- Cada evento de autenticación se registra en `ILogger` y en el repositorio `AuthAuditLogRepository`
- Todos los comentarios y código fuente están en inglés, excepto este archivo de documentación
