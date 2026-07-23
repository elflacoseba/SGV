# Decisiones de Implementación

## Integración Continua

No se utiliza GitHub CI. El repo es unipersonal en etapa de desarrollo activo; los tests que requieren MySQL (`[MySqlFact]`) no pueden ejecutarse en runners de GitHub sin una base de datos real y la suite completa tarda más de lo razonable para feedback iterativo. La validación se hace localmente con `dotnet test SGV.slnx`. Los tests `[MySqlFact]` se skipean automáticamente cuando no hay conexión MySQL disponible. El workflow `.github/workflows/ci.yml` existe como referencia pero está desactivado.

## SDK y Target Framework

Los proyectos apuntan a `net10.0` (.NET 10). El archivo `global.json` fija el SDK en `10.0.300` con roll-forward `latestMajor` para permitir compatibilidad con versiones posteriores del SDK 10.x.

## Proveedor de Base de Datos

Se utiliza Pomelo Entity Framework Core 9.x como proveedor único para MySQL 8. Los paquetes `Microsoft.EntityFrameworkCore*`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore` y `Pomelo.EntityFrameworkCore.MySql` permanecen en versiones 9.x porque Pomelo 9 depende de EF Core relational `>= 9.0.0 && < 9.0.999`. SQL Server no se soporta como proveedor activo.

## Índices Únicos con Soft Delete

MySQL no soporta índices filtrados como SQL Server. Para preservar las reglas de unicidad sobre registros activos (no eliminados), se utilizan columnas generadas (computed columns) con índices únicos. La columna generada devuelve el valor de la columna de negocio cuando el registro está activo (`IsDeleted = 0`) y `NULL` cuando está eliminado. MySQL permite múltiples `NULL` en índices únicos, lo que replica el comportamiento de los índices filtrados de SQL Server.

## Identity

Se mantiene `IdentityUser` con clave string, por lo que las columnas de auditoría que referencian usuarios usan `varchar(450)`. Esta decisión conserva el comportamiento estándar de ASP.NET Core Identity y evita personalización prematura.

## Ocupaciones Activas

La versión inicial aplica una única ocupación vigente por Puesto (`ActivePuestoIdUnique`) y una única ocupación vigente por la combinación Persona + Puesto (`ActivePersonaPuestoUnique`), mediante columnas generadas con índices únicos. Una Persona puede mantener varias ocupaciones activas simultáneas siempre que correspondan a Puestos distintos. La regla vigente de unicidad por persona simple no se aplica; una futura restricción de ese tipo requeriría reintroducir la columna `ActivePersonaIdUnique` con su índice único y la verificación correspondiente en la capa de aplicación.

## Postulantes Externos

Los postulantes externos se registran sin habilidades estructuradas en esta versión. La compatibilidad automática queda enfocada en postulantes internos vinculados a una persona.

## Auditoría

La auditoría se implementa con una tabla única `Auditorias` y un interceptor de EF Core. Se excluyen campos sensibles por nombre para evitar persistir contraseñas, tokens o stamps de seguridad en JSON.

## TestSgvDbContextFactory (separado del factory de producción)

El factory de tests (`tests/SGV.Tests/Persistencia/TestSgvDbContextFactory.cs`) es independiente de `SgvDbContextFactory`. Razones:

1. **Responsabilidades distintas:** el factory de producción está diseñado para `dotnet ef` design-time (migraciones, scripting). El de tests persigue disponibilidad inmediata.
2. **Default seguro en tests, fail-loud en producción:** `TestSgvDbContextFactory` cae a `localhost:3306;Database=sgv_test;User=root;Password=` cuando no hay configuración externa. `SgvDbContextFactory` tira `InvalidOperationException` en la misma situación — es parte de la seguridad: no exponer credenciales por defecto.
3. **Aislamiento:** los tests nunca heredan config de producción ni viceversa. Si el developer setea `ConnectionStrings__SgvDatabase`, ambos apuntan al mismo target, pero cada uno resuelve su propia cadena.
4. **El runtime de la API no usa ninguno de los dos factories:** lee `builder.Configuration.GetConnectionString("SgvDatabase")` vía DI estándar en `Program.cs`.

## SgvDbContextFactory fail-loud

El factory de producción (`src/SGV.Infraestructura/Persistencia/SgvDbContextFactory.cs`) **no tiene fallback de conexión**. Si no se configura `ConnectionStrings:SgvDatabase` (vía user-secrets, env var o appsettings), lanza `InvalidOperationException` con un mensaje que orienta al developer. Históricamente tenía un placeholder `"CONEXION_STRING_AQUI"` y luego un default con credenciales hardcodeadas, ambos eliminados por razones de seguridad.

Cada developer debe configurar una vez:
```bash
dotnet user-secrets set "ConnectionStrings:SgvDatabase" \
  "Server=localhost;Port=3306;Database=SGV;User=root;Password=TU_PASSWORD" \
  --project src/SGV.Api
