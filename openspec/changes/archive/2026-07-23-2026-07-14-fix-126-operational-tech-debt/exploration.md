# Exploración: issue #126 — health, login, CI y contrato MySQL

> Estado investigado: rama `develop`, commit `515b0f24`, 2026-07-14. Investigación read-only previa a propuesta. No se diseñó ni implementó la solución.

## Estado actual

La issue mezcla tres deudas operativas vigentes con una afirmación que ya quedó obsoleta. El login conserva el timeout predeterminado de `HttpClient` y no degrada fallos de transporte; API y Web no exponen health/readiness; y el runtime MySQL no tiene un contrato explícito de validación, timeout ni readiness. En cambio, el build frontend **ya está incorporado en CI** con instalación reproducible, auditoría, build y gate de drift de artefactos.

| Frente | Veredicto sobre la issue | Evidencia principal |
|---|---|---|
| Login sin timeout acotado | **Confirmado** | `src/SGV.Web/Program.cs:72-77` no asigna `Timeout`; `SignIn.cshtml.cs:26-71` no captura excepciones de transporte. |
| Health/readiness ausentes | **Confirmado** | Ninguno de los dos `Program.cs` registra o mapea health checks (`SGV.Api:15-167`, `SGV.Web:12-141`); búsqueda de símbolos health = 0 resultados. |
| CI omite frontend | **Corregido por drift** | `.github/workflows/ci.yml:35-54` ya ejecuta Bun, `bun ci`, auditoría, `bun run build` y `git diff --exit-code -- bun.lock wwwroot`. |
| Startup MySQL no documentado | **Parcialmente confirmado** | La separación design-time/runtime sí está documentada (`docs/decisiones-implementacion.md:31-50`), pero no el comportamiento de `AutoDetect`, su presupuesto de conexión ni un contrato de readiness. |

### 1. Login y presupuesto HTTP

#### Registro real de `AuthApiClient`

`SGV.Web` valida primero `SgvApi:BaseUrl` como URI absoluta mediante options + `ValidateOnStart` (`src/SGV.Web/Program.cs:16-21`). Luego registra `IAuthApiClient` como typed client (`Program.cs:72-77`):

- resuelve `IOptions<SgvApiOptions>`;
- configura únicamente `HttpClient.BaseAddress` (`:74-75`);
- no configura headers predeterminados;
- no asigna `HttpClient.Timeout`, por lo que queda el default de plataforma de 100 segundos;
- agrega `ApiBearerTokenHandler` al pipeline (`:77`).

El contraste interno confirma el drift de resiliencia: `CargoApiClient`, `PuestosApiClient` y `HabilidadApiClient` sí fijan 10 segundos (`Program.cs:86-119`), mientras `AuthApiClient` y `UnidadOrganizativaApiClient` no lo hacen (`:72-84`). La factory web de tests también construye el `AuthApiClient` manualmente sin timeout (`tests/SGV.Tests/Web/SgvWebApplicationFactory.cs:89-101`); solo el override de Cargo replica 10 segundos (`:104-119`).

#### Superficie pública y excepciones de `AuthApiClient`

`AuthApiClient` tiene un único método público: `LoginAsync(LoginRequest, CancellationToken)` (`src/SGV.Web/Integration/Auth/AuthApiClient.cs:11-25`; interfaz en `IAuthApiClient.cs:8-16`). Su flujo es:

1. `POST` JSON a la ruta centralizada `AuthApiRoutes.Login` y reenvía el token (`AuthApiClient.cs:14-16`).
2. Un `401 Unauthorized` se traduce a `null` (`:18-21`).
3. Cualquier otro status no exitoso pasa por `EnsureSuccessStatusCode` y lanza `HttpRequestException` (`:23`).
4. Un status exitoso deserializa `LoginResponse` y vuelve a reenviar el token (`:24`).

