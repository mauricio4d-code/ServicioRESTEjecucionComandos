# ServicioRESTEjecucionComandos

Servicio REST desarrollado en C# con ASP.NET Core 8 que proporciona una interfaz web autenticada para ejecutar comandos de forma asíncrona mediante una cola de ejecución con procesamiento paralelo configurable.

El sistema incluye autenticación JWT con tokens de acceso y refrescado (refresh tokens), validación contra una base de datos legacy, registro de auditoría y limpieza automática de tokens expirados.

---

## Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (o superior)
- Windows (para ejecución de aplicaciones de consola nativas)
- Base de datos legacy (SQLite, PostgreSQL o SQL Server) configurada con usuarios y roles

---

## Estructura del Proyecto

```
ServicioRESTEjecucionComandos/
├── Program.cs                          # Punto de entrada y configuración de dependencias
├── ServicioRESTEjecucionComandos.csproj # Archivo de proyecto y paquetes NuGet
├── appsettings.json                    # Configuración del servicio
├── Controllers/
│   ├── AuthController.cs               # Endpoints de autenticación (login, refresh, logout)
│   └── CommandController.cs            # Endpoint REST para ejecutar comandos (protegido)
├── Data/
│   ├── AuthDbContext.cs                # Contexto EF Core para BD legacy (usuarios/roles)
│   └── RefreshTokenDbContext.cs        # Contexto EF Core para BD SQLite (tokens/auditoría)
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
│   └── ExecutionQueueItem.cs           # Modelo para items de la cola
├── Repositories/
│   ├── RefreshTokenRepository.cs       # Repositorio para persistencia de refresh tokens
│   └── AuthAuditLogRepository.cs       # Repositorio para registros de auditoría
├── Services/
│   ├── AuthService.cs                  # Orquestador de flujos de autenticación
│   ├── JwtService.cs                   # Generación de tokens JWT
│   ├── RefreshTokenService.cs          # Generación y rotación de refresh tokens
│   ├── RefreshTokenCleanupService.cs   # Limpieza automática de tokens expirados
│   ├── CommandExecutor.cs              # Ejecuta la aplicación de consola
│   ├── ExecutionQueue.cs               # Cola thread-safe para items
│   └── QueuedExecutionService.cs       # Servicio de fondo que procesa la cola
└── wwwroot/
    ├── index.html                      # Interfaz web para ejecutar comandos
    ├── login.html                      # Interfaz de inicio de sesión
    └── auth.js                         # Cliente JavaScript para autenticación
```

---

## Configuración

El archivo [`appsettings.json`](appsettings.json) contiene todas las configuraciones del servicio:

### DataxConfig

Configura los parámetros de la aplicación de consola que se ejecutará:

| Clave | Descripción | Ejemplo |
|-------|-------------|---------|
| `ExePath` | Ruta completa a `Datax.SAFI.Downloader.exe` | `C:\ruta\a\la\app.exe` |
| `Code` | Parámetro `-code` para el comando | `S_BOEIF_99_00001` |
| `Start` | Parámetro `-start` (fecha inicio) | `2026-02-28` |
| `End` | Parámetro `-end` (fecha fin) | `2026-02-28` |
| `Codesend` | Parámetro `-codesend` | `IBBIS` |

### QueueConfig

Configura el comportamiento de la cola de ejecución:

| Clave | Descripción | Valor por Defecto |
|-------|-------------|-------------------|
| `WaitSeconds` | Segundos de espera cuando la cola está vacía | `10` |
| `MaxParallelExecutions` | Máximo de comandos ejecutándose simultáneamente | `1` |

### ResultConfig

| Clave | Descripción | Valor por Defecto |
|-------|-------------|-------------------|
| `OutputPath` | Ruta donde se guardan los archivos de resultado | `D:\` |

### Authentication

| Clave | Descripción | Valores Válidos |
|-------|-------------|-----------------|
| `Provider` | Proveedor de base de datos legacy | `sqlite`, `postgres`, `sqlserver` |

### ConnectionStrings

| Clave | Descripción |
|-------|-------------|
| `AuthDatabase` | Cadena de conexión a la BD legacy (usuarios/roles) |
| `RefreshTokenDatabase` | Cadena de conexión a la BD SQLite (tokens/auditoría) |

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

El endpoint `/api/command/execute-async` está protegido con el atributo `[Authorize]`. Las solicitudes sin un token JWT válido recibirán una respuesta `401 Unauthorized`.

---

## Endpoints de Ejecución de Comandos

### POST /api/command/execute-async

Encola una nueva solicitud de ejecución de comando. Requiere autenticación JWT.

**Encabezados requeridos:**
```
Authorization: Bearer <token_jwt>
```

**Respuesta:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Pending",
  "message": "Command enqueued successfully"
}
```

---

## Flujo de Ejecución

1. El usuario inicia sesión a través de la interfaz HTML
2. El usuario presiona "Ejecutar" en la interfaz principal
3. Se envía una solicitud POST autenticada al endpoint `/api/command/execute-async`
4. Se crea un `ExecutionQueueItem` y se encola en `ExecutionQueue`
5. Se devuelve inmediatamente el ID del item encolado
6. `QueuedExecutionService` descola el item de forma automática
7. `CommandExecutor` ejecuta `Datax.SAFI.Downloader.exe` con los parámetros configurados
8. El resultado se guarda en `D:\ETLResult_[ID].json`

---

## Archivos de Resultado

Cada ejecución genera un archivo JSON en la ruta configurada (`D:\` por defecto):

```
D:\ETLResult_[GUID].json
```

Ejemplo de contenido:

```json
{
  "itemId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Success",
  "exitCode": 0,
  "output": "Output de la aplicación...",
  "error": "",
  "executedAt": "2026-05-01T10:00:00Z",
  "completedAt": "2026-05-01T10:05:00Z"
}
```

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

---

## Notas Importantes

- Asegúrese de que la ruta `ExePath` apunte a una ubicación válida donde exista `Datax.SAFI.Downloader.exe`
- El disco `D:` debe existir y tener espacio disponible para los archivos de resultado
- Los parámetros del comando son estáticos y se leen del archivo de configuración
- La clave secreta JWT (`Jwt:SecretKey`) debe tener al menos 32 caracteres
- La base de datos legacy debe contener las tablas `user` y `userrole` con la estructura esperada
- El servicio no incluye Swagger; solo la interfaz HTML está disponible
- Todos los comentarios y código fuente están en inglés, excepto este archivo de documentación