```
CI exporta `ConnectionStrings__SgvDatabase` directamente en `.github/workflows/ci.yml`.

## Contrato runtime MySQL — health, readiness y startup

Esta subsección documenta el contrato operativo de `SGV.Api` y `SGV.Web` respecto del runtime MySQL introducido por el change `2026-07-14-fix-126-operational-tech-debt` (issue #126). Cubre los probes de liveness y readiness, el comportamiento de `ServerVersion.AutoDetect`, el `Connection Timeout` recomendado, la separación entre los factories design-time y el runtime, y la ubicación de los secretos por ambiente. El objetivo es que cualquier operador pueda decidir qué esperar del proceso sin tener que leer el código de los composition roots.

### Liveness

`GET /health/live` en `SGV.Api` y `SGV.Web` no requiere MySQL ni contacto con la API upstream. Responde `200 OK` únicamente si el proceso está vivo y el pipeline HTTP responde. Es útil como `livenessProbe` de Kubernetes o equivalente: si el binario está en un loop de crash, el orquestador lo detecta y reinicia. La ruta está mapeada con `Predicate = _ => false` para garantizar que ningún check con side effects (DB, red) se ejecute durante liveness.

### Readiness

`GET /health/ready` en `SGV.Api` requiere MySQL alcanzable. El check abre una conexión cruda con `MySqlConnector.MySqlConnection` (sin pasar por EF Core ni por `SgvDbContext`), respeta el `CancellationToken` del orquestador, y reporta `Healthy` si la conexión abre o `Unhealthy(message)` si falla. Por construcción **no dispara `ServerVersion.AutoDetect`** ni resuelve el contexto de EF, así que el probe no introduce latencia adicional por detección de versión.

`GET /health/ready` en `SGV.Web` requiere que la API upstream responda `200` a su propio `/health/live` dentro de 3 segundos (`HttpClient.Timeout = TimeSpan.FromSeconds(3)` en el named client `SgvApiHealthProbe`, sin `ApiBearerTokenHandler` porque es un probe, no un request user-facing). Responde `503 Service Unavailable` con cuerpo JSON `status: "Unhealthy"` si algún componente está caído.

### Anonimato de los probes

Los dos probes son anónimos en API y Web. Cada `MapHealthChecks(...)` aplica `.AllowAnonymous()` explícito. La `FallbackPolicy = RequireAuthenticatedUser()` permanece intacta: solo `/health/live` y `/health/ready` son excepción. Esto preserva el default-deny vigente sobre el resto de la API y queda codificado por el delta `openspec/changes/2026-07-14-fix-126-operational-tech-debt/specs/sgv-readonly-api/spec.md`. Los orquestadores pueden pegar a los probes sin credenciales y sin recibir `401` ni redirects a `/auth/sign-in`.

### Timeout de conexión recomendado

La connection string productiva **DEBE** incluir `Connection Timeout=5` (cinco segundos) para acotar el `Open()` de `MySqlConnector` tanto en el readiness check como en el primer `ServerVersion.AutoDetect` de `SgvDbContext`. Sin esta configuración, `MySqlConnector` cae al default de plataforma (típicamente 15 segundos), y un MySQL inalcanzable puede colgar el primer request del proceso durante ese presupuesto.

El chequeo del runtime no aborta al `Build()` si falta `Connection Timeout`: la advertencia queda cubierta por esta documentación operativa (`.NET 10` no expone `ValidateOptionsResult.Warn`, ver `design.md` §4.E). En cambio, una connection string ausente, whitespace o sin `Server=` y `Database=` sí aborta el host.

### `ServerVersion.AutoDetect`

`ServerVersion.AutoDetect(connectionString)` se ejecuta la primera vez que `SgvDbContext` se resuelve (es decir, en el primer request HTTP que use la DB). No se pre-calienta en el readiness check ni en el `Build()`. El costo de la detección queda diferido al primer uso real. Operadores pueden mitigar el riesgo con:

- **Pre-warm externo** (`curl` desde el load balancer, un warm-up job o un `initContainer` que pegue a una ruta que resuelva el contexto antes de marcar el pod como `Ready`).
- **Versión fija** en `SgvDbContextFactory` design-time (`MySqlServerVersion(8.0.36)`) ya está aplicada. Replicar esa decisión en runtime es una decisión separada que excede el alcance de este change.

### Separación design-time vs runtime

- `SgvDbContextFactory` (design-time, en `src/SGV.Infraestructura/`) usa `MySqlServerVersion(8.0.36)` fija y aplica el principio fail-loud. Sirve únicamente para `dotnet ef` (migraciones, scripting). El host de la API **no** lo invoca.
- `Program.cs` (runtime, en `src/SGV.Api`) usa `ServerVersion.AutoDetect(connectionString)` en el registro de `SgvDbContext`. La lambda se evalúa al resolver el contexto, no necesariamente durante `builder.Build()`.
- Esta coexistencia no rompe el contrato de migraciones: `dotnet ef migrations` lee `SgvDbContextFactory`; la API runtime usa la registrada en DI. Los tests usan `TestSgvDbContextFactory`, completamente independiente.

### Ubicación de los secretos por ambiente

- **CI** (`mysql:8.0` service en `.github/workflows/ci.yml`): exporta `ConnectionStrings__SgvDatabase` como variable de entorno del job. ASP.NET Core convierte `__` en `:` para `IConfiguration`.
- **Local dev**: cada developer debe generar su propia connection string con `dotnet user-secrets set "ConnectionStrings:SgvDatabase" "<su conexión>" --project src/SGV.Api`. El factory de tests cae a un default seguro (`localhost:3306;Database=sgv_test;User=root;Password=`) para que la suite corra sin setup cuando MySQL está disponible; si MySQL no responde, los `[MySqlFact]` se omiten limpio.
- **Producción / staging**: secret manager del orquestador (AWS Secrets Manager, GCP Secret Manager, Azure Key Vault) inyectado como env var al arranque del pod. **Nunca commitear** la connection string productiva a git. Los archivos `appsettings*.json` versionados no contienen connection strings reales; solo config no sensible (logging, Swagger, origins, placeholder JWT dev).

> El placeholder JWT dev (`DEV-PLACEHOLDER-DO-NOT-USE-IN-PROD-0000000000000000`) presente en `src/SGV.Api/appsettings.Development.json` y `src/SGV.Web/appsettings.Development.json` es **solo** para development local. **NO DEBE** aparecer como valor productivo en ningún ambiente. Detectarlo en una review es trivial con `grep "DEV-PLACEHOLDER" config.json` o equivalente en el repositorio de configuración del orquestador.

### Migraciones

Las migraciones EF Core **no se ejecutan al startup** de `SGV.Api`. Corren fuera de banda, operadas por pipeline CI o por `dotnet ef database update` manual contra el ambiente destino. El único `Database.Migrate()` productivo vive en el bootstrap de tests (`tests/SGV.Tests/Persistencia/MySqlTestDatabaseBootstrap.cs:92-108`).

### Validación al startup

Si `ConnectionStrings:SgvDatabase` falta, está whitespace, o no incluye `Server=` y `Database=`, `SGV.Api` aborta el `Build()` con `Microsoft.Extensions.Options.OptionsValidationException` citando la clave `ConnectionStrings:SgvDatabase` y la causa específica. El host **no** continúa con `ServerVersion.AutoDetect` ni con el registro del contexto. Esto coincide con el patrón fail-loud vigente para JWT (`Program.cs` de ambos proyectos) y para `SgvApi:BaseUrl` en `SGV.Web`. La validación es diferida (`IValidateOptions<DbContextOptions<SgvDbContext>>`) y se ejecuta en el primer resolve del contexto vía `ValidateOnStart`; además hay un throw temprano inline en `Program.cs` para cortar antes de cualquier override de `WebApplicationFactory.ConfigureAppConfiguration`.

## Mapa de bloques GUID reservados por catálogo

Para que los catálogos inmutables seedeados por migración tengan IDs estables y predecibles — y para evitar colisiones accidentales entre catálogos que crezcan a futuro — el proyecto reserva bloques contiguos de 16 bits del espacio de GUIDs. Cada bloque agrupa `2^16 = 65536` filas (suficiente para catálogos pequeños/medianos); el primer byte del `Guid` (little-endian byte 0) identifica el catálogo al que pertenece la fila. Todos los IDs son persistidos como `CHAR(36) COLLATE ascii_general_ci` en MySQL.

| Bloque GUID     | Catálogo                        | Ejemplo de uso            | Constantes                       |
|-----------------|---------------------------------|---------------------------|----------------------------------|
| `70000000-…`    | `NivelCargo` (issue #141)       | `NivelCargoConstantes`    | `DirectivoId`, `OperativoId`     |
| `71000000-…`    | `TipoDocumento` (issue #147)    | `TipoDocumentoConstantes` | `DniId`, `LeId`, `LcId`, `PasaporteId` |
| (libre)         | Próximos catálogos              | reservado                 | —                                |

**Por qué bloques y no IDs al azar.** Los seed values se persisten tanto en `DatosSemilla.HasData` (model snapshot path) como en `InsertData` dentro de la migración EF. Un test de paridad (`DatosSemilla_*_SeedIdsMatchConstantes`) asserta que ambos lugares usen la misma source-of-truth. Si los IDs se generaran con `Guid.NewGuid()`, ese test sería frágil: cualquier `add migration` accidental movería los IDs en el snapshot sin tocar la fila viva. Con bloques reservados por catálogo, los IDs quedan explícitos en el código de constantes y el bloque de 16 bits sirve como una "etiqueta" legible del catálogo dueño.

**Por qué 16 bits y no otro tamaño.** Un bloque de 16 bits provee 65536 filas. `NivelCargo` usa 2 (Directivo, Operativo); `TipoDocumento` usa 4 (DNI, LE, LC, Pasaporte). El catálogo más grande previsto a futuro sigue quedando holgado. Si un catálogo crece más allá de 65536 filas (no previsto), se le asigna un nuevo bloque adyacente.

**Regla operativa para próximos cambios.** Cualquier catálogo inmutable nuevo DEBE:

1. Asignarse un bloque contiguo `XX000000-…` con `XX` aún no usado.
2. Declarar sus IDs en `src/SGV.Infraestructura/Persistencia/Catalogos/<Nombre>Constantes.cs` siguiendo el patrón de `NivelCargoConstantes` y `TipoDocumentoConstantes`.
3. Actualizar este mapa en `docs/decisiones-implementacion.md` y `AGENTS.md`.

Los catálogos mutables (CRUD vía API) NO usan este mapa: generan IDs con `Guid.NewGuid()` como cualquier entidad de negocio.

## Gestión de secretos JWT

`JwtOptions.SigningKey` cumple el mismo principio fail-loud que `SgvDbContextFactory`: no hay default embebido. Si la sección `Jwt:SigningKey` falta, está vacía, contiene solo whitespace o mide menos de 32 bytes UTF-8, el host **no arranca** y `Program.cs` propaga un `Microsoft.Extensions.Options.OptionsValidationException` con el mensaje `Jwt:SigningKey must be configured and ≥32 UTF-8 bytes`. Este contrato se valida en `WebApplicationFactory<TEntryPoint>.CreateClient()` vía `ValidateOnStart`, así que cualquier arranque — development, CI o producción — cae en el mismo fail-loud. Aplica tanto a `SGV.Api` como a `SGV.Web`: la API firma/valida bearer tokens y la Web valida firma, issuer, audience y lifetime antes de convertir el JWT en principal de cookie.

**Dev local.** `src/SGV.Api/appsettings.Development.json` y `src/SGV.Web/appsettings.Development.json` proveen el mismo placeholder pinned (≥32 bytes UTF-8, sufijo `DEV-PLACEHOLDER-DO-NOT-USE-IN-PROD-0000000000000000`) para que `dotnet run` funcione sin setup adicional y ambos hosts acepten el mismo contrato JWT. Para pruebas locales con tokens propios, cada developer debe generar una clave aleatoria propia y persistirla en ambos proyectos con:

```bash
dotnet user-secrets set "Jwt:SigningKey" "<random ≥32 bytes ASCII>" --project src/SGV.Api
dotnet user-secrets set "Jwt:SigningKey" "<random ≥32 bytes ASCII>" --project src/SGV.Web
```

> **El placeholder dev NO es apto para producción.** Es público en el repo. Cualquier deploy que arranque con él es vulnerable a falsificación de tokens admin. La diferencia entre el placeholder y una clave real es detectable con `grep "DEV-PLACEHOLDER" config.json` en cualquier review.

**Producción / CI.** No se commitea ninguna clave. Las opciones soportadas son:

1. Variable de entorno `Jwt__SigningKey` (ASP.NET Core convierte `__` en `:` para `IConfiguration`).
2. Secret manager del proveedor (AWS Secrets Manager, GCP Secret Manager, Azure Key Vault, etc.) inyectado como env var al arranque del pod.

**Operación del secreto en GitHub Actions.** El job de tests exporta `Jwt__SigningKey` desde `secrets.JWT_SIGNING_KEY` (defense-in-depth: aunque el placeholder dev cubre el caso normal, este export garantiza que la suite no dependa de él). El valor es un secreto dedicado (≥32 bytes), independiente del placeholder dev, y se rota manualmente. Para crearlo o rotarlo:

```bash
openssl rand -base64 48
```

…y guardar el resultado en *Settings → Secrets and variables → Actions → JWT_SIGNING_KEY* del repositorio, scope `Environment: production` si aplica.

## Inmutabilidad de `Codigo` en `UnidadOrganizativa`

`UnidadOrganizativa.Codigo` es la identidad lógica de la unidad. Una vez creada, **no puede cambiar**. El contrato se sostiene en tres capas, cada una con un mecanismo distinto pero convergente:

1. **Dominio** — `UnidadOrganizativa` es `sealed record class : EntidadAuditable` con propiedades `init`. `Codigo` se asigna únicamente en el constructor primario. Toda mutación posterior (`Actualizar`, `DefinirVigencia`, `CambiarUnidadPadre`, `Activar`, `Desactivar`) devuelve una nueva instancia vía `with` y **nunca** expone `Codigo` como parámetro. El método legacy `CambiarDatos(codigo, ...)` está eliminado. La asimetría con `Puesto` (que mantiene `sealed class` con `private set`) es deliberada: no se quiere acoplar `Puesto` a esta restricción.

2. **Contrato HTTP** — `SGV.Contracts.Organizacion.Comandos.ActualizarUnidadOrganizativaRequest` no tiene `Codigo`. El binding de System.Text.Json descarta silenciosamente cualquier `codigo` extra en el body de `PUT /api/v1/unidades-organizativas/{id}`. El campo queda **fuera de contrato**: no se persiste, no se valida, no genera error. Esta propiedad aplica también a clientes maliciosos que envíen `{"codigo":"HACKED", ...}` — el servidor devuelve la unidad con su `Codigo` original intacto. La capa web refuerza la regla ocultando el input en `Edit` (PR3).

3. **Persistencia** — `PersistenceToDomainMapper.ToDomain(UnidadOrganizativaEntity)` no usa `SetProperty` / `BindingFlags.NonPublic` para `IsActive`, `UnidadPadre` ni `TipoUnidadOrganizativa`. Esas propiedades se aplican con `with { ... }` sobre el record. La razón: `PropertyInfo.SetValue` (que es lo que envuelve el helper `SetProperty`) no respeta el modifier `IsExternalInit` en runtime, así que podría saltarse el `init`-only del record. El `with` del compilador sí lo respeta y mantiene el invariante end-to-end. La suite incluye un test estructural (`ToDomain_UnidadOrganizativa_NoLlamaSetPropertyReflectionHelper`) que recorre el IL del método y falla si alguien re-introduce el helper.

**Reactivación** — `ReactivarAsync` es el único flujo que sigue validando conflicto por código activo. La validación se hace contra `unidad.Codigo` (el código persistido en el record cargado), **no** contra un valor enviado por el cliente, porque el cliente nunca envía código en update. El índice único computado `ActiveCodigoUnique` (`CASE WHEN IsDeleted = 0 THEN Codigo ELSE NULL END`) en `UnidadOrganizativaConfiguracion` sigue siendo la red de seguridad a nivel DB.

### Generalización post #124 — `Reconstitute(...)` en las 6 entidades principales

Tras el change **#124** (archivado en `openspec/changes/archive/2026-07-13-fix-124-persistence-mapper-reconstitute/`), la estrategia `Reconstitute(...)` dejó de ser una excepción para `UnidadOrganizativa` y se extendió como **patrón único** a las 6 entidades principales que el mapper reconstituye desde persistencia:

| Entidad | `Reconstitute(...)` | Migración adicional |
|---|---|---|
| `Cargo` | `src/SGV.Dominio/Organizacion/Cargo.cs` | — |
| `Habilidad` | `src/SGV.Dominio/Habilidades/Habilidad.cs` | — |
| `Puesto` | `src/SGV.Dominio/Organizacion/Puesto.cs` | reusa `CambiarPuestoSuperior` para invariante `Id != puestoSuperiorId` |
| `Persona` | `src/SGV.Dominio/Personas/Persona.cs` | acepta `telefono` / `tipoDocumento` / `numeroDocumento` explícitos |
| `Ocupacion` | `src/SGV.Dominio/Ocupaciones/Ocupacion.cs` | replica validación `FechaFin >= FechaInicio` del ctor primario |
| `UnidadOrganizativa` | `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` | migrada de `init` + `with`-returning a `private set` + `void`-return mutators para paridad total |

**Consecuencias del cambio:**

1. **`UnidadOrganizativa` pierde la asimetría `init`-only / `with`-returning** que documentaban los puntos (1) y (3) anteriores. Sus propiedades (`Codigo`, `Nombre`, `TipoUnidadOrganizativaId`, `Descripcion`, `UnidadPadreId`, `VigenteDesde`, `VigenteHasta`) ahora son `private set` (no `init`). Sus mutadores (`Actualizar`, `DefinirVigencia`, `CambiarUnidadPadre`, `Activar`, `Desactivar`) retornan `void` y mutan `this` (no devuelven nueva instancia vía `with`). El test `Codigo_EsInmutableTrasCreacion` se reformuló para chequear **"setter NO público"** (sigue garantizando que `Codigo` solo se asigna dentro de la entidad), no el modifier `IsExternalInit`.
2. **`PersistenceToDomainMapper.cs` ya no usa `PropertyInfo.SetValue` ni `SetProperty<T>`**. Los 12 call sites anteriores (`SetProperty(cargo, "IsActive", ...)` etc.) fueron reemplazados por invocación directa de cada factory `X.Reconstitute(...)`. El helper `SetProperty<T>` (`PersistenceToDomainMapper.cs:225-232` pre-#124) y la directiva `using System.Reflection;` están eliminados.
3. **Asimetría con `Cargo` desaparece**: el punto (1) original aclaraba que `Puesto` (no `UnidadOrganizativa`) mantiene `private set`. Tras #124, **las 6 entidades comparten el mismo shape** (`internal Reconstitute` + `private set` + `void`-return mutators). El equipo ya no necesita recordar la excepción de UO.

**Defensa contra reintroducción de reflexión:** la suite incluye **6 tests IL estructurales** (1 por entidad, replicando el patrón de `UnidadOrganizativaRepositoryTests.cs:984-1045`) que recorren el cuerpo IL de cada `ToDomain(TEntity)` y fallan si alguien re-introduce el helper `SetProperty<T>` o cualquier llamada a `PropertyInfo.SetValue`. El de `UnidadOrganizativa` ya existía pre-#124; los otros 5 (`Cargo`, `Habilidad`, `Puesto`, `Persona`, `Ocupacion`) son nuevos.

**`InternalsVisibleTo`** — el factory `Reconstitute(...)` es `internal static`, por lo que `SGV.Dominio.csproj` declara:

- `<InternalsVisibleTo("SGV.Tests") />` — para que los tests IL y de comportamiento puedan invocar el factory directamente.
- `<InternalsVisibleTo("SGV.Infraestructura") />` — para que `PersistenceToDomainMapper` pueda invocar el factory. **`InternalsVisibleTo` no es transitivo** entre assemblies de Clean Architecture, así que Infraestructura necesita su propia visibilidad explícita.

> **Detalle completo** (firmas exactas, orden canónico de asignaciones, lista de consumers UO actualizados, evidencia de TDD): ver `openspec/changes/archive/2026-07-13-fix-124-persistence-mapper-reconstitute/archive-report.md` y `design.md §2`.

## Patrón catálogo vs listado — Unidades Organizativas

`SGV.Web` distingue dos contratos de consumo del API de unidades organizativas según el caso de uso del lado web. Mezclarlos produce los bugs clásicos de la issue #120: catálogos truncados, round-trips sin consumidor y round-trips con `pageSize` mágico.

### El catálogo (dropdown completo)

- **Cuándo se usa** — Sólo cuando el PageModel debe renderizar un `<select>` con **todas** las UO activas (típicamente, formularios de creación).
- **Cliente tipado** — `IUnidadOrganizativaApiClient.GetAllActivasAsync()` (sin parámetros de paginación hacia el PageModel). La implementación recorre internamente páginas hasta igualar `TotalCount`, así el caller no necesita saber de `pageSize`.
- **Endpoint backend preferido** — `GET /api/v1/unidades-organizativas` (sin paginar, retorna `IReadOnlyList<UnidadOrganizativaDto>`). Para catálogos pequeños sirve; para cientos de UO, evaluar autocomplete.
- **Único consumidor vigente** — `Puestos/Create` (PR 3A). Su PageModel requiere los tres catálogos (UO, Cargos, Puestos superiores) para poblar los selects visibles.

### El listado paginado (Index / reportes)

- **Cuándo se usa** — Para vistas de tabla con buscador, filtros, ordenamiento y paginación clásica (10/25/50 por página).
- **Cliente tipado** — `IUnidadOrganizativaApiClient.QueryAsync(UnidadOrganizativaListQuery)`.
- **Endpoint backend** — `GET /api/v1/unidades-organizativas/consulta?page=...&pageSize=...`.
- **Consumidores vigentes** — `UnidadesOrganizativas/Index` (vista principal), reportes.

### Por qué Puestos/Edit no carga catálogos

`_Form.cshtml` envuelve los selects de `UnidadOrganizativaId` y `CargoId` en `@if (!Model.IsEdit) { ... }` — los campos son **inmutables** en un Puesto existente y el form de edición no los renderiza. Por construcción, ningún control visual consume `UnidadOrganizativaOptions` ni `CargoOptions` en Edit:

- `IPuestoForm.UnidadOrganizativaOptions` y `IPuestoForm.CargoOptions` permanecen inicializados como `[]` (lista vacía) porque `IPuestoForm` los exige y Edit no tiene nada que poblar.
- `EditModel` (PR 3B) recibe **únicamente** `IPuestosApiClient` + `ILogger<EditModel>` por constructor — el resto de los clientes se eliminaron en el change `2026-07-13-fix-120-uo-catalog-no-truncation` (issue #120). La firma del constructor es la primera línea de defensa contra reintroducir el dead code.
- `PuestoSuperiorOptions` **sí** se carga: ese dropdown SÍ se renderiza en Edit (es el único campo de "selección" editable de un Puesto).

### Regla operativa para próximos cambios

> **Si querés mostrar un catálogo en Edit, primero modificá `_Form.cshtml` para renderizar el select correspondiente; después habilitá la carga en el PageModel. Nunca cargues un catálogo "por las dudas" — la suite `PuestoEditLoadCatalogsTests` asserta explícitamente que `QueryCalls` y `GetAllCalls` quedan en cero.**

## Autorización del API

La API adopta una postura **default-deny** desde el change `2026-07-09-agregar-autorizacion-api-restantes` (issue #96). El patrón vigente replica los precedentes de `CargosController` (archive `2026-07-01-2026-07-01-cargos-crear-autorizacion-admin`) y `PuestosController` (issue #90).

### Reglas

1. **Fallback policy global en `Program.cs`** — `AddAuthorization(opts => opts.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())`. Cualquier endpoint sin `[Authorize]` explícito falla cerrado con `401 Unauthorized`. Es la red de seguridad para controllers futuros: si se suma un controller nuevo sin `[Authorize]`, ya no queda público por default.
2. **Decoración explícita por controller** — Los controllers que requieren autenticación usan `[Authorize]` a nivel clase. Los controllers con mutaciones (`POST`, `PUT`, `PATCH`, `DELETE`) sobre-ponen `[Authorize(Roles = RolesSgv.Administrador)]` por acción, usando la constante `RolesSgv.Administrador` (sin literales de string repetidos). Las lecturas autenticadas (`GET`) heredan `[Authorize]` de la clase y permiten cualquier rol válido.
3. **Única excepción anónima: `AuthController.Login`** — El handler `Login` (`POST /api/v1/auth/login`) lleva `[AllowAnonymous]` explícito para sobrevivir la fallback policy global. Es la única ruta del API accesible sin credenciales; cualquier otro endpoint sin token devuelve `401`.

### Catálogos read-only autenticados

`NivelesCargoController` (`GET /api/v1/niveles-cargo*`) y `TipoUnidadesOrganizativasController` (`GET /api/v1/tipos-unidad-organizativa*`) pasan de anónimos a autenticados. Esto rompe el contrato histórico de la spec `sgv-readonly-api/spec.md`, que ahora queda reescrita para reflejar esta postura. Los consumidores externos que leían catálogos sin token deben autenticarse o recibir `401`.

### Precedentes y outliers

- **Controllers ya endurecidos** (no tocados por este change): `CargosController`, `PuestosController`, `UsuariosController`, `SkillsController`. Su `[Authorize]` sigue vigente.
- **No se introducen policies nominales nuevas**: el patrón `RolesSgv.Administrador` literal se mantiene para evitar indirección. Si en el futuro se requieren policies compuestas, se decidirá en un change separado.
- **Ventana de exposición por JWT**: el sistema valida firma, issuer, audience y lifetime del JWT pero NO reconsulta roles contra la DB por request. Un usuario cuyo rol cambia de `Administrador` a `GestorVacantes` conserva permisos de mutación hasta que su JWT expire. Esta ventana es inherente a JWT y no se aborda en este change.
- **Sub-recursos**: la decoración `[Authorize]` a nivel clase se hereda a sub-recursos anidados (e.g. `PUT /api/v1/personas/{id}/skills/{skillId}`). El sub-recurso `PersonasController.UpsertSkill`/`DeleteSkill` queda protegido automáticamente; no requiere override adicional porque la mutación ya exige `RolesSgv.Administrador` por la convención adoptada.

## Hardening defense-in-depth en SGV.Web

La shell web replica una defensa en profundidad coherente con el backend para los entry points administrativos de Organización.

### Reglas

1. **Lecturas autenticadas, mutaciones admin-only** — Los listados y detalles de `Cargos` y `Puestos` siguen accesibles para cualquier usuario autenticado, pero las operaciones de mutación (`crear`, `editar`, `eliminar`, `reactivar` y navegación a gestión de habilidades de cargo) se consideran admin-only también en la UI.
2. **UI gating explícito** — `Index.cshtml` de `Cargos` y `Puestos` MUST ocultar CTAs admin-only para usuarios autenticados sin rol `Administrador`. Esto evita affordances engañosas aunque el backend ya falle cerrado con `403`.
3. **GET restringidos con UX consistente** — `Create` y `Edit` de `Cargos` y `Puestos` redirigen a `/error/403` cuando el usuario está autenticado pero no tiene rol `Administrador`.
4. **POST restringidos con `Forbid()`** — Los handlers POST admin-only en Razor Pages devuelven `Forbid()` para no-admin. Con cookie auth, el navegador aterriza en el flujo estándar de access denied del shell.

## Hardening runtime: cookie y CORS por ambiente

`SGV.Api` y `SGV.Web` aplican una matriz de seguridad que depende del ambiente (`ASPNETCORE_ENVIRONMENT`). La matriz se valida en arranque mediante `ValidateOnStart` y fail-loud, de modo que un deploy mal configurado se surface antes de aceptar tráfico.

### Matriz ambiente ↔ seguridad

| Atributo                                | Development              | Distinto de Development |
|-----------------------------------------|--------------------------|-------------------------|
| `SGV.Web` cookie `HttpOnly`             | `true`                   | `true`                  |
| `SGV.Web` cookie `SameSite`             | `Lax`                    | `Lax`                   |
| `SGV.Web` cookie `SecurePolicy`         | `SameAsRequest`          | `Always`                |
| `SGV.Web` HSTS (`app.UseHsts()`)        | no se activa             | 30 días (default)       |
| `SGV.Web` HTTPS redirection             | `app.UseHttpsRedirection()` activo siempre | `app.UseHttpsRedirection()` activo siempre |
| `SGV.Api` CORS `AllowedOrigins`         | opcional (fallback dev)  | **obligatorio**, fail-loud si ausente o vacío |
| `SGV.Api` CORS en Production            | n/a                      | `WithOrigins(<lista>).AllowCredentials()` |
| `SGV.Api` CORS en Development (sin origins) | `SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod()` (sin credenciales) | n/a |
| Combinación prohibida en cualquier CORS | `AllowAnyOrigin` + `AllowCredentials` jamás juntos | igual |

### Configuración de `AllowedOrigins` en la API

`SGV.Api` lee la sección de configuración `AllowedOrigins` como arreglo de strings. En **Production / Staging**, una sección ausente o vacía lanza `InvalidOperationException` con un mensaje que orienta al operador. En **Development**, la ausencia es tolerable y cae al fallback documentado arriba.

**Variables de entorno** (convención ASP.NET Core: `__` reemplaza a `:`):

```bash
AllowedOrigins__0=https://app.example.com
AllowedOrigins__1=https://admin.example.com
```

> **Importante**: los origins se matchean literales, sin slash final. Si configurás `https://app.example.com/`, el middleware CORS rechaza los requests reales. La regla es: protocolo + host + puerto opcional, sin path.