No hay `try/catch` ni traducción interna. Por contrato vigente, esto es correcto para el cliente: `web-apiclient-transport-contract/spec.md:9-35` exige propagar `HttpRequestException` y `TaskCanceledException` y respetar cancelación cooperativa; `:104-108` exceptúa específicamente el `401` de Auth para devolver `null`. También pueden propagarse cancelación solicitada (`OperationCanceledException`/habitualmente `TaskCanceledException` desde `HttpClient`), errores de deserialización (`JsonException`/`NotSupportedException`) y errores de status. No existe retry/backoff con Polly ni con handlers de resiliencia en `src/`.

#### Flujo completo de `SignInModel.OnPostAsync`

El handler recibe el `CancellationToken` del request (`src/SGV.Web/Pages/Auth/SignIn.cshtml.cs:26`) y:

1. retorna la misma página si `ModelState` es inválido (`:28-31`);
2. crea `LoginRequest` con usuario/email y contraseña (`:33`);
3. invoca `authApiClient.LoginAsync(request, cancellationToken)` (`:34`), conservando cancelación extremo a extremo;
4. ante `null`, agrega `Credenciales inválidas.` al `ModelState` y retorna la página (`:36-40`);
5. rechaza un access token vacío con log warning y mensaje controlado (`:42-47`);
6. valida firma, issuer, audience y lifetime mediante `IAuthSessionFactory` (`:49-66`); solo captura familias de token inválido (`SecurityTokenException` o `ArgumentException`, excluyendo `ArgumentNullException`);
7. crea propiedades, emite la cookie y redirige localmente a `/` (`:68-71`).

Los inputs tienen validaciones `[Required]` con mensajes en español (`:74-81`). La vista renderiza un `validation-summary` `ModelOnly` como alerta (`src/SGV.Web/Pages/Auth/SignIn.cshtml:20-22`) y mantiene labels/copy en español (`:5,16-17,25-37`). Por eso los fallos funcionales controlados permanecen visibles en la misma pantalla.

Hoy `HttpRequestException`, timeout/cancelación y errores JSON ocurren antes del bloque de validación JWT y **escapan del PageModel**. Fuera de Development, el pipeline usa `UseExceptionHandler("/Error")` (`src/SGV.Web/Program.cs:124-129`), pero no existe ninguna Razor Page con ruta `/Error`: las únicas rutas son `/error/400`, `/401`, `/403`, `/404`, `/408`, `/500` y `/maintenance`. Esto es un riesgo adyacente: un fallo de transporte de login no solo abandona el formulario, sino que cae en un handler cuya ruta objetivo no está materializada.

La UX está mezclada: login y sus errores son españoles, directos y específicos; las páginas genéricas 408/500 conservan copy de Inspinia en inglés (`Pages/Error/408.cshtml:39-41`, `500.cshtml:28-30`). Un eventual error recuperable de login debería seguir el lenguaje y el patrón visual de `SignIn`, no copiar el template genérico en inglés.

#### `ApiBearerTokenHandler` en el login

El login **sí atraviesa** `ApiBearerTokenHandler` porque el typed client lo agrega (`Program.cs:72-77`). El handler intenta leer el access token de la cookie y, si existe, agrega `Authorization: Bearer` (`ApiBearerTokenHandler.cs:48-97`). Sin embargo, en el login anónimo normal no hay cookie/token: reenvía el request sin header (`:57-93`). El handler preserva el `CancellationToken` al llamar `base.SendAsync` (`:48-55`). Por tanto, no aporta autenticación al login ni altera su timeout; es un paso inocuo en el camino anónimo.

#### Cancelación y pruebas existentes

La cancelación está ampliamente cableada: la búsqueda estructural encontró 79 métodos de clientes Web, 63 PageModels y 60 actions API con `CancellationToken`. Los clientes administrativos reenvían el token a `GetAsync`, `PostAsJsonAsync`, `PutAsJsonAsync`, `DeleteAsync` y deserialización; el login hace lo mismo (`AuthApiClient.cs:14-24`).

