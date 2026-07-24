# Propuesta: `2026-07-14-fix-126-operational-tech-debt`

> Issue: [#126 — Operación: faltan health/readiness, timeout de login y build frontend en CI](https://github.com/elflacoseba/SGV/issues/126)
> Exploración: `openspec/changes/2026-07-14-fix-126-operational-tech-debt/exploration.md` (rama `develop`, commit `515b0f24`).
> Modo de artefactos: híbrido (filesystem + Engram). TDD estricto activo (`openspec/config.yaml:11`).

## 1. Resumen

Este cambio cierra tres deudas operativas vigentes detectadas por la issue #126 y corrige una premisa obsoleta: (a) login sin timeout acotado ni manejo de fallos de transporte recuperables, (b) ausencia total de health/readiness en `SGV.Api` y `SGV.Web`, y (c) contrato runtime MySQL sin validar ni documentar (timeout, `AutoDetect`, readiness). La afirmación sobre "CI sin build frontend" queda descartada por la exploración con evidencia: `.github/workflows/ci.yml:35-54` ya ejecuta Bun, auditoría, `bun run build` y el gate `git diff --exit-code -- bun.lock wwwroot`. El user-visible outcome es: login falla rápido (<10 s) con mensaje de error en español cuando la API no responde o se demora; orquestadores pueden distinguir liveness y readiness vía `/health/live` y `/health/ready` anónimos; la API falla loud al startup si la connection string MySQL falta o es inválida.

## 2. Motivación

La issue #126 mezcla frentes que el repositorio no resuelve:

- **Operadores / orquestadores sin señal de readiness.** Hoy, si MySQL está caído, la API puede arrancar y entregar errores 5xx por cada request hasta que EF intente `Open()`. No hay `/health/ready` para que un orquestador retire la instancia del balanceo. La separación liveness/readiness está parcialmente documentada pero nunca aplicada (`exploration.md:11-14`).
- **Login espera hasta 100 s antes de fallar.** `AuthApiClient` y `UnidadOrganizativaApiClient` usan el `Timeout` por defecto de `HttpClient` (100 s), mientras `CargoApiClient`, `PuestosApiClient` y `HabilidadApiClient` ya están alineados a 10 s (`src/SGV.Web/Program.cs:72-119`). Además, `SignInModel.OnPostAsync` no captura `HttpRequestException` ni `TaskCanceledException` (`src/SGV.Web/Pages/Auth/SignIn.cshtml.cs:26-71`), por lo que el error escala al pipeline global. Esto es un dolor operativo real: la cookie de auth la emite `SGV.Web`, no la API, y la cancelación efectiva depende del navegador del usuario, que puede esperar casi dos minutos sin señal.
- **Contrato runtime MySQL no explicitado.** `ServerVersion.AutoDetect(connectionString)` corre con el presupuesto de conexión de MySqlConnector sin `Connection Timeout` explícito (`src/SGV.Api/Program.cs:64-71`). Si la DB no responde al arranque, la falla es silenciosa o tardía, y `docs/decisiones-implementacion.md:31-50` no define qué esperar.

La exploración (`exploration.md`) descartó la cuarta afirmación de la issue: el build frontend en CI ya está implementado y guardado por el gate de drift. Tratar esa parte como implementación sería trabajo duplicado.

## 3. Alcance

### 3.1 In-scope

- **A. Timeout de `AuthApiClient`/`UnidadOrganizativaApiClient`.** Edición quirúrgica de `src/SGV.Web/Program.cs:72-84` para asignar `Timeout = TimeSpan.FromSeconds(10)` en ambos typed clients, alineándolos con `CargoApiClient`, `PuestosApiClient` y `HabilidadApiClient` (`:86-119`). El override equivalente en `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs:89-101` también debe reflejar el nuevo presupuesto.
- **B. UX frontera login.** En `src/SGV.Web/Pages/Auth/SignIn.cshtml.cs:34` envolver `authApiClient.LoginAsync(...)` en `try/catch` para `HttpRequestException` (transporte caído) y `TaskCanceledException` solo cuando **no** venga del `cancellationToken` del request (distinción semántica obligatoria: respetar `web-apiclient-transport-contract/spec.md:104-108`). Cada rama agrega `ModelState.AddModelError(string.Empty, mensaje)` en español y retorna la página sin redirigir. Conservar el patrón visual actual (`SignIn.cshtml:20-22`): un único `validation-summary` `ModelOnly`.
- **C. Health checks.**
  - **API** (`src/SGV.Api/Program.cs`): registrar `AddHealthChecks().AddDbContextCheck<SgvDbContext>(name: "mysql", tags: new[] { "ready" })` y mapear `/health/live` (predicate `tags.Contains("live")`, equivalente a liveness sin DB) + `/health/ready` (predicate `tags.Contains("ready")`). Marcar ambos endpoints con `.AllowAnonymous()` para evitar la fallback policy global (`Program.cs:97-100`). Dependencia nueva: `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore 9.0.0` (compatible con EF Core `9.0.0` ya presente, `exploration.md:118`).
  - **Web** (`src/SGV.Web/Program.cs`): nuevo `IHealthCheck` en `src/SGV.Web/Integration/Health/SgvApiUpstreamHealthCheck.cs` que pega a `<SgvApi:BaseUrl>/health/live` con `HttpClient.Timeout = TimeSpan.FromSeconds(3)` propio, respeta `cancellationToken`, y reporta `Healthy`/`Unhealthy` con `Exception` capturada. Mapear `/health/live` (liveness sin upstream) + `/health/ready` (incluye el check upstream) también `.AllowAnonymous()`.
- **D. Validación connection string MySQL.** En `src/SGV.Api/Program.cs:64-67` leer `ConnectionStrings:SgvDatabase` y, si falta o no es absoluta, lanzar `OptionsValidationException` (o equivalente por `IValidateOptions<SgvDbContextOptions>`) con mensaje claro antes de `UseMySql(...)`. Coherente con el precedente `ValidateOnStart` de JWT (`SGV.Web/Program.cs:16-28`) y de `SgvApi:BaseUrl` (`SGV.Web/Program.cs:16-21`). Documentar el timeout recomendado en `appsettings*.json` versionados sin filtrar el placeholder dev.
- **E. Documentación operativa MySQL.** Extender `docs/decisiones-implementacion.md` (subsección tras `:31-50`) con el contrato: liveness vs readiness, `Connection Timeout` sugerido para AutoDetect, semántica status de `/ready`, separación runtime vs design-time factory, dónde colocar la connection string por ambiente.

### 3.2 Out-of-scope (non-goals)

| Non-goal | Justificación |
|---|---|
| Build frontend en CI | Ya implementado: `.github/workflows/ci.yml:35-54`. Drift = regression guard, no nueva tarea. |
| Fix `UseExceptionHandler("/Error")` → ruta inexistente | Hallazgo adyacente (`exploration.md:55`); absorbe trabajo ajeno sin contract explícito. Si se decide, su propio change. |
| Retry/backoff automático en login o startup MySQL | Sin precedente en el repo; riesgo de arranques lentos o trabajo duplicado. |
| Manifiestos Docker / Kubernetes / IIS / Helm | No existen en el repo; contrato de endpoint, no ejemplos de orquestador. |
| Migraciones automáticas al startup | Siguen siendo operacionales; este cambio solo documenta el contrato, no las ejecuta. |
| Cambios a cookie/CORS (#101) o JWT (#97) | Ortogonales; cualquier interacción se delega a los changes archivados. |
| Cambiar `AutoDetect` → `MySqlServerVersion(8.0.36)` fijo en runtime | El design-time ya fija versión (`SgvDbContextFactory.cs:37-41`); cambiar runtime requiere evidencia separada. |
| Reescribir pipeline frontend / `package.json` / `gulpfile.js` | No es deuda vigente; mantener como está. |
| Modificar `SgvDbContextFactory` design-time | Ya tiene contrato fail-loud; no se toca. |
| Páginas de error Inspinia en español | Ortogonal al contrato de login; otro change. |
| Documentar el placeholder JWT dev como productivo | Prohibido: el placeholder es dev-only (`decisiones-implementacion.md:56-70`). |

## 4. Criterios de aceptación (verificables)

| ID | Criterio | Cobertura esperada |
|---|---|---|
| AC-1 | `AuthApiClient` y `UnidadOrganizativaApiClient` se registran con `Timeout = TimeSpan.FromSeconds(10)` en Web. | Test unitario sobre los `HttpClient` resultantes (leer `Timeout`) o test de integración con `SgvWebApplicationFactory` y handler demorado que verifique `TaskCanceledException` antes de 10 s ± tolerancia. |
| AC-2 | `SignInModel.OnPostAsync` agrega mensaje en español a `ModelState` cuando la API upstream devuelve `HttpRequestException`; no propaga la excepción al pipeline `UseExceptionHandler`. | Test `SignInTransportTests` con fake client que lanza `HttpRequestException`. |
| AC-3 | `SignInModel.OnPostAsync` agrega mensaje en español cuando la API upstream excede el timeout; no propaga `TaskCanceledException` cuando el `cancellationToken` del request no está cancelado. | Test con fake client que lanza `TaskCanceledException` y un `CancellationToken` no cancelado. |
| AC-4 | `GET /health/live` en API responde `200` anónimo y no requiere MySQL. | Test `Api/HealthTests` con `ApiWebApplicationFactory` (DbContext real o stub según el caso). |
| AC-5 | `GET /health/ready` en API responde `200` cuando MySQL responde a `CanConnectAsync`; responde `503` con cuerpo JSON de health cuando MySQL está caído. | `[MySqlFact]` para caso real + test con DbContext deprecado a `Unhealthy` para caso de fallo. |
| AC-6 | `GET /health/live` en Web responde `200` anónimo. | Test `Web/HealthTests` con `SgvWebApplicationFactory`. |
| AC-7 | `GET /health/ready` en Web responde `200` cuando el upstream responde `200` en `<3 s`; `503` cuando upstream no responde dentro del budget. | Tests parametrizados con upstream sano / caído / lento. |
| AC-8 | API falla loud (lanza y aborta host) si `ConnectionStrings:SgvDatabase` falta o es inválida al startup, con mensaje operativo claro. | Test que configure el host con connection string vacía y verifique `OptionsValidationException` o equivalente. |
| AC-9 | `docs/decisiones-implementacion.md` documenta el contrato runtime MySQL (liveness, readiness, timeout, AutoDetect, separación design-time/runtime) en una subsección identificada. | Verificación manual sobre el artefacto al cierre del change. |
| AC-10 | Suite completa `dotnet test SGV.slnx --configuration Release` pasa con MySQL real en CI; el verify-report declara explícitamente cuántos `[MySqlFact]` se ejecutaron vs cuántos se omitieron (drift cacheado 146 vs estático 166, `exploration.md:228`). | `verify-report.md` con conteo final y entorno. |
| AC-11 | `bun run build` + `git diff --exit-code -- bun.lock wwwroot` siguen pasando en CI. | Regression guard ya existente; el verify-report lo incluye como evidencia. |

## 5. Diseño de alto nivel

- **A. Timeout login** — Edición puntual en `Program.cs:72-84`:
  ```csharp
  services.AddHttpClient<IAuthApiClient, AuthApiClient>((sp, client) =>
  {
      var opts = sp.GetRequiredService<IOptions<SgvApiOptions>>().Value;
      client.BaseAddress = new Uri(opts.BaseUrl);
      client.Timeout = TimeSpan.FromSeconds(10); // nuevo, alineado con Cargo/Puestos/Habilidad
  }).AddHttpMessageHandler<ApiBearerTokenHandler>();
  ```
  Mismo patrón para `UnidadOrganizativaApiClient`. Tests web factory se actualizan en consecuencia.

- **B. UX frontera login** — En `SignInModel.OnPostAsync:34`:
  ```csharp
  try
  {
      var response = await _authApiClient.LoginAsync(request, cancellationToken);
      // resto del flujo existente
  }
  catch (HttpRequestException ex)
  {
      _logger.LogWarning(ex, "Fallo de transporte al autenticar contra la API");
      ModelState.AddModelError(string.Empty,
          "No pudimos contactar al servicio de autenticación. Intentá nuevamente en unos minutos.");
      return Page();
  }
  catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
  {
      _logger.LogWarning("Timeout al autenticar contra la API");
      ModelState.AddModelError(string.Empty,
          "La autenticación tardó demasiado. Intentá nuevamente.");
      return Page();
  }
  ```
  La guarda `when (!cancellationToken.IsCancellationRequested)` preserva el contrato del spec transversal (`web-apiclient-transport-contract/spec.md:104-108`).

- **C. Health API** — `SGV.Api/Program.cs`:
  ```csharp
  builder.Services.AddHealthChecks()
      .AddDbContextCheck<SgvDbContext>(name: "mysql", tags: new[] { "ready" });

  // ... resto del pipeline ...

  app.MapHealthChecks("/health/live", new HealthCheckOptions
  {
      Predicate = _ => true, // liveness: solo proceso vivo, sin tag "ready"
  }).AllowAnonymous();
  app.MapHealthChecks("/health/ready", new HealthCheckOptions
  {
      Predicate = check => check.Tags.Contains("ready"),
      ResponseWriter = HealthCheckResponseWriter.WriteJson, // wrapper ASP.NET por defecto
  }).AllowAnonymous();
  ```
  Paquete nuevo solo en `SGV.Api.csproj`. El check MySQL reusa `SgvDbContext` ya registrado.

- **D. Health Web upstream** — `src/SGV.Web/Integration/Health/SgvApiUpstreamHealthCheck.cs`:
  ```csharp
  public sealed class SgvApiUpstreamHealthCheck : IHealthCheck
  {
      private readonly SgvApiOptions _opts;
      public SgvApiUpstreamHealthCheck(IOptions<SgvApiOptions> opts) => _opts = opts.Value;
      public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
      {
          try
          {
              using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
              using var resp = await client.GetAsync(new Uri(new Uri(_opts.BaseUrl), "/health/live"), ct);
              return resp.IsSuccessStatusCode ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy($"Upstream {resp.StatusCode}");
          }
          catch (Exception ex) { return HealthCheckResult.Unhealthy(ex.Message, ex); }
      }
  }
  ```
  Registro en `SGV.Web/Program.cs` con tag `"ready"`; mapeo idéntico al API con `.AllowAnonymous()`.

- **E. Validación MySQL** — `SGV.Api/Program.cs:64-71` antes de `AddDbContext`:
  ```csharp
  var connectionString = builder.Configuration.GetConnectionString("SgvDatabase");
  if (string.IsNullOrWhiteSpace(connectionString))
      throw new OptionsValidationException(
          "ConnectionStrings:SgvDatabase",
          typeof(string),
          new[] { "Debe configurar ConnectionStrings:SgvDatabase antes de iniciar la API." });

  builder.Services.AddDbContext<SgvDbContext>(options =>
      options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
             .AddInterceptors(...));
  ```
  Mismo patrón que `ValidateOnStart` (JWT y SgvApiOptions); fail-loud al construir el host.

- **F. Documentación** — Nueva subsección en `docs/decisiones-implementacion.md` después de `:50`:
  - Liveness: `GET /health/live`, no requiere DB ni upstream, siempre verde si el proceso responde.
  - Readiness: `GET /health/ready`, requiere MySQL en API y API viva desde Web. `503` con `status: Unhealthy` ante fallo.
  - AutoDetect: presupuesto controlado por `Connection Timeout` (sugerir 5–10 s en connection string productiva); no reintentar.
  - Migraciones: no se ejecutan al startup; corren fuera de banda.
  - Secrets: connection string productiva por env var o user-secrets del proyecto API; nunca commitear.

## 6. Riesgos

- **Regresión en login:** los cambios en `SignIn.cshtml.cs` pueden alterar contrato de `WebAuthenticationTests`. Actualizar/agregar tests para tiempo/transporte antes de cualquier despliegue.
- **Readiness con AutoDetect bloqueante:** `AddDbContextCheck<SgvDbContext>` resuelve el contexto por primera vez, lo que dispara `ServerVersion.AutoDetect` si no fue resuelto antes; sin budget explícito en connection string, el probe puede colgar. Mitigar con `Connection Timeout=5` documentado y `appsettings` de ejemplo.
- **503 espurios:** latencia transitoria en MySQL marca `Unhealthy`. Documentar el budget y el comportamiento esperado, sin agregar retry automático en este change.
- **Drift de cobertura MySQL:** 146 cacheados vs 166 reales estáticos. Verificar MySQL real en CI y declarar conteo explícito en `verify-report.md`.
- **Scope creep por `UseExceptionHandler("/Error")`:** mantenerlo non-goal. Si se observa durante apply, escalarlo como propuesta separada.
- **Conflicto documental con `2026-07-11-*` in-flight** (tienen `verify-report.md` sin `archive-report.md`): coordinar antes de tocar `docs/decisiones-implementacion.md` o specs transversales.
- **Presupuesto de revisión:** #124 terminó en 1310 LoC con `size:exception`. Si el forecast de `tasks.md` supera 400 LoC netas, dividir en chained PRs (`/health` por un lado, login + validación MySQL por otro).

## 7. Dependencias

- **Paquete NuGet nuevo:** `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore 9.0.0` (compatible con `Microsoft.EntityFrameworkCore.Relational 9.0.0` ya presente en `SGV.Infraestructura.csproj:7-17`).
- Decisiones vigentes que **se mantienen** (no se tocan): cookie/CORS endurecido (#101), JWT fail-loud con `ValidateOnStart` (#97), `SGV.Web → SGV.Contracts` wire-types, Reconstitute mapper (#124), spec transversal `web-apiclient-transport-contract/spec.md` para el manejo de excepciones en `AuthApiClient`/`SignInModel`, doc Ocupaciones (#127), errores `CommandResult` (#125) diferido.
- Sin dependencias de otros repositorios.
- Sin migraciones EF nuevas en este change.

## 8. Preguntas abiertas

1. ¿El usuario quiere alinear `UnidadOrganizativaApiClient` a 10 s en este mismo change (recomendado) o dejarlo para otra iteración?
2. ¿Prefiere `AllowAnonymous()` explícito por endpoint (recomendado, scope limitado) o relajar la fallback policy global de la API? La primera opción no toca #97.
3. ¿Mensaje exacto en español para transporte caído y timeout? Sugerencias:
   - Transporte caído: "No pudimos contactar al servicio de autenticación. Intentá nuevamente en unos minutos."
   - Timeout: "La autenticación tardó demasiado. Intentá nuevamente."
4. ¿El health upstream en Web debe pegar a `/health/live` (recomendado, ya implica proceso vivo upstream) o a un endpoint dedicado más liviano?
5. ¿`AddDbContextCheck` es suficiente o se prefiere una variante que use `CanConnectAsync` explícito? La primera opción es la nativa de EF; la segunda permite budget propio.

## 9. Cómo se prueba

- **Unit / integración web:**
  - `tests/SGV.Tests/Web/AuthApiClientTimeoutTests.cs` — verifica `Timeout = 10 s` y cancelación cooperativa.
  - `tests/SGV.Tests/Web/SignInTransportTests.cs` — upstream `HttpRequestException` y timeout (`TaskCanceledException` no ligada a `requestAborted`).
  - `tests/SGV.Tests/Web/HealthTests.cs` — `SgvWebApplicationFactory` con upstream sano/caído/lento, verifica `/health/live`, `/health/ready`, anonimato y respuesta JSON.

- **Unit / integración API:**
  - `tests/SGV.Tests/Api/HealthTests.cs` con `[MySqlFact]` para readiness real contra `sgv_test`, y test con DbContext deprecado a `Unhealthy` para la rama 503.
  - Test nuevo: arrancar host con `ConnectionStrings:SgvDatabase` vacío y verificar `OptionsValidationException` al `Build()`.

- **Auth web smoke tests existentes** (`WebAuthenticationTests`) — agregar escenarios transporte y actualizar si la firma del mensaje cambia.

- **MySQL en CI:** la pipeline ya levanta `mysql:8.0` (`.github/workflows/ci.yml:13-25`). `verify-report.md` debe declarar conteo final `[MySqlFact]` ejecutados vs omitidos.

- **Frontend regression guard:** `bun run build` + `git diff --exit-code -- bun.lock wwwroot` (ya existe en CI; verificar que sigue verde tras el change).

- **Comandos:**
  - `dotnet test SGV.slnx --configuration Release`
  - `bun install && bun run build` dentro de `src/SGV.Web`