**Archivo `appsettings.json`** (alternativa para deploys con config-as-code):

```json
{
  "AllowedOrigins": [
    "https://app.example.com",
    "https://admin.example.com"
  ]
}
```

El comportamiento fail-loud está protegido por `tests/SGV.Tests/Api/CorsAllowedOriginsValidationTests.cs` (4 tests `[Fact]`):
- Production sin origins → `InvalidOperationException` con mensaje conteniendo `AllowedOrigins`.
- Production con origins → host arranca.
- Development sin origins → host arranca con fallback dev.
- Búsqueda estática: `src/SGV.Api/Program.cs` no contiene `AllowAnyOrigin` (la combinación prohibida es estructuralmente imposible).

### Cookie de autenticación de `SGV.Web`

El ternario en `src/SGV.Web/Program.cs` aplica la matriz según `builder.Environment.IsDevelopment()`. El chequeo vive dentro del bloque `AddCookie(...)` para que sea trivial de auditar y modificar.

```csharp
options.Cookie.HttpOnly = true;
options.Cookie.SameSite = SameSiteMode.Lax;
options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;
```

El comportamiento está protegido por `tests/SGV.Tests/Web/WebCookieAuthenticationOptionsTests.cs` (2 tests `[Fact]`): Production→`Always`, Development→`SameAsRequest`.