`WebAuthenticationTests` protege ruta centralizada + éxito (`tests/SGV.Tests/Web/WebAuthenticationTests.cs:30-50`), `401 → null` (`:52-65`), error visible por credenciales inválidas (`:81-103`), cookie/redirect exitosos (`:105-131`) y JWT inválido sin cookie (`:133-160`). No hay test de `AuthApiClient`/`SignIn` para `HttpRequestException`, timeout o cancelación. El contrato transversal ya aporta el precedente de que el cliente debe propagar y el consumidor debe decidir la UX.

### 2. Health y readiness

#### API: composición y pipeline actuales

`SGV.Api` registra ProblemDetails y controllers (`src/SGV.Api/Program.cs:17-20`), Swagger (`:21-62`), DbContext/auditoría (`:64-71`), JWT validado al arranque (`:73-100`), servicios de aplicación/infraestructura (`:102-109`) y CORS con fail-loud fuera de Development (`:111-146`). No registra `AddHealthChecks`.

El pipeline usa exception handler + status pages (`:150-152`), Swagger solo en Development (`:154-158`), CORS (`:160`), autenticación/autorización (`:162-163`) y `MapControllers` (`:165`). No mapea `/health` ni `/ready`.

La API tiene fallback authorization global `RequireAuthenticatedUser` (`:97-100`). Cualquier probe futuro mapeado sin metadata explícita podría quedar protegido por esa fallback policy; la propuesta deberá definir conscientemente el contrato anónimo/autenticado de los probes para no entregar `401` al orquestador.

#### Web: composición y pipeline actuales

`SGV.Web` registra Razor Pages (`src/SGV.Web/Program.cs:14-15`), options de API y JWT con `ValidateOnStart` (`:16-28`), cookie auth (`:30-44`), autorización (`:46`), bridge bearer y typed clients (`:48-119`). No registra health checks ni un check de upstream API.

El pipeline usa exception handler/HSTS fuera de Development (`:123-129`), HTTPS redirection (`:131`), routing, auth (`:133-135`), static assets y Razor Pages (`:137-139`). No mapea probes.

#### Controllers y búsqueda de health

Controllers vigentes bajo `src/SGV.Api/Controllers/`:

| Controller | Ruta base |
|---|---|
| `AuthController` | `AuthApiRoutes.Base` (`AuthController.cs:9-12`) |
| `CargosController` | `api/v1/cargos` (`CargosController.cs:15-19`) |
| `NivelesCargoController` | `api/v1/niveles-cargo` (`NivelesCargoController.cs:11-15`) |
| `NivelesHabilidadController` | `api/v1/niveles-habilidad` (`NivelesHabilidadController.cs:12-16`) |
| `OcupacionesController` | `api/v1/ocupaciones` (`OcupacionesController.cs:15-19`) |
| `PersonasController` | `api/v1/personas` (`PersonasController.cs:14-18`) |
| `PuestosController` | `api/v1/puestos` (`PuestosController.cs:15-19`) |
| `SkillsController` | `api/v1/skills` (`SkillsController.cs:16-20`) |
| `TipoUnidadesOrganizativasController` | `api/v1/tipos-unidad-organizativa` (`TipoUnidadesOrganizativasController.cs:8-11`) |
| `UnidadesOrganizativasController` | `api/v1/unidades-organizativas` (`UnidadesOrganizativasController.cs:15-19`) |
| `UsuariosController` | `api/v1/usuarios` (`UsuariosController.cs:10-14`) |

Ninguno es probe. La búsqueda en `src/` y `tests/` no encontró `AddHealthChecks`, `MapHealthChecks`, `IHealthCheck`, `HealthCheckResult`, `HealthStatus`, `HealthCheckOptions`, `ResponseWriter` ni tests contra `/health` o `/ready`.

Tampoco hay probing productivo directo de MySQL: `AuditoriaSaveChangesInterceptor` solo participa en `SaveChanges`; no es inicializador. `Database.CanConnect()` y `Database.Migrate()` existen únicamente en el bootstrap de tests (`tests/SGV.Tests/Persistencia/MySqlTestDatabaseBootstrap.cs:92-108`). No hay `MigrateAsync`, wait-for-MySQL ni retry de startup en `src/`.

#### Factories de integración y testabilidad

