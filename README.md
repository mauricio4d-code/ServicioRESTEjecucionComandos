# ServicioRESTEjecucionComandos

Servicio REST en C# (.NET 8) que proporciona una interfaz web para ejecutar comandos de forma asincrona mediante una cola de ejecucion con procesamiento paralelo configurable.

## Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (o superior)
- Windows (para ejecucion de aplicaciones de consola nativas)

## Estructura del Proyecto

```
ServicioRESTEjecucionComandos/
├── Program.cs                          # Punto de entrada y configuracion de dependencias
├── ServicioRESTEjecucionComandos.csproj # Archivo de proyecto
├── appsettings.json                    # Configuracion del servicio
├── Controllers/
│   └── CommandController.cs            # Endpoint REST para ejecutar comandos
├── Models/
│   └── ExecutionQueueItem.cs           # Modelo para items de la cola
├── Services/
│   ├── CommandExecutor.cs              # Ejecuta la aplicacion de consola
│   ├── ExecutionQueue.cs               # Cola thread-safe para items
│   └── QueuedExecutionService.cs       # Servicio de fondo que procesa la cola
└── wwwroot/
    └── index.html                      # Interfaz web para ejecutar comandos
```

## Configuracion

El archivo `appsettings.json` contiene todas las configuraciones del servicio:

```json
{
  "DataxConfig": {
    "ExePath": "C:\\Path\\To\\Datax.SAFI.Downloader.exe",
    "Code": "S_BOEIF_99_00001",
    "Start": "2026-02-28",
    "End": "2026-02-28",
    "Codesend": "IBBIS"
  },
  "QueueConfig": {
    "WaitSeconds": 10,
    "MaxParallelExecutions": 3
  },
  "ResultConfig": {
    "OutputPath": "D:\\"
  }
}
```

### Parametros de Configuracion

| Seccion | Clave | Descripcion | Valor por Defecto |
|---------|-------|-------------|-------------------|
| DataxConfig | ExePath | Ruta completa a la aplicacion de consola Datax.SAFI.Downloader.exe | - |
| DataxConfig | Code | Parametro -code para el comando | S_BOEIF_99_00001 |
| DataxConfig | Start | Parametro -start (fecha inicio) | 2026-02-28 |
| DataxConfig | End | Parametro -end (fecha fin) | 2026-02-28 |
| DataxConfig | Codesend | Parametro -codesend | IBBIS |
| QueueConfig | WaitSeconds | Segundos de espera cuando la cola esta vacia | 10 |
| QueueConfig | MaxParallelExecutions | Maximo de comandos ejecutandose simultaneamente | 3 |
| ResultConfig | OutputPath | Ruta donde se guardan los archivos de resultado | D:\ |

## Como Correr el Servicio

### Opcion 1: Usando dotnet CLI

```bash
cd ServicioRESTEjecucionComandos
dotnet run
```

### Opcion 2: Usando Visual Studio

1. Abra el archivo `ServicioRESTEjecucionComandos.csproj` en Visual Studio
2. Presione F5 o haga clic en "Iniciar depuracion"

### Acceso al Servicio

Una vez iniciado el servicio, acceda a:

```
http://localhost:5000
```

o

```
http://localhost:5001
```

(El puerto exacto se mostrara en la consola al iniciar)

La pagina principal redirige automaticamente a la interfaz HTML donde puede presionar el boton "Ejecutar".

## Endpoints REST

### POST /api/command/execute-async

Encola una nueva solicitud de ejecucion de comando.

**Respuesta:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Pending",
  "message": "Command enqueued successfully"
}
```

## Flujo de Ejecucion

1. El usuario presiona "Ejecutar" en la interfaz HTML
2. Se envia una solicitud POST al endpoint `/api/command/execute-async`
3. Se crea un `ExecutionQueueItem` y se encola en `ExecutionQueue`
4. Se devuelve inmediatamente el ID del item encolado
5. `QueuedExecutionService` descola el item de forma automatica
6. `CommandExecutor` ejecuta `Datax.SAFI.Downloader.exe` con los parametros configurados
7. El resultado se guarda en `D:\ETLResult_[ID].json`

## Archivos de Resultado

Cada ejecucion genera un archivo JSON en la ruta configurada (`D:\` por defecto):

```
D:\ETLResult_[GUID].json
```

Ejemplo de contenido:
```json
{
  "itemId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Success",
  "exitCode": 0,
  "output": "Output de la aplicacion...",
  "error": "",
  "executedAt": "2026-05-01T10:00:00Z",
  "completedAt": "2026-05-01T10:05:00Z"
}
```

## Ejecucion Paralela

El servicio permite ejecutar multiples comandos en paralelo. El numero maximo de ejecuciones simultaneas se configura mediante `MaxParallelExecutions` en el archivo `appsettings.json` (valor por defecto: 3).

Este limite se implementa usando un `SemaphoreSlim` que garantiza que nunca haya mas instancias de `CommandExecutor` ejecutandose al mismo tiempo que el numero configurado.

## Notas Importantes

- Asegurese de que la ruta `ExePath` apunte a una ubicacion valida donde exista `Datax.SAFI.Downloader.exe`
- El disco `D:` debe existir y tener espacio disponible para los archivos de resultado
- Los parametros del comando son estaticos y se leen del archivo de configuracion
- El servicio no incluye Swagger; solo la interfaz HTML esta disponible