> **Por qué `SameAsRequest` en Development**: el equipo trabaja con `http://localhost:5266` sin TLS para iterar. `Always` bloquearía ese flujo de sign-in con un browser moderno. La cookie sigue llevando `HttpOnly` y `Lax`, así que el riesgo en dev queda acotado a robo local (no a exfiltración cross-origin).

### Reverse proxy y `UseForwardedHeaders` (pendiente de implementación)

Cuando `SGV.Api` o `SGV.Web` se sirven detrás de un reverse proxy (nginx, Traefik, ALB), el host ve la IP del proxy, no la del cliente. Sin `UseForwardedHeaders`, las redirecciones HTTPS generan URLs con `Host: proxy`, lo que rompe links absolutos y filtraciones de información.

> **Este change documenta pero NO implementa `UseForwardedHeaders`.** El snippet queda como referencia para el próximo SDD que aborde headers de proxy.

```csharp
// src/SGV.Api/Program.cs (futuro) o src/SGV.Web/Program.cs
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};

// Restringir a proxies conocidos. NUNCA usar KnownNetworks vacías en producción.
forwardedHeadersOptions.KnownProxies.Clear();
forwardedHeadersOptions.KnownProxies.Add(IPAddress.Parse("10.0.0.1"));     // nginx primario
forwardedHeadersOptions.KnownProxies.Add(IPAddress.Parse("10.0.0.2"));     // nginx secundario
// Para subredes internas (k8s pods):
// forwardedHeadersOptions.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("10.244.0.0"), 16));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeadersOptions.Default.ForwardedHeaders;
});

var app = builder.Build();
app.UseForwardedHeaders(forwardedHeadersOptions);
```