- `ApiWebApplicationFactory` reemplaza servicios de aplicación por fakes y el esquema de auth por `Test` (`tests/SGV.Tests/Api/ApiWebApplicationFactory.cs:821-904`), pero no elimina ni reemplaza `SgvDbContext`. Un test de liveness puro puede usar esta factory; uno de readiness MySQL deberá decidir si usa MySQL real/configurado o un override explícito del check para no confundir “host arrancó” con “DB lista”.
- `SgvWebApplicationFactory` permite overrides de servicios y handlers (`tests/SGV.Tests/Web/SgvWebApplicationFactory.cs:18-67,83-145`). Es una base adecuada para simular upstream sano/caído, aunque el override de auth construye `HttpClient` manualmente (`:89-101`) y debe mantenerse alineado con cualquier presupuesto real que se quiera probar.

#### Paquetes health disponibles

No hay referencias `AspNetCore.HealthChecks.*`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` ni package lock/central package file. Los `.csproj` actuales confirman ausencia (`SGV.Api.csproj:8-15`, `SGV.Web.csproj:14-16`, `SGV.Infraestructura.csproj:7-17`).

Opciones estándar para este stack:

- **Primera opción, alineada con EF Core 9**: `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` **9.0.0** + `AddDbContextCheck<SgvDbContext>()`. El nuspec 9.0.0 depende de `Microsoft.EntityFrameworkCore.Relational 9.0.0`, coherente con Pomelo/EF 9 del repo. Se apoya en el DbContext ya registrado y evita un segundo stack de conexión.
- **Alternativa provider-level**: `AspNetCore.HealthChecks.MySql` **9.0.0** (Xabaril) + `AddMySql`. El paquete existe y usa `MySqlConnector`; no existe un paquete `AspNetCore.HealthChecks.Pomelo`. Es útil si se busca probar MySQL directamente sin pasar por EF, a costa de una dependencia adicional y potencial duplicación de configuración.
- El framework ASP.NET Core 10 ya aporta `AddHealthChecks`/`MapHealthChecks`, tags y predicates para separar liveness y readiness. El check de upstream Web→API requerirá una comprobación HTTP acotada, sea custom o mediante una extensión de terceros; hoy no existe ninguna.

#### Binding y despliegue de probes

No hay Dockerfile, Compose, manifiestos Kubernetes, Helm, `web.config`, `readinessProbe` ni `livenessProbe`. Tampoco se documenta `ASPNETCORE_URLS`/`Urls` para deploy. Solo existen perfiles locales:

- API: `https://localhost:7160;http://localhost:5160` (`src/SGV.Api/Properties/launchSettings.json:4-12`).
- Web: `http://localhost:5266` y `https://localhost:7298;http://localhost:5266` (`src/SGV.Web/Properties/launchSettings.json:4-20`).

`docs/decisiones-implementacion.md:244-280` documenta reverse proxy/forwarded headers como pendiente, no un contrato de puertos o probes. La propuesta debe distinguir contrato de endpoint de ejemplos específicos de Kubernetes y no asumir un orquestador inexistente en el repo.

### 3. Build frontend en CI

La afirmación de la issue está desactualizada. `.github/workflows/ci.yml` contiene un único workflow `CI`, disparado por PR y push a `develop`/`main` (`:1-7`), con un único job `build-and-test` en `ubuntu-latest` (`:9-11`). No hay matrix ni caches/cache keys explícitos.

El servicio MySQL está integrado en ese mismo job:

- imagen `mysql:8.0` (`:13-15`);
- `MYSQL_ROOT_PASSWORD=sgv_test_pwd` y `MYSQL_DATABASE=sgv_test` (`:16-18`);
- port `3306:3306` (`:19-20`);
- health command `mysqladmin ping -h localhost`, interval 10 s, timeout 5 s, 10 retries (`:21-25`).

Pasos completos, en orden:

