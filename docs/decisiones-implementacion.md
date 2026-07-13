# Decisiones de Implementación

## SDK y Target Framework

Los proyectos apuntan a `net10.0` (.NET 10). El archivo `global.json` fija el SDK en `10.0.300` con roll-forward `latestMajor` para permitir compatibilidad con versiones posteriores del SDK 10.x.

## Proveedor de Base de Datos

Se utiliza Pomelo Entity Framework Core 9.x como proveedor único para MySQL 8. Los paquetes `Microsoft.EntityFrameworkCore*`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore` y `Pomelo.EntityFrameworkCore.MySql` permanecen en versiones 9.x porque Pomelo 9 depende de EF Core relational `>= 9.0.0 && < 9.0.999`. SQL Server no se soporta como proveedor activo.

## Índices Únicos con Soft Delete

MySQL no soporta índices filtrados como SQL Server. Para preservar las reglas de unicidad sobre registros activos (no eliminados), se utilizan columnas generadas (computed columns) con índices únicos. La columna generada devuelve el valor de la columna de negocio cuando el registro está activo (`IsDeleted = 0`) y `NULL` cuando está eliminado. MySQL permite múltiples `NULL` en índices únicos, lo que replica el comportamiento de los índices filtrados de SQL Server.

## Identity

Se mantiene `IdentityUser` con clave string, por lo que las columnas de auditoría que referencian usuarios usan `varchar(450)`. Esta decisión conserva el comportamiento estándar de ASP.NET Core Identity y evita personalización prematura.

## Ocupaciones Activas

La versión inicial aplica una única ocupación vigente por puesto y una única ocupación vigente por persona mediante columnas generadas con índices únicos. Si el negocio requiere cargos concurrentes, se deberá agregar tipo de ocupación o porcentaje de dedicación.

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