Reglas operativas:
- **Listar todos los proxies explícitamente**. No aceptar `X-Forwarded-*` de cualquier origen.
- **No combinar `X-Forwarded-For` con `X-Forwarded-Proto` ciegamente**: si solo te importa el protocolo (para HTTPS redirection), limitá a `XForwardedProto`.
- En **desarrollo sin proxy real**, NO registrar este middleware: aceptar headers forwarded de cualquier origen es un vector de spoofing.

### HSTS en `SGV.Web`

`app.UseHsts()` ya está activo en `src/SGV.Web/Program.cs` para cualquier ambiente distinto de `Development`. El default de 30 días es suficiente para nuestros deployments internos; si en el futuro se sube a `max-age=31536000`, ese cambio requiere un SDD separado.

## Política de paralelismo en la suite de tests

La suite `SGV.Tests` usa `xunit.runner.json` con `parallelizeTestCollections: true` y `maxParallelThreads: 4` para limitar la competencia entre colecciones de tests que comparten instancias de `WebApplicationFactory`.

### Arquitectura de aislamiento

Cada test usa `WebIntegrationFixture` como `ICollectionFixture`. El fixture administra `WebClientLease` por test, y `TestSentinel` provee contadores atómicos que reemplazaron el estado estático compartido de `AuthSessionFactory`.

La clave de la determinismo está en que `WebIntegrationFixture` **no usa estado estático**: cada host `SgvWebApplicationFactory` arranca con su propia clave JWT, su propio `AuthSessionFactory` Singleton (via DI), y su propia cookie de autenticación. No hay caché cross-test que se contamine.

### Watchers de archivos en hosts de test

El assembly de tests fija `DOTNET_USE_POLLING_FILE_WATCHER=1` mediante un module initializer antes de construir cualquier `WebApplicationFactory`. En macOS, la acumulación de hosts de la suite completa saturaba `FSEventStream`: los primeros síntomas eran 43 fallos intermitentes en UO/Puesto, seguidos por timeouts de cinco minutos al construir hosts Cargo y, finalmente, `Stack overflow` dentro de `FileSystemWatcher`. Usar polling elimina esa dependencia del watcher nativo y mantiene intacta la política de cuatro colecciones paralelas.

El tradeoff es un consumo de CPU levemente mayor mientras corre el proceso de tests y una detección de cambios de archivos menos inmediata. Es aceptable porque los hosts de integración no dependen de hot reload, el ajuste queda confinado a `SGV.Tests` y no modifica el comportamiento de API/Web en desarrollo o producción.

### Limitante de paralelismo

El límite `maxParallelThreads: 4` protege dos cosas:

1. **Saturación de hosts**: cada `WebApplicationFactory` arranca un Kestrel real. Con 4 hilos concurrentes × ~1 host por test, el límite evita que el scheduler de xUnit lance decenas de hosts simultáneos que agoten recursos del sistema o disparen `MSB4166`.

2. **Sentinel cross-collection**: `TestSentinel.AliveCount` es atómico pero compartido entre colecciones. Aunque la suite completa es determinista (3 corridas consecutivas idénticas), `maxParallelThreads: 4` reduce la ventana de preemption que puede hacer que un test individual vea un `AliveCount` distinto al esperado.

### Gate de 3 corridas

Todo cambio que toque `tests/SGV.Tests/` debe validarse con **3 corridas consecutivas de `dotnet test SGV.slnx --no-build`** en la misma máquina y commit que reporten:

- Mismo número total de tests pasados y fallados en las 3 corridas.
- Sin `MSB4166` (MSBuild node reuse crash).
- Cada corrida bajo `--no-build` para eliminar la variación de compilación.
- < 15 minutos por corrida.

Si las 3 corridas no son idénticas, el cambio reintroduce no-determinismo y no debe mergearse sin revisión y corrección.