1. `actions/checkout@v4` (`:27-28`).
2. `actions/setup-dotnet@v4`, .NET `10.0.x` (`:30-33`).
3. `oven-sh/setup-bun` pinneado por SHA, Bun `1.3.14` (`:35-38`).
4. `bun ci` en `src/SGV.Web` (`:40-42`).
5. `bun audit --audit-level=high` (`:44-46`).
6. `bun run build` (`:48-50`).
7. `git diff --exit-code -- bun.lock wwwroot` (`:52-54`), que falla si instalación/build altera lock o assets versionados.
8. `dotnet restore` (`:56-57`).
9. `dotnet build --no-restore --configuration Release` (`:59-60`).
10. `dotnet test --no-build --configuration Release --verbosity normal` (`:62-66`) con connection string MySQL y `Jwt__SigningKey` desde secret.

`src/SGV.Web/package.json:32-35` define `build = "gulp build"`. `gulpfile.js:23-25` usa `wwwroot` como fuente y destino; `plugins` copia assets de terceros (`:27-99`), `styles` compila SCSS, autoprefixa y minifica (`:101-126`), y `exports.build` ejecuta `plugins` seguido de `styles` (`:140-144`). Solo existe `.github/workflows/ci.yml`; no hay workflow frontend separado.

**Consecuencia de alcance**: el frente CI no necesita implementación para satisfacer “ejecutar build frontend”. En `sdd-propose` debe tratarse como evidencia de issue parcialmente resuelta, no volver a agregar pasos duplicados. Si se conserva en el cambio, sería solo para documentar/cerrar la discrepancia o para un requisito nuevo explícito, no para “agregar Bun”.

### 4. Contrato de arranque y readiness MySQL

#### Runtime API

`SGV.Api` obtiene `ConnectionStrings:SgvDatabase` sin validar null/whitespace (`src/SGV.Api/Program.cs:64-67`) y registra el contexto con:

```csharp
options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
       .AddInterceptors(...);
```

(`Program.cs:67-71`). No hay options validation, mensaje operativo, timeout explícito, retry ni documentación asociada en ese bloque.

En Pomelo 9.0.0, `ServerVersion.AutoDetect(string)` crea una conexión sin pooling, elimina el database del connection string y llama `Open()` sincrónicamente para leer `ServerVersion`. Por la forma de `AddDbContext`, la lambda de opciones se evalúa al resolver `SgvDbContext`, no necesariamente durante `builder.Build()`: según qué servicio se resuelva, la detección puede golpear MySQL durante startup o quedar diferida hasta el primer request que use DB. Ese comportamiento es precisamente el contrato hoy no explicitado. El tiempo de conexión queda delegado a MySqlConnector/connection string; la configuración runtime no fija `Connection Timeout`.

La cadena de CI sí fija `Default Command Timeout=60`, pero **no** `Connection Timeout` (`.github/workflows/ci.yml:62-65`). Son presupuestos distintos: el primero no acota el `Open()` de AutoDetect. Los `appsettings*.json` versionados no contienen ninguna connection string: `src/SGV.Api/appsettings.Development.json:1-18` solo trae logging, Swagger, origins y el placeholder JWT dev-only; Web solo contiene `SgvApi:BaseUrl` (`src/SGV.Web/appsettings.json:8-11`, `appsettings.Development.json:3-8`).

#### Factory design-time y registro de infraestructura

`SgvDbContextFactory` es **solo design-time** (`src/SGV.Infraestructura/Persistencia/SgvDbContextFactory.cs:8-18`). Construye configuración desde `appsettings.json`, `appsettings.Development.json` y variables de entorno (`:20-25`), valida presencia de `SgvDatabase` y lanza `InvalidOperationException` orientativa si falta (`:27-35`). Usa una versión fija `MySqlServerVersion(8.0.36)` y no AutoDetect (`:37-41`).

`DependencyInjection.AddInfraestructuraServicios` registra detector de constraints, UoW, repositorios y servicios (`src/SGV.Infraestructura/DependencyInjection.cs:22-82`); no registra el DbContext ni un `AddSgvDatabase`. El runtime lo hace directamente en `SGV.Api/Program.cs`. La documentación lo reconoce: factory de tests y design-time separados, runtime por DI estándar (`docs/decisiones-implementacion.md:31-42`).

