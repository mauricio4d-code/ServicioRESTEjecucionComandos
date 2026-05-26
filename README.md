# ServicioRESTEjecucionComandos

Servicio REST desarrollado en C# con ASP.NET Core 8 que proporciona una interfaz web autenticada para ejecutar comandos de forma asíncrona mediante una cola de ejecución con procesamiento paralelo configurable.

El sistema incluye autenticación JWT con tokens de acceso y refrescado (refresh tokens), validación contra una base de datos legacy, registro de auditoría, limpieza automática de tokens expirados, y gestión de ejecuciones a través de la tabla `ServiceItem` en una base de datos de servicio (PostgreSQL o SQL Server).

---

## Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (o superior)
- Windows (para ejecución de aplicaciones de consola nativas)
- Base de datos legacy (SQLite, PostgreSQL o SQL Server) configurada con usuarios y roles
- Base de datos de servicio (PostgreSQL o SQL Server) para la tabla `ServiceItem` y consultas a `base_datos`

---

## Estructura del Proyecto

```
ServicioRESTEjecucionComandos/
├── Program.cs                          # Punto de entrada y configuración de dependencias
├── ServicioRESTEjecucionComandos.csproj # Archivo de proyecto y paquetes NuGet
├── appsettings.json                    # Configuración del servicio
├── Controllers/
│   ├── AuthController.cs               # Endpoints de autenticación (login, refresh, logout)
│   └── CommandController.cs            # Endpoints REST para comandos, base-datos, y consultas
├── Data/
│   ├── AuthDbContext.cs                # Contexto EF Core para BD legacy (usuarios/roles)
│   ├── RefreshTokenDbContext.cs        # Contexto EF Core para BD SQLite (tokens/auditoría)
│   └── ServiceDbContext.cs             # Contexto EF Core para BD de servicio (ServiceItem, BaseDatos)
├── DTOs/
│   ├── LoginRequest.cs                 # DTO para solicitud de inicio de sesión
│   ├── LoginResponse.cs                # DTO para respuesta con tokens JWT
│   ├── RefreshRequest.cs               # DTO para solicitud de refresco de token
│   ├── RefreshResponse.cs              # DTO para respuesta con nuevos tokens
│   └── ErrorResponse.cs                # DTO para respuestas de error
├── Interfaces/
│   ├── IPasswordValidator.cs           # Interfaz para validación de contraseñas
│   └── LegacyPasswordValidator.cs      # Implementación para BD legacy
├── Models/
│   ├── User.cs                         # Modelo de usuario legacy
│   ├── UserRole.cs                     # Modelo de rol de usuario
│   ├── RefreshToken.cs                 # Modelo de token de refresco
│   ├── AuthAuditLog.cs                 # Modelo de registro de auditoría
│   ├── ExecutionQueueItem.cs           # Modelo para items de la cola
│   ├── ServiceItem.cs                  # Modelo para seguimiento de ejecuciones en BD
│   └── BaseDatos.cs                    # Modelo para tabla base_datos (lookup)
├── Repositories/
│   ├── RefreshTokenRepository.cs       # Repositorio para persistencia de refresh tokens
│   ├── AuthAuditLogRepository.cs       # Repositorio para registros de auditoría
│   └── ServiceItemRepository.cs        # Repositorio para CRUD de ServiceItem
├── Services/
│   ├── AuthService.cs                  # Orquestador de flujos de autenticación
│   ├── JwtService.cs                   # Generación de tokens JWT
│   ├── RefreshTokenService.cs          # Generación y rotación de refresh tokens
│   ├── RefreshTokenCleanupService.cs   # Limpieza automática de tokens expirados
│   ├── CommandExecutor.cs              # Ejecuta la aplicación de consola
│   ├── ExecutionQueue.cs               # Cola thread-safe para items
│   └── QueuedExecutionService.cs       # Servicio de fondo que procesa la cola
└── wwwroot/
    ├── index.html                      # Interfaz web con selector de BD y tabla de resultados
    ├── login.html                      # Interfaz de inicio de sesión
    └── auth.js                         # Cliente JavaScript para autenticación
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

### ServiceDb

Configura la base de datos de servicio donde se almacena la tabla `ServiceItem`:

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
| `ServiceDatabase` | Cadena de conexión a la BD de servicio (ServiceItem, base_datos) |

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

Todos los endpoints de `/api/command/*` están protegidos con el atributo `[Authorize]`. Las solicitudes sin un token JWT válido recibirán una respuesta `401 Unauthorized`.

---

## Endpoints de Ejecución de Comandos

### GET /api/command/base-datos

Devuelve todos los registros de la tabla `base_datos` para poblar el selector de base de datos.

**Respuesta:**
```json
[
  { "codigo": "XYZ", "nombre": "Base de Datos XYZ" },
  { "codigo": "ABC", "nombre": "Base de Datos ABC" }
]
```

### GET /api/command/query-results?codigo=XYZ

Ejecuta la consulta de seguimiento para el código de base de datos especificado.

**Query ejecutado:**
```sql
SELECT DISTINCT ON (e.cod_envio)
       s.tipoentidad,
       e.cod_envio,
       s.fechadatos
FROM dim_entidad_asfi e
JOIN dtx_seguimiento s
   ON e.tipo_entidad_asfi_codigo = s.tipoentidad
WHERE e.cod_envio IS NOT NULL
  AND e.cod_envio <> ''
  AND s.codigo = XYZ
ORDER BY e.cod_envio, s.fechadatos DESC;
```

**Respuesta:**
```json
[
  {
    "tipoentidad": "ENTIDAD_1",
    "cod_envio": "ENV001",
    "fechadatos": "2026-05-15T00:00:00"
  }
]
```

### POST /api/command/execute

Encola una nueva ejecución de comando vinculada a un registro `ServiceItem`.

**Solicitud:**
```json
{
  "action": "Actualizar",
  "codigo": "ENV001"
}
```

**Respuesta:**
```json
{
  "queueItemId": "123e4567-e89b-12d3-a456-426614174000",
  "serviceItemId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "PENDING",
  "message": "Command enqueued successfully. Action: Actualizar"
}
```

### GET /api/command/status/{itemId}

Devuelve el estado actual de un `ServiceItem` por su `ItemId`. Usado para polling del progreso de ejecución.

**Respuesta:**
```json
{
  "itemId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "SUCCESS",
  "exitCode": 0,
  "output": "Command output...",
  "error": "",
  "executedAt": "2026-05-20T10:00:00Z",
  "completedAt": "2026-05-20T10:05:00Z"
}
```

### GET /api/command/service-items

Devuelve todos los registros de `ServiceItem` ordenados por creación descendente.

---

## Flujo de Ejecución (Actualizado)

1. El usuario inicia sesión a través de la interfaz HTML
2. Se cargan automáticamente las bases de datos disponibles desde la tabla `base_datos`
3. El usuario selecciona una base de datos del combo box
4. Se muestra una tabla con los resultados de la consulta de seguimiento (`dtx_seguimiento` + `dim_entidad_asfi`)
5. Cada fila de la tabla incluye botones "Actualizar" y "Reprocesar"
6. Al presionar uno de estos botones:
   - Se crea un registro `ServiceItem` con estado `PENDING`
   - Se encola un `ExecutionQueueItem` vinculado al `ServiceItemId`
   - Se inicia polling automático del estado
7. `QueuedExecutionService` descola el item y actualiza el estado a `RUNNING`
8. `CommandExecutor` ejecuta `Datax.SAFI.Downloader.exe` con los parámetros configurados (usando el `codigo` dinámico como `Codesend`)
9. Al finalizar, el estado se actualiza a `SUCCESS` (si `ExitCode == 0`) o `FAILED` (si `ExitCode != 0`)
10. Los campos `Output`, `Error`, `ExitCode` y `CompletedAt` se actualizan en la tabla `ServiceItem`

---

## Tabla ServiceItem

La tabla `ServiceItem` se crea automáticamente al iniciar el servicio (si no existe). Contiene:

| Columna | Tipo | Descripción |
|---------|------|-------------|
| `ItemId` | GUID | Identificador único del registro |
| `Status` | VARCHAR | Estado actual: `PENDING`, `RUNNING`, `SUCCESS`, `FAILED` |
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

## Limpieza Automática de Tokens

El servicio `RefreshTokenCleanupService` se ejecuta en segundo plano y realiza las siguientes tareas periódicamente:

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

Almacena la tabla `ServiceItem` (creada automáticamente) y proporciona acceso a las tablas existentes `base_datos`, `dim_entidad_asfi` y `dtx_seguimiento`. Soporta los siguientes proveedores:

| Proveedor | Paquete | Configuración |
|-----------|---------|---------------|
| PostgreSQL | `Npgsql.EntityFrameworkCore.PostgreSQL` | `ServiceDb.Provider = "postgres"` |
| SQL Server | `Microsoft.EntityFrameworkCore.SqlServer` | `ServiceDb.Provider = "sqlserver"` |

---

## Cambios Recientes

### Eliminación de ResultConfig.OutputPath

La configuración `ResultConfig.OutputPath` ha sido eliminada. Los resultados de ejecución ya no se guardan como archivos JSON en disco. En su lugar, se almacenan en la tabla `ServiceItem` de la base de datos de servicio.

### Nueva interfaz de usuario

La interfaz principal (`index.html`) ahora incluye:

- **Selector de base de datos:** Combo box que carga las bases de datos disponibles desde la tabla `base_datos`
- **Tabla de resultados:** Muestra los resultados de la consulta de seguimiento para la base de datos seleccionada
- **Botones de acción:** "Actualizar" y "Reprocesar" en cada fila, reemplazando el botón "Ejecutar Comando" anterior
- **Indicador de estado:** Badge de estado en cada fila que se actualiza automáticamente mediante polling
- **Notificaciones toast:** Mensajes de confirmación/error para las acciones del usuario

---

## Notas Importantes

- Asegúrese de que la ruta `ExePath` apunte a una ubicación válida donde exista `Datax.SAFI.Downloader.exe`
- La clave secreta JWT (`Jwt:SecretKey`) debe tener al menos 32 caracteres
- La base de datos legacy debe contener las tablas `user` y `userrole` con la estructura esperada
- La base de datos de servicio debe contener la tabla `base_datos` con columnas `codigo` y `nombre`
- La tabla `ServiceItem` se crea automáticamente al iniciar el servicio
- El servicio no incluye Swagger; solo la interfaz HTML está disponible
- Todos los comentarios y código fuente están en inglés, excepto este archivo de documentación