> **Nota (issue #121, PR size:exception)**: el presente change estableció el límite `maxParallelThreads: 4` por experimentación con la suite completa de 1773 tests (3 corridas consecutivas ~42 min c/u). Equipos que reduzcan la suite deberían re-evaluar este número. Ver `openspec/changes/2026-07-11-hacer-suite-tests-determinista/verify-report.md`.

## Issue #125 — Taxonomía de errores para `CommandResult` y clientes HTTP de Web

> Change: `2026-07-13-taxonomia-errores-commandresult` (slice 1 de 4). Artefactos SDD completos en `openspec/changes/2026-07-13-taxonomia-errores-commandresult/`.

### Rationale

`SGV.Contracts` convive con cinco taxonomías paralelas para fallos HTTP: `HabilidadErrorType.Infrastructure`, `CargoCommandResult`/`PuestoCommandResult`/`UnidadOrganizativaCommandResult` (que colapsan 401/403/5xx en `Validation` con magic code `Unexpected`), `CargoSkillCommandResult` (la aproximación más cercana al objetivo pero sin repositorio compartido), `MapSkillError` privado de `CargoApiClient`, y los cinco `*DeleteResult` que exponen `StatusCode/Code/Message` sin categoría semántica. El resultado: cada cliente HTTP repite su propia matriz de clasificación, los `PageModel` ramifican con `if (ex is X)` divergentes, y el mismo status produce un mensaje distinto para el usuario según el dominio.

### Decisión

Una sola taxonomía `ErrorCategoria` definida como `enum` append-only en `src/SGV.Contracts/Comun/ErrorCategoria.cs`. Mantiene `SGV.Contracts` como leaf (verificado: `SGV.Contracts.csproj` solo referencia `Microsoft.IdentityModel.Tokens 8.14.0`). Cada uno de los seis `*Error` records (`HabilidadError`, `CargoError`, `PuestoError`, `UnidadOrganizativaError`, `CargoSkillError`, `UsuarioError`) y los cinco `*DeleteResult` ganan `Categoria: ErrorCategoria`. Los enums `*ErrorType` vigentes se marcan `[Obsolete]` durante el ciclo del change y se eliminan al archivar.

### Reglas invariantes

- **Append-only**: las variantes y sus ordinales son contrato público estable. Agregar nuevas variantes SOLO al final; NO reordenar ni reasignar ordinales.
- **Mapeo nombre-a-nombre**: la conversión entre los enums `*ErrorType` y `ErrorCategoria` se hace vía `ErrorCategoriaMappers.ToCategoria(...)` y `ToTipo<Domain>(...)`. Prohibido el cast `(ErrorCategoria)(int)type` — los ordinales NO coinciden (p.ej. `CargoSkillErrorType.Validation = 1` mientras `ErrorCategoria.Validation = 2`).
- **Round-trip simétrico**: cada `ToCategoria`/`ToTipo<Domain>` es exhaustivo (sin `default:`). Categorías sin equivalente en el dominio origen lanzan `NotSupportedException` con mensaje claro.
- **`[Obsolete]` durante el ciclo**: los enums `HabilidadErrorType`, `CargoErrorType`, `PuestoErrorType`, `UnidadOrganizativaErrorType`, `CargoSkillErrorType`, `UsuarioErrorType` se marcan con `[Obsolete("Use SGV.Contracts.Comun.ErrorCategoria. Will be removed in the archive of change 2026-07-13.")]`. Los call sites existentes siguen compilando porque el atributo se emite como warning por defecto.
- **Eliminación al archivar**: los enums `[Obsolete]` se borran durante la fase `sdd-archive` del change `2026-07-13-taxonomia-errores-commandresult`, NO en este PR ni en los slices 2-4.

### Compatibilidad

- **Source-breaking**: NO. El nuevo parámetro `Categoria` se agrega con default `ErrorCategoria.Unexpected` a los records `*Error` para preservar source-compat. Los enums `[Obsolete]` emiten warning, no error.
- **Wire-breaking**: NO. Los controllers no serializan los enums a `ProblemDetails`. La matriz de status HTTP se preserva.
- **DB-breaking**: NO. La taxonomía es interna a `SGV.Contracts` y `SGV.Web`.

### Archivos clave

- `src/SGV.Contracts/Comun/ErrorCategoria.cs` — enum común (7 variantes, ordinales 0..6).
- `src/SGV.Contracts/Comun/ErrorCategoriaMappers.cs` — mapeos nombre-a-nombre para los 6 enums vigentes.
- `src/SGV.Contracts/*/Comandos/*CommandResult.cs` — 6 `*Error` records con `Categoria` agregado.
- `src/SGV.Contracts/Organizacion/Comandos/CargoSkillDeleteResult.cs` — `Categoria` agregado.
- `src/SGV.Web/Integration/*/...ViewModel.cs` — 4 `*DeleteResult` records con `Categoria` agregado; `PuestoDeleteResult.StatusCode` pasa de `HttpStatusCode` non-nullable a `HttpStatusCode?` nullable.

### Follow-up documentado (fuera de este change)

- `PersonaCommandResult`, `PersonaSkillCommandResult`, `OcupacionCommandResult` (viven en `SGV.Aplicacion`): no se migran en este change. Sólo exponen `NotFound`/`Conflict`/`Validation` hoy; no impactan flujos administrativos y la superficie a migrar sumaría otro bloque sin valor inmediato. Issue de follow-up sugerido tras archive del #125.
- Los `ApiResults.Map*Status` de `SGV.Api/Infrastructure/Results/ApiResults.cs` se centralizan en un `MapCategoria(ErrorCategoria)` exhaustivo en el Slice 4 (issue #125, PR #4).

## Frontend CRUD de Personas

> Change: `2026-07-14-frontend-crud-personas`. Artefactos SDD completos en `openspec/changes/2026-07-14-frontend-crud-personas/`. Chain strategy: `feature-branch-chain` con 4 PRs encadenados contra la tracker `feat/2026-07-14-frontend-crud-personas-tracker`.

### Decisiones de diseño

#### Ruta `/personas` (sibling de `/organizacion/cargos`)

El módulo Personas NO cuelga de `/organizacion/`. La spec original proponía `/organizacion/personas`; en sesión interactiva se confirmó `/personas` como ruta directa. Razones:

1. **Personas no es un subdominio de Organización** — Cargo, Puesto y UnidadOrganizativa modelan la estructura organizacional. Persona es una entidad de dominio independiente (con sus propios skills, datos de contacto, documento) que cualquier capacidad organizacional referencia (e.g. una UnidadOrganizativa tiene responsable → Persona). Anidarla bajo `/organizacion/` confundiría el modelo mental.
2. **Coherencia con la API** — el backend expone `PersonasController` en `/api/v1/personas` (sibling de `/api/v1/cargos`, no anidado). El espejo Web debe respetar el mismo árbol.
3. **URLs estables para integraciones externas futuras** — `GET /personas/{id}` será consumido por terceros sin necesidad de conocer el detalle organizativo.

Consecuencia práctica: el directorio `Pages/Personas/` (no `Pages/Organizacion/Personas/`); la nav apunta a `/personas` con icono `ti ti-user`.

#### Wire-types movidos de `Aplicacion.Personas` a `Contracts.Personas`

Los records `PersonaDto`, `PersonaCommandResult`, `PersonaError`, `PersonaErrorType`, `CrearPersonaRequest` y `ActualizarPersonaRequest` vivían en `SGV.Aplicacion.Personas.*`. Migrar a `SGV.Contracts.Personas.*` siguiendo el precedente de Cargos (archive `2026-07-09-frontend-crud-cargos-pages`):

- **`SGV.Contracts` sigue siendo leaf** — el grafo de proyectos no cambia. `SGV.Web` ya no depende de `SGV.Aplicacion.Personas` (verificado por el grep `grep -r "SGV.Aplicacion.Personas" src/SGV.Web/` que retorna cero hits).
- **JSON shape idéntico** — los DTOs son `sealed record` con los mismos nombres y orden de propiedades; el wire format no cambia. Los tests existentes de `PersonasController` siguen pasando sin tocar.
- **Movimiento, no duplicación** — los archivos originales en `Aplicacion` se borran (no quedan copias huérfanas). Los call sites internos (`PersonaServicioComandos`, `PersonaServicioConsulta`, `PersonasController`, `PersonaSkill*`) actualizan `using SGV.Contracts.Personas.*` (revisión: 4 archivos tocados, ~12 líneas de `using` modificadas, 0 cambios de lógica).
- **`PersonaSkill*` queda en Aplicacion** — los records `PersonaSkillDto`, `PersonaSkillCommandResult`, etc. viven en `SGV.Aplicacion.Personas.Habilidades` y NO se migran en este change. Se mueven en el frontend de habilidades de persona (cambio futuro, fuera de alcance).

#### Asunción del typeahead: dataset activo <500 personas

`Pages/Personas/Shared/_PersonaTypeahead.cshtml` consume `GET /api/v1/personas` completo y filtra client-side ≥2 chars con debounce de 250 ms. La asunción operativa es que **el dataset activo típico no supera 500 personas**. Por debajo de ese umbral:

- Payload de ~100 KB para un GET sin paginación, aceptable para una carga única en `OnGetAsync` del host page.
- Filtro client-side evita round-trips HTTP por keystroke.
- Debounce evita render-cost con cada pulsación.

**Si el dataset supera las ~500 personas activas**, el primer GET pesa >100 KB y deforma la experiencia (latencia de carga, memoria retenida en el navegador). Follow-up documentado: agregar `GET /api/v1/personas/buscar?q={term}` que devuelve las N mejores coincidencias server-side, manteniendo el contrato del partial sin cambios (el JS ya espera un array de `PersonaDto`). Issue de seguimiento sugerido para cuando el `COUNT(*)` de `Personas` activas supere el umbral.

#### `MapCategoriaToLegacyType` endémico en 5 clientes (warning CS8524)

Cada `*ApiClient` de Web (`CargoApiClient`, `PuestoApiClient`, `UnidadOrganizativaApiClient`, `PersonaApiClient` y `HabilidadApiClient`) tiene un método privado `MapCategoriaToLegacyType(ErrorCategoria)` que colapsa la taxonomía común al enum histórico `*ErrorType` para preservar source-compat con call sites vigentes. **El warning CS8524 aparece en los 5 archivos** porque `ErrorCategoria` es append-only (regla del change #125): cuando se agrega una variante nueva, los 5 switches deben actualizarse simultáneamente o el compilador avisa.

El warning **es endémico y aceptado** mientras los enums `[Obsolete]` no se eliminen (archivado del change #125). Endurecerlo exigiría:

1. Invertir el flujo: que el enum `[Obsolete]` se elimine primero, y luego eliminar `MapCategoriaToLegacyType` de cada cliente.
2. O bien introducir un shared slice en `SGV.Web.Integration.Common` que centralice la conversión, eliminando la duplicación pero NO el problema del exhaustivo (la advertencia seguiría apareciendo en el lugar centralizado).

Por ahora, el equipo acepta el warning y lo trata como checklist en code review para cuando se sume una nueva variante de `ErrorCategoria`. Será resuelto naturalmente al archivar el change #125 (cuando los enums legacy se borren).

### Compatibilidad y rollback

- **Source-breaking**: NO. `SGV.Aplicacion.Personas.PersonaDto` etc. ya no existen; cualquier consumer interno que los importaba actualiza el `using`. No quedan referencias externas al repositorio SGV.
- **Wire-breaking**: NO. El JSON shape del API no cambia (mismo nombre de propiedades, mismo orden).
- **DB-breaking**: NO. Cero migraciones.
- **Rollback**: borrar `Pages/Personas/`, `Integration/Personas/`, revertir `Program.cs`, `_Sidenav.cshtml` y `using SGV.Contracts.Personas`. Cero impacto en API runtime, BD o datos.

### Archivos clave

- `src/SGV.Contracts/Personas/Consultas/Dtos/{PersonaDto,PersonaListQuery,PersonaListadoDto,PersonaSegmentoListado}.cs` — wire-types de consulta (4 archivos).
- `src/SGV.Contracts/Personas/Comandos/{CrearPersonaRequest,ActualizarPersonaRequest,PersonaErrorType,PersonaCommandResult,PersonaDeleteResult,PersonaError}.cs` — wire-types de comandos (6 archivos).
- `src/SGV.Web/Integration/Personas/{IPersonaApiClient,PersonaApiClient,PersonaInputModel,PersonaListItemViewModel,PersonaListQueryViewModel,PersonaFormHelpers,PersonaPostResultMapper,IPersonaForm,PersonaTypeaheadViewModel}.cs` — cliente HTTP + helpers (9 archivos).
- `src/SGV.Web/Pages/Personas/{Index,Create,Edit,Details}.{cshtml,cshtml.cs}` + `_Form.cshtml` + `Shared/_PersonaTypeahead.cshtml` — Razor Pages (9 archivos).
- `src/SGV.Web/Program.cs` — `AddHttpClient<IPersonaApiClient, PersonaApiClient>` con `ApiBearerTokenHandler` (10s timeout, paralelo a Cargo/Habilidad/Puesto).
- `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` — ítem colapsable "Personas" con icono `ti ti-user`.
- `tests/SGV.Tests/Web/Persona/{FakePersonaApiClient,PersonaWebTestFixture,FakePersonaApiClientTests,IPersonaApiClientContractTests,PersonaApiClientBasicTests,IndexPageTests,CreatePageTests,EditPageTests,DetailsPageTests,TypeaheadTests,PersonaWebSeamTests}.cs` — 11 archivos, ~80 tests web.

### PR encadenados (feature-branch-chain)

| PR | Squash | Scope | Tests netos |
|----|--------|-------|-------------|
| 1 | `5158cec6` (#143) | Backend paginado `/consulta` + wire-types | ~200 backend |
| 2 | `180b8701` (#144) | Integration client + DI + nav | 0 |
| 3 | `82a5455` (#145) | Razor Pages + typeahead | 0 |
| 4 | (este PR) | Tests web + docs | ~80 web |

Tracker PR (no-merge) mantiene el squash de los 4 PRs encadenados hasta que se decida el merge final. La cadena vive bajo `feat/2026-07-14-frontend-crud-personas-tracker` con 4 work-branches hijos. Cada PR child mantiene su diff enfocado en su work-unit y nunca apunta directo a `main` (regla del chained-pr skill).

### Follow-up documentado (fuera de este change)

- **Frontend de habilidades de persona** (`PersonaSkill*` actualmente vive en `SGV.Aplicacion.Personas.Habilidades`): cuando se sume al scope de Personas, los records deben moverse a `SGV.Contracts.Personas.Habilidades` siguiendo el mismo precedente.
- **`GET /api/v1/personas/buscar?q=`** — endpoint server-side de búsqueda rápida para el typeahead, requerido cuando el dataset activo supere las ~500 personas.
- **Gate de Edit en Details**: el page model actual muestra el botón Editar a cualquier autenticado y delega el gate al handler GET de Edit. Considerar gating visual en Details para UX consistente con Index.

## Módulo Usuarios — soft-delete de Identity con columna generada STORED

> Change: `Implementa módulo usuarios` (PR1 backend cerrado; PR2/3/4 pendientes). Artefactos SDD en `openspec/changes/Implementa módulo usuarios/`. Chain strategy: `feature-branch-chain` con 4 PRs encadenados contra tracker `feat/2026-07-15-implementa-modulo-usuarios-tracker`.

### Contexto

El módulo Usuarios extiende el comportamiento de `AspNetUsers` con baja lógica (`IsDeleted`), replicando el patrón de `Personas` / `UnidadesOrganizativas` / `Habilidades` / `Cargos`: columna `IsDeleted TINYINT(1) NOT NULL DEFAULT 0` + columna generada STORED con índice único para convivencia con soft delete (precedente archivado: `2026-07-11-fix-active-puesto-id-unique-type`).

### Restricción MySQL 8 sobre columnas generadas STORED

MySQL 8 declara `ALGORITHM=INPLACE` incompatible con la creación de una columna generada STORED durante una operación `ALTER TABLE`. El RED inicial sobre base limpia devolvió:

```
ALGORITHM=INPLACE is not supported for this operation. Try ALGORITHM=COPY.
```

La columna STORED exige `ALGORITHM=COPY`, que bloquea lecturas y escrituras sobre `AspNetUsers` durante toda la copia — proporcional al tamaño de la tabla al momento del deploy.

### Decisión adoptada

El maintainer aceptó la **opción A — Aceptar `ALGORITHM=COPY`** en sesión interactiva tras el `sdd-apply` del PR1. La migración `AddSoftDeleteToAspNetUsers` se divide en dos operaciones para minimizar la ventana de bloqueo:

1. **Paso 1 (`INPLACE, LOCK=NONE`)** — `ALTER TABLE AspNetUsers ADD COLUMN IsDeleted TINYINT(1) NOT NULL DEFAULT 0`. Online, sin bloqueo.
2. **Paso 2 (`COPY`)** — `ALTER TABLE AspNetUsers ADD COLUMN ActiveUserNameUnique VARCHAR(256) GENERATED ALWAYS AS (CASE WHEN IsDeleted=0 THEN LOWER(UserName) ELSE NULL END) STORED COLLATE utf8mb4_0900_ai_ci, ADD UNIQUE INDEX IX_AspNetUsers_ActiveUserNameUnique`. Ventana de mantenimiento; tamaño proporcional al `COUNT(*)` de `AspNetUsers` al momento del deploy.

Las alternativas evaluadas y descartadas quedan registradas en `openspec/changes/Implementa módulo usuarios/apply-progress.md`:

- **B — Cambiar a `VIRTUAL`**: viable (`ALGORITHM=INPLACE` lo soporta), pero cambia el shape del DDL aprobado y exige revisión del design.
- **C — Rediseñar el patrón** (trigger, índice condicional): fuera del alcance del change.

### Plan operativo para producción

Antes de aplicar la migración a un ambiente productivo:

1. **Medir la ventana esperada**: `SELECT COUNT(*) FROM AspNetUsers` para estimar el tiempo de copia (regla práctica: ~10 s por cada 100 K filas en hardware medio).
2. **Programar ventana de mantenimiento** con aviso a usuarios (la app puede seguir sirviendo lecturas, pero Login/SignUp/Create quedan bloqueados durante la copia).
3. **Ejecutar la migración en dos pasos** (no atómicos): primero la columna `IsDeleted` (INPLACE), luego la columna STORED y el índice (COPY).
4. **Validar post-deploy**: `SELECT COUNT(*) FROM AspNetUsers WHERE IsDeleted = 1` (debe ser 0 al inicio), `SHOW INDEX FROM AspNetUsers WHERE Key_name = 'IX_AspNetUsers_ActiveUserNameUnique'` (debe existir).

### Limitaciones conocidas

- **Índice único de Identity**: `AspNetUsers` mantiene su índice único estándar sobre `NormalizedUserName`. Si bien la columna nueva `ActiveUserNameUnique` protege la regla pedida (un mismo `UserName` no puede existir dos veces entre usuarios activos), reasignar el mismo `UserName` a un usuario reactivado mientras otro eliminado conserva `NormalizedUserName` puede seguir chocando con Identity. No se alteró ese índice porque no figura en el DDL aprobado; queda como follow-up si la regla se endurece en un change futuro.
- **Auditoría explícita**: `SgvIdentityUser` no extiende `AuditableEntityBase`, por lo que `AuditoriaSaveChangesInterceptor` no captura mutaciones sobre `AspNetUsers`. La auditoría se hace manualmente vía `IAuditoriaServicio.RegistrarAsync` desde cada handler de mutación (`CrearUsuarioHandler`, `EditarUsuarioHandler`, `DesactivarUsuarioHandler`, `ReactivarUsuarioHandler`), incluyendo diffs de `UserName`, `Email` y roles.

### Archivos clave del PR1

- Migración: `src/SGV.Infraestructura/Persistencia/Migraciones/20260715145121_AddSoftDeleteToAspNetUsers.cs`.
- Script SQL idempotente: `docs/migracion-add-softdelete-usuarios.sql`.
- Modelo Identity: `src/SGV.Infraestructura/Seguridad/SgvIdentityUser.cs` + `SgvIdentityUserConfiguracion.cs`.
- Gateway: `src/SGV.Infraestructura/Seguridad/UsuarioIdentityGateway.cs` (consulta paginada/segmentada sin N+1, actualización atómica, baja, reactivación).
- Aplicación: `src/SGV.Aplicacion/Seguridad/Usuarios/UsuarioServicioComandos.cs` (D-01..D-04), `IAuditoriaServicio.cs`.
- API: `src/SGV.Api/Controllers/UsuariosController.cs`, `src/SGV.Api/Seguridad/UsuarioActualHttpContext.cs`.
- Contratos: `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs` (`UsuarioDto` con `Nombres`/`Apellidos` agregados al final; nuevos `ActualizarUsuarioRequest`, `UsuarioListQuery`, `UsuarioListadoDto`, `UsuarioSegmentoListado`).
- Tests: 77 focalizados usuarios + 26 API + 10 MySQL gateway/migración + 2211 totales.

### PR encadenados (feature-branch-chain)

| PR | Rama | Scope | Tests netos |
|----|------|-------|-------------|
| 1 | `feat/2026-07-15-implementa-modulo-usuarios-pr1-backend` | Backend: migración + `/consulta` paginado + endpoints + auditoría | ~38 backend |
| 2 | `feat/2026-07-15-implementa-modulo-usuarios-pr2-integration` (pendiente) | Integration client + DI + sidenav | ~14 |
| 3 | `feat/2026-07-15-implementa-modulo-usuarios-pr3-paginas-listado` (pendiente) | Razor Pages Index/Details/Delete/Reactivar | ~18 |
| 4 | `feat/2026-07-15-implementa-modulo-usuarios-pr4-paginas-form` (pendiente) | Razor Pages Create/Edit + `_Form.cshtml` | ~16 |

Tracker PR (no-merge) mantiene la integración final en `feat/2026-07-15-implementa-modulo-usuarios-tracker` hasta que se decida el merge. La cadena vive bajo ese branch con 4 work-branches hijos. Cada PR child mantiene su diff enfocado en su work-unit y nunca apunta directo a `main` (regla del chained-pr skill).

### Follow-up documentado (fuera del change)

- PR2: cliente tipado `SGV.Web/Integration/Usuarios/{IUsuarioApiClient, UsuarioApiClient}` + DI.
- PR3: Razor Pages Index segmentado + Details readonly + Delete (PRG) + Reactivar (PRG).
- PR4: Razor Pages Create con dropdown Personas + Edit atómico + `_Form.cshtml` compartido + ítem colapsable "Seguridad" en `_Sidenav.cshtml` (gateado `EsAdministrador`).

## Buscador modal de Personas en Crear/Editar Usuario — manejo de `409` (D-10 corregida)

Change archivado: `2026-07-17-buscador-personas-modal` (PRs #158, #159, #160). El `design.md` archivado (D-10) proponía reflejar el `409` por carrera (persona ya con usuario activo) como `ModelState.AddModelError(string.Empty, ...)`, copiando el patrón `CodigoDuplicado` de Cargos. La implementación, en cambio, sigue `tasks.md` y la spec REQ-UCE-10: `ModelState.AddModelError("Input.PersonaId", "Esa persona ya tiene un usuario activo.")`.

### Decisión vigente

Ante `409` en POST de Crear o Edit de Usuario, el feedback se vincula al campo `PersonaId` del formulario (no al `ModelState` general). Esto preserva mejor el form para reintento y cumple REQ-UCE-10 de forma verificable por test (`Post_Create_Con409_PreservaFormYMuestraErrorEnPersonaId` y equivalente Edit).

### Por qué se apartó del `design.md` original

El `design.md` archivado es audit trail del change cerrado y **no se modifica**. Esta entrada en `docs/decisiones-implementacion.md` es la referencia vigente para implementaciones futuras. La razón del apartamiento: `string.Empty` produce un error visible en el `asp-validation-summary` (mejor para errores globales del form, p.ej. conexión con el backend), mientras que `Input.PersonaId` produce el error pegado al campo, que es lo que el usuario necesita ver para entender qué dato конкретно está ocupado. La spec REQ-UCE-10 ya exigía lo segundo.

### Comportamiento a replicar en próximos cambios análogos

Cuando un error 409 (o equivalente de conflicto único) provenga de un endpoint consumido por un formulario Razor:

- **Sí** usar `ModelState.AddModelError("<nombre-del-campo>", "<mensaje-acotado-al-campo>")` para feedback pegado al input.
- **Sí** preservar el resto del form para reintento (excepto password, que nunca se preserva).
- **No** usar `string.Empty` salvo que el error sea genuinamente transversal al form.

Archivos vigentes: `src/SGV.Web/Pages/Seguridad/Usuarios/Create.cshtml.cs`, `src/SGV.Web/Pages/Seguridad/Usuarios/Edit.cshtml.cs` (ambos en `OnPostAsync` / handler equivalente).

## Cultura regional es-AR como default único (issue #191)

### Contexto

La shell web (`SGV.Web`) y la API (`SGV.Api`) operaban con la cultura del
proceso heredada del host. Tres bugs se concentraban en la gestión de
habilidades de Cargos:

1. Los banners de feedback de Bootstrap (success / warning / danger) no
   eran dismissible: el usuario no podía cerrarlos sin esperar al próximo
   redirect.
2. El input `Ponderación` (`type="number"`) aceptaba sólo `"."` como
   separador decimal (HTML5 ignora la cultura de la página). Los usuarios
   con configuración regional es-AR tipeaban `"1,50"` y el form rechazaba
   el valor al guardar.
3. La ponderación podía llegar vacía desde el form; el servicio aplicaba
   un default invisible de `1.00m`, oscureciendo el problema (la grilla
   mostraba una ponderación que el usuario nunca tipeó).

### Decisión adoptada (vigente)

- **Cultura única `es-AR`** para Web y API vía
  `AddLocalization()` + `Configure<RequestLocalizationOptions>` con
  `DefaultRequestCulture = "es-AR"` + `app.UseRequestLocalization()`
  insertado entre `UseRouting()` y `UseAuthentication()` en Web, y
  temprano en el pipeline de API (después de CORS, antes de RateLimiter).
  Es la fuente única de la cultura para render, model binding,
  validación y orden de strings.
- **JSON wire sigue invariante.** El contrato HTTP entre Web y API
  transporta decimales con `.` (default de `System.Text.Json`); la cultura
  es-AR sólo afecta la capa de presentación y los servicios que usen
  `CultureInfo.CurrentCulture` (por ejemplo, el `StringComparer.Create
  (CultureInfo.CurrentCulture, ...)` en el orden de unidades
  organizativas). No hay riesgo de romper el contrato de API.
- **`type="number"` reemplazado por `type="text" inputmode="decimal"
  pattern="[0-9]+([,][0-9]{1,2})?"`** en los inputs de Ponderación
  (grilla editable + form Asignar). HTML5 ignora la cultura en
  `type="number"` y exige `"."`; la única forma nativa de aceptar coma
  sin JS custom es migrar a `text` con `inputmode` + `pattern` +
  validación server-side.
- **`CargoSkillPonderacionRule.TryParse`** es tolerante: si el string
  tiene coma y no tiene punto, la coma es separador decimal de es-AR y
  se reemplaza por punto antes de parsear en `InvariantCulture`. Esto
  esquiva la ambigüedad de `NumberStyles.Number` en es-AR donde la coma
  es a la vez separador de miles y decimal (`"100,00"` sin contexto se
  interpreta como `10000`).
- **`AsignarCargoSkillRequestValidator.Ponderacion` ahora es
  `NotNull`** (era opcional). El form de Asignar renderiza `value="1"`
  como default HTML estático; si el usuario borra el valor, la validación
  local corta antes de invocar al backend y el servicio nunca recibe un
  payload con ponderación vacía. Se eliminó el fallback
  `request.Ponderacion ?? PonderacionPorDefecto` en `CargoSkillServicio`
  — alcanzar esa rama con `null` es ahora un error de programación
  (`ArgumentNullException` defensivo).
- **Alertas `alert-dismissible` + `btn-close`** en todos los banners de
  feedback de `Habilidades.cshtml` (patrón vigente en
  `Pages/Auth/SignIn.cshtml`). El usuario puede cerrar el banner sin
  esperar al próximo PRG.

### Invariantes preservados

- `Personas/Edit.cshtml.cs` y `Cargos/Edit.cshtml.cs` siguen usando
  `InvariantCulture` explícito para serializar `ReturnPage` (campo hidden
  de paginación): son identificadores técnicos que viajan por querystring
  y deben ser invariantes.
- `src/SGV.Api/Program.cs` mantiene `InvariantCulture` para serializar el
  header HTTP técnico `Retry-After`.

### Archivos clave del change

- `src/SGV.Web/Program.cs` — `AddLocalization` + `UseRequestLocalization`.
- `src/SGV.Api/Program.cs` — mismo setup, pipeline API.
- `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml` — alerts
  dismissible, input `text`/`inputmode="decimal"`, default `1`.
- `src/SGV.Web/Pages/Organizacion/Cargos/CargoSkillPonderacionRule.cs` —
  parser tolerante con normalización de coma es-AR.
- `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/AsignarCargoSkillRequestValidator.cs` —
  regla `NotNull` para `Ponderacion`.
- `src/SGV.Aplicacion/Organizacion/Comandos/CargoSkillServicio.cs` —
  eliminación del fallback `PonderacionPorDefecto` (constante pública
  removida).

### Cobertura nueva

- `CargoSkillPonderacionRuleTests.TryParse_WithinRange_ReturnsValidAndParsedValue`:
  espejos es-AR (`"1,00"`, `"2,50"`, `"50,00"`, `"50,75"`, `"100,00"`).
- `CargoHabilidadesValidationTests`:
  `Post_Asignar_PonderacionConComaEsAR_GuardaCorrectamente`,
  `Post_Actualizar_PonderacionConComaEsAR_GuardaCorrectamente`,
  `Post_Asignar_PonderacionVacia_NoInvocaApiYMuestraErrorRequerido`,
  `Render_StatusMessageAlert_LlevaAlertDismissibleYBotonClose`.
- `CargoSkillControllerTests.UpsertSkill_PonderacionNull_Returns400ConMensajeObligatoria`.
- Tests actualizados al nuevo contrato:
  `AsignarCargoSkillRequestValidatorTests.Should_Have_Error_When_Ponderacion_Is_Null`,
  `CargoSkillServicioTests.UpsertAsync_SinEsObligatoria_AplicaDefaultFalseYDevuelveDtoCompleto`,
  `CargoSkillServicioTests.UpsertAsync_PonderacionNull_RetornaValidacionYSinGuardar`,
  `CargoHabilidadesLoadTests` (aserción `value="2,50"` en formato es-AR),
  `CargoHabilidadesDeleteErrorTests` (aserción `alert-dismissible`).

Plan de pruebas ejecutado: `dotnet test SGV.slnx` — 2840 / 2840 verde
(11 tests nuevos sobre la base previa de 2829).