Hay una inconsistencia documental concreta: `SgvDbContextFactory` aconseja user-secrets “desde `src/SGV.Api`” (`SgvDbContextFactory.cs:10-14,31-34`; docs `:44-50`), pero el factory establece el base path en `Directory.GetCurrentDirectory()` y no llama `AddUserSecrets`; por sí mismo no carga el secret store del proyecto API. Sí lee variables de entorno y archivos presentes en el cwd. Esto no debe mezclarse con el contrato runtime al proponer el cambio.

No existe migración automática productiva. El único `CanConnect + Migrate` vive en tests (`MySqlTestDatabaseBootstrap.cs:92-108`). El test factory fija explícitamente `MySqlServerVersion(8.0.36)` (`TestSgvDbContextFactory.cs:56-64`) y usa defaults con `Connection Timeout=5` (`:42-43`) o stub con un segundo (`:32-33`).

#### Documentación y deployment

`docs/decisiones-implementacion.md:7-9` fija MySQL 8 + Pomelo/EF 9; `:31-50` explica design-time/test/runtime y cómo proporcionar connection string. Falta, sin embargo:

- definir si la API debe arrancar sin MySQL y quedar `NotReady`, o fallar al resolver DB;
- separar liveness de readiness;
- explicitar timeout de conexión para AutoDetect/probe;
- definir si la versión se autodetecta o se fija como MySQL 8;
- definir ownership de migraciones (la app productiva no las ejecuta);
- documentar semántica/status de `/ready` y el check Web→API.

No hay historia de deploy más allá de launch profiles locales y documentación de reverse proxy pendiente. Cualquier ejemplo de Kubernetes/IIS/Docker debe quedar como ejemplo, no como infraestructura asumida.

### 5. Contexto transversal y precedentes

- **Cookie/CORS (#101)** — el change archivado `2026-07-10-endurecer-cookie-cors-deploy` concentra decisiones runtime en los composition roots y usa `WebApplicationFactory` para invariantes por ambiente (`proposal.md:16-45`, `design.md:16-45`). La cookie es `HttpOnly`, `SameSite=Lax`, `SecurePolicy=Always` fuera de Development y `SameAsRequest` en Development. CORS falla loud sin origins fuera de Development. Relevancia para #126: los probes no deben romper ese fail-loud y la fallback auth API debe considerarse; el health no requiere modificar cookie ni bearer bridge.
- **JWT (#97)** — `ValidateOnStart` y `IPostConfigureOptions` establecen el precedente de configuración fail-loud (`archive/.../design.md:20-68`). El timeout de login es ortogonal a firma/validación JWT. La documentación de #126 no debe copiar ni convertir en contrato productivo el placeholder versionado; `docs/decisiones-implementacion.md:56-70` lo marca inequívocamente dev-only.
- **Doc-only (#127)** — `2026-07-13-fix-127-doc-ocupaciones-unicidad-persona` corrigió una afirmación stale sin alterar modelo/spec y la blindó con un test de coherencia (`proposal.md:31-58`, `verify-report.md:3-24`). Es el precedente correcto para el subclaim de CI: no inventar trabajo de código cuando el repo ya cumple.
- **Refactor transversal (#124)** — el archive autoritativo muestra que un cambio inicialmente estimado en 506 LoC terminó en 1310 LoC y requirió `size:exception` (`archive-report.md:13-35,132-143`). Relevancia: health + login + startup docs puede crecer rápido en tests/factories; el proposal debe proteger el presupuesto de 400 líneas y evitar absorber observabilidad, migraciones, proxy headers o deployment manifests.

#### Proyectos y versiones

Todos los nueve `.csproj` apuntan a `net10.0`:

| Proyecto | Paquetes/versiones relevantes |
|---|---|
| `src/SGV.Dominio` | sin paquetes (`SGV.Dominio.csproj:1-16`) |
| `src/SGV.Contracts` | `Microsoft.IdentityModel.Tokens 8.14.0` (`:8-10`) |
| `src/SGV.Aplicacion` | FluentValidation `12.1.1`, EF Core `9.0.0` (`:6-10`) |
| `src/SGV.Infraestructura` | Identity EF `9.0.0`, Pomelo MySQL `9.0.0`, JWT `8.14.0`, EF Design/config `9.0.0` (`:7-17`) |
| `src/SGV.Api` | JwtBearer `10.0.0`, EF Design `9.0.0`, Swashbuckle `7.2.0` (`:8-18`) |
| `src/SGV.Web` | JWT `8.14.0` (`:3-16`) |
| `tests/SGV.Tests` | coverlet `6.0.2`, Test SDK `17.12.0`, xUnit `2.9.2`, runner `2.8.2`, MVC Testing `10.0.0` (`:10-24`) |
| `InspinaTemplate/Starterkit` | sin paquetes (`Starterkit.csproj:1-9`) |
| `InspinaTemplate/Inspinia` | sin paquetes (`Inspinia.csproj:1-9`) |

No hay `Directory.Packages.props`, `Directory.Build.props` ni `packages.lock.json`; las versiones son locales a cada proyecto.

#### Drift, anomalías y cambios en vuelo

- `2026-07-11-fix-active-puesto-id-unique-type` y `2026-07-11-hacer-suite-tests-determinista` tienen `verify-report.md` pero no `archive-report.md`. Parecen pendientes de archivo. Pueden generar conflicto en `tests/`, `docs/decisiones-implementacion.md` y specs de DB/test; #126 no debe incluir ni cerrar ese trabajo.
- Existe la anomalía `openspec/changes/2026-07-13-fix-124-persistence-mapper-reconstitute/` junto a `openspec/changes/archive/2026-07-13-fix-124-persistence-mapper-reconstitute/`. No se tocó. La copia de `archive/` es autoritativa y contiene el `archive-report.md`.
- La baseline refrescada indicaba 146 tests `[MySqlFact]` que se skipean sin MySQL. En el HEAD investigado, el inventario estático encuentra **166 atributos reales** `^[MySqlFact]`; el número efectivo de casos puede diferir de ambos conteos. El contrato sí está confirmado: localmente se omiten solo por servidor inaccesible (`MySqlFactAttribute.cs:17-41`, `MySqlTestDatabaseBootstrap.cs:21-29,111-120`), mientras en CI no se omiten. Todo verify de #126 debe reportar explícitamente si MySQL estuvo disponible y cuántos tests fueron ejecutados/omitidos; sin MySQL, la cobertura de readiness DB es parcial.

## Áreas afectadas

- `src/SGV.Web/Program.cs` — registro y presupuesto del typed client de Auth; eventual registro/mapeo health de Web.
- `src/SGV.Web/Integration/Auth/AuthApiClient.cs` — contrato actual de propagación nativa y cancelación del login.
- `src/SGV.Web/Pages/Auth/SignIn.cshtml.cs` — frontera que hoy no transforma fallos de transporte en UX recuperable.
- `src/SGV.Web/Pages/Auth/SignIn.cshtml` — renderizado de errores del login y lenguaje UX.
- `src/SGV.Web/Integration/Auth/ApiBearerTokenHandler.cs` — participa en el pipeline de login, aunque no agrega bearer al login anónimo.
- `src/SGV.Api/Program.cs` — DbContext/AutoDetect, fallback auth y eventual health/readiness API.
- `src/SGV.Infraestructura/Persistencia/SgvDbContextFactory.cs` — precedente design-time fail-loud y versión fija; no es usado por runtime.
- `src/SGV.Infraestructura/DependencyInjection.cs` — confirma que persistencia runtime no está encapsulada en `AddSgvDatabase`.
- `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` — base de integración API, con servicios fake pero DbContext real no reemplazado.
- `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` — base para simular API upstream y verificar probes/login.
- `tests/SGV.Tests/Web/WebAuthenticationTests.cs` — cobertura actual de login y gap de transporte/timeout.
- `src/SGV.Api/SGV.Api.csproj` / `src/SGV.Infraestructura/SGV.Infraestructura.csproj` — posible dependencia health EF/MySQL, según enfoque.
- `docs/decisiones-implementacion.md` y `AGENTS.md` — contrato operativo de MySQL, probes y secretos sin filtrar el placeholder dev a producción.
- `.github/workflows/ci.yml` — **evidencia**, no cambio necesario para frontend; ya contiene el gate completo.

## Enfoques

1. **Capacidades first-party y alcance mínimo** — Mantener la propagación nativa en `AuthApiClient`, acotar su typed client y manejar el fallo recuperable en la frontera Razor; usar health checks built-in con `AddDbContextCheck<SgvDbContext>` para readiness API, un check HTTP acotado para readiness Web y liveness sin dependencias; explicitar/validar la configuración MySQL y documentar el contrato. No modificar CI salvo documentación de la discrepancia.
   - Pros: coherente con EF Core 9 y patrones `ValidateOnStart`; menos dependencias; separa liveness/readiness; evita duplicar el trabajo frontend ya presente.
   - Contras: el check upstream Web requiere una implementación pequeña y decisiones sobre anonimato/status; debe distinguir cancelación del request de timeout propio.
   - Esfuerzo: Medio.

2. **Probe MySQL provider-level + contrato operativo más activo** — Usar `AspNetCore.HealthChecks.MySql` para conexión directa, mantener AutoDetect pero exigir timeout explícito y documentar/reintentar de forma acotada o delegar espera al orquestador; check upstream separado en Web.
   - Pros: prueba MySQL sin depender del modelo EF; telemetría de dependencia más directa.
   - Contras: agrega paquete/segundo camino de conexión, duplica configuración y aumenta riesgo de divergencia; retries/startup pueden ampliar alcance y latencia; no resuelve por sí mismo la UX de login.
   - Esfuerzo: Medio-Alto.

## Recomendación

Llevar a `sdd-propose` el enfoque 1, manteniendo cuatro decisiones de alcance: (a) CI frontend se declara ya resuelto y no se duplica; (b) liveness no depende de MySQL/API upstream y readiness sí; (c) `AuthApiClient` sigue propagando excepciones nativas, mientras `SignInModel` es la frontera de UX; (d) el contrato runtime MySQL se valida/documenta por separado del factory design-time. La propuesta debe definir explícitamente anonimato de probes frente al fallback policy API y ejecutar pruebas DB con MySQL real.

## Riesgos

- Los endpoints health API pueden responder `401` si se omite metadata compatible con la fallback policy global.
- Un readiness check que resuelva `SgvDbContext` también activa `ServerVersion.AutoDetect`; sin corregir su presupuesto/configuración puede convertir el probe en el mismo bloqueo que intenta detectar.
- Capturar toda `TaskCanceledException` como timeout puede ocultar cancelación por desconexión del cliente; la propuesta debe distinguir semánticas sin romper el contrato transversal.
- Agregar retries automáticos al POST de login o al startup MySQL puede causar trabajo duplicado/arranques lentos; no existe precedente de retry en el repo.
- `UseExceptionHandler("/Error")` apunta a una ruta inexistente; es un hallazgo adyacente que debe declararse non-goal o resolverse conscientemente para evitar scope creep.
- El placeholder JWT dev existe en el repo pero NO debe aparecer como valor productivo en documentación de operación.
- Los dos changes con verify sin archive y la anomalía duplicada #124 elevan el riesgo de conflictos documentales/SDD.
- La suite sin MySQL brinda cobertura parcial; el conteo cacheado de 146 ya presenta drift frente al inventario estático de 166.
- Health + login + docs + factories puede superar 400 líneas; #124 demuestra que el forecast transversal se subestima con facilidad.

## Listo para propuesta

**Sí.** El orchestrator puede indicar que la investigación confirmó login, health/readiness y contrato runtime MySQL como deuda vigente, pero corrigió la premisa de CI: Bun/build/gate ya existen. Próximo paso recomendado: `sdd-propose 2026-07-14-fix-126-operational-tech-debt`, con CI fuera del scope de implementación y verificación MySQL real obligatoria para declarar cobertura completa.
