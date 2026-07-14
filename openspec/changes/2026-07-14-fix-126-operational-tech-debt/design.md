# Diseño: `2026-07-14-fix-126-operational-tech-debt` — corrección

> Issue: #126 — health/readiness, timeout de login, contrato runtime MySQL.
> Estado: proposal + exploration + 4 spec deltas aprobados. TDD estricto (`openspec/config.yaml:11`). Modo `hybrid`. Rama `develop`, HEAD `e672912c`.
> Artefactos previos: `proposal.md`, `exploration.md`, `specs/operational-readiness/spec.md`, `specs/web-apiclient-transport-contract/spec.md`, `specs/sgv-web-authentication/spec.md`. **Nuevo**: `specs/sgv-readonly-api/spec.md` (resuelve conflicto con default-deny).

## 1. Resumen arquitectónico

El cambio cierra tres deudas operativas con superficie acotada a `Program.cs` de `SGV.Api`/`SGV.Web`, dos nuevos archivos en `SGV.Api/Infrastructure/Health/` (`SgvDbContextReadinessHealthCheck.cs`, `HealthCheckResponseWriter.cs`), uno en `SGV.Web/Integration/Health/` (`SgvApiUpstreamHealthCheck.cs`), un try/catch en `SignInModel.OnPostAsync`, validación diferida de `ConnectionStrings:SgvDatabase` vía `IValidateOptions<SgvDbContextOptions>`, y documentación operativa. Sin dependencias NuGet nuevas (la librería `HealthChecks.EntityFrameworkCore` quedaba acoplada a `AddDbContextCheck`, reemplazado por `IHealthCheck` propio). No se tocan dominio, aplicación, contratos, infraestructura ni migraciones EF.

Flujo runtime tras el cambio:

```
Orquestador               SGV.Web                          SGV.Api                                MySQL
   │  GET /health/live       │                                │                                       │
   ├────────────────────────►│  200 (predicate=false)         │                                       │
   │  GET /health/ready      │                                │                                       │
   ├────────────────────────►│  SgvApiUpstreamHealthCheck     │                                       │
   │                         │  GET /health/live (≤3s) ──────►│  SgvDbContextReadinessHealthCheck     │
   │                         │  200 / 503                     │  CanConnectAsync(ct) ────────────────►│
   │                         │                                │                                       │
   │  POST /auth/sign-in     │  AuthApiClient (10s timeout)   │  POST /api/v1/auth/login              │
   ├────────────────────────►├───────────────────────────────►├──────────────────────────────────────►│
```

## 2. Decisiones arquitectónicas

| ID | Decisión | Alternativa | Rationale | Consecuencia |
|---|---|---|---|---|
| ADR-01 | `IHealthCheck` propio `SgvDbContextReadinessHealthCheck` con `IConfiguration` + `MySqlConnector.MySqlConnection.OpenAsync(ct)` dentro de try/catch | `AddDbContextCheck<SgvDbContext>` (paquete `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore 9.0.0`) y versión previa con `IDbContextFactory<SgvDbContext>.CanConnectAsync` | `AddDbContextCheck` resuelve el contexto (dispara `ServerVersion.AutoDetect` en primera resolución) ANTES de correr el cuerpo del check; con MySQL caído puede colgar 15-30 s o devolver 500. La versión con `IDbContextFactory` compartía opciones DI con `AddDbContext` y provocaba `Cannot resolve scoped service from root provider`. El check con conexión crusa evita EF Core DI por completo para el probe | Sin NuGet nuevo (`MySqlConnector` es transitivo vía Pomelo). `Connection Timeout` en connection string productiva DEBE ser ≤5 s (sección 4.F); AutoDetect queda diferido al primer request real — se documenta en §6 |
| ADR-02 | `/health/live` ≠ `/health/ready` (tag-based + predicate) | Un solo endpoint agregador | Semántica esperada por orquestadores (Kubernetes, IIS, Docker) | Doble endpoint, doble mapping; predicado por tags |
| ADR-03 | `.AllowAnonymous()` por endpoint de health; fallback policy `RequireAuthenticatedUser` intacta | Relajar fallback policy global | Scope limitado, no debilita el resto de la API. Coherente con delta a `sgv-readonly-api` (§4.G) | Las dos rutas API deben declararse explícitamente `AllowAnonymous`. La spec transversal `sgv-readonly-api` se delega explícitamente este delta |
| ADR-04 | `Timeout = TimeSpan.FromSeconds(10)` en `AuthApiClient` y `UnidadOrganizativaApiClient` | Dejar 100 s default | Consistencia con `CargoApiClient`, `PuestosApiClient`, `HabilidadApiClient` (`SGV.Web/Program.cs:86-119`) | El login falla rápido y `TaskCanceledException` se mapea a UX en español |
| ADR-05 | Try/catch de transporte en `SignInModel.OnPostAsync`, NO en `AuthApiClient` | Capturar y traducir en el cliente | Coherente con `web-apiclient-transport-contract/spec.md:50-82` (cliente propaga, consumidor decide UX) | `AuthApiClient` se mantiene minimal |
| ADR-06 | Validación diferida vía `IValidateOptions<SgvDbContextOptions>` registrado como `IValidateOptions<DbContextOptions<SgvDbContext>>` y enlazado al host (`ValidateOnStart`); valida null/whitespace, formato mínimo (`Server=`, `Database=`), warning de `Connection Timeout` ausente | Throw temprano en `Program.cs` antes de `AddDbContext` | El throw temprano corre antes de que `WebApplicationFactory.ConfigureAppConfiguration` aplique overrides, y no captura connection strings malformadas | El host corre pero `Build()`/primer resolve del DbContext dispara el validador; mensaje cita la clave y la causa |
| ADR-07 | `IHttpClientFactory` con named client `SgvApiHealthProbe` (`Timeout = 3 s`, sin `ApiBearerTokenHandler`); rethrow de `OperationCanceledException` cuando `ct.IsCancellationRequested`, `Unhealthy("Upstream timeout")` cuando `TaskCanceledException` interno por timeout, `Unhealthy(message)` para `HttpRequestException` | `new HttpClient()` inline (sin seam) | Testeable vía `IHttpClientFactory` mockeado o `DelegatingHandler` en el named client; no contamina el bridge `ApiBearerTokenHandler`; respeta cancelación cooperativa | Nuevo archivo `src/SGV.Web/Integration/Health/SgvApiUpstreamHealthCheck.cs`; registro del named client en `SGV.Web/Program.cs` |
| ADR-08 | `HealthCheckResponseWriter.WriteJson` estático compartido entre API y Web; `Content-Type: application/json` explícito; DTO sin `Exception` ni stack trace (usa `description` sanitizada, max ~200 chars); usa `ResultStatusCodes` default (200 Healthy/Degraded, 503 Unhealthy) | `WriteAsJsonAsync(report)` inline (puede serializar `Exception`) | Evita fugas de stack trace y serialización inválida; contrato JSON estable entre API y Web | Nuevo archivo `src/SGV.Api/Infrastructure/Health/HealthCheckResponseWriter.cs`; Web reusa el mismo writer (referencia `SGV.Contracts` no aplica; ver §3 sobre accesibilidad) |

## 3. Estructura de archivos

```
openspec/changes/2026-07-14-fix-126-operational-tech-debt/
├── (existing) exploration.md, proposal.md, design.md (este)
├── specs/
│   ├── (existing) operational-readiness/spec.md
│   ├── (existing) web-apiclient-transport-contract/spec.md
│   ├── (existing) sgv-web-authentication/spec.md
│   └── sgv-readonly-api/spec.md                          ← NUEVO (resuelve §4.G)

src/SGV.Api/
├── Infrastructure/Health/
│   ├── SgvDbContextReadinessHealthCheck.cs               (nuevo: ADR-01)
│   └── HealthCheckResponseWriter.cs                      (nuevo: ADR-08)
├── Program.cs                                            (modificado: AddHealthChecks + MapHealthChecks + options validator)
└── SGV.Api.csproj                                        (sin cambios — sin NuGet nuevo)

src/SGV.Web/
├── Integration/Health/SgvApiUpstreamHealthCheck.cs       (nuevo: ADR-07; usa IHttpClientFactory named client)
├── Program.cs                                            (modificado: AddHttpClient named "SgvApiHealthProbe" + AddHealthChecks + MapHealthChecks + Timeout 10s en Auth/Unidad en :72-84)
└── Pages/Auth/SignIn.cshtml.cs                           (modificado: try/catch OnPostAsync usando parámetro `logger`)

tests/SGV.Tests/
├── Api/HealthTests.cs                                    (nuevo: liveness + readiness; readiness usa connection string inválida para simular DB caída)
├── Api/StartupValidationTests.cs                         (nuevo: 4 escenarios conexión — §4.E)
├── Api/ApiWebApplicationFactory.cs                       (modificado: helper `configureConfig` opcional para tests; conexión válida por defecto)
├── Web/AuthApiClientTimeoutTests.cs                      (nuevo: Timeout=10s con TaskCompletionSource, NO Task.Delay)
├── Web/SignInTransportTests.cs                           (nuevo: HttpRequestException, TaskCanceledException, cancelación cooperativa)
├── Web/HealthTests.cs                                    (nuevo: /health/live + /health/ready con fake DelegatingHandler)
└── Web/SgvWebApplicationFactory.cs                       (modificado: override AuthApiClient con HttpClient manual sin Timeout)

docs/decisiones-implementacion.md                         (modificado: subsección runtime MySQL tras :50)
```

## 4. Diseño detallado por frente

### 4.A — Timeout login (FRENTE A)

`src/SGV.Web/Program.cs:72-84` actualiza dos registros typed client. `CargoApiClient`/`PuestosApiClient`/`HabilidadApiClient` (`:86-119`) ya tienen `Timeout = 10s` y **no se tocan**.

```csharp
// :72-77 — AuthApiClient (nuevo: client.Timeout = TimeSpan.FromSeconds(10))
builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10); // alineado con Cargo/Puestos/Habilidad
}).AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>());

// :79-84 — UnidadOrganizativaApiClient (mismo patrón)
```

`tests/SGV.Tests/Web/SgvWebApplicationFactory.cs:89-101` debe agregar `client.Timeout = TimeSpan.FromSeconds(10)` al `HttpClient` manual del override de `IAuthApiClient` (hoy solo setea `BaseAddress`). Sin este cambio, el test factory diverge del runtime y AC-1 no se cumple.

### 4.B — UX frontera login (FRENTE B)

`src/SGV.Web/Pages/Auth/SignIn.cshtml.cs:34` se envuelve en try/catch. `SignIn.cshtml:20-22` no se modifica: el `validation-summary ModelOnly` ya renderiza cualquier `ModelState.AddModelError(string.Empty, ...)`.

Importante: `SignInModel` es primary-constructor con parámetro `logger` (`:14-17`), NO existe campo `_logger`. El snippet usa `logger.LogWarning(...)` directamente:

```csharp
var request = new LoginRequest(Input.UserNameOrEmail, Input.Password);

try
{
    var response = await authApiClient.LoginAsync(request, cancellationToken);

    if (response is null)
    {
        ModelState.AddModelError(string.Empty, "Credenciales inválidas.");
        return Page();
    }
    // ... resto del flujo (:42-71) sin cambios ...
}
catch (HttpRequestException ex)
{
    logger.LogWarning(ex, "Fallo de transporte al autenticar contra la API");
    ModelState.AddModelError(string.Empty,
        "No pudimos contactar al servicio de autenticación. Intentá nuevamente en unos minutos.");
    return Page();
}
catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
{
    logger.LogWarning("Timeout al autenticar contra la API");
    ModelState.AddModelError(string.Empty,
        "La autenticación tardó demasiado. Intentá nuevamente.");
    return Page();
}
```

La guarda `when (!cancellationToken.IsCancellationRequested)` es **obligatoria** para preservar el contrato transversal (`web-apiclient-transport-contract/spec.md:76-82`). Si el `cancellationToken` del request está cancelado, la excepción propaga al pipeline.

### 4.C — Health API (FRENTE C)

`src/SGV.Api/SGV.Api.csproj` **sin cambios** (sin NuGet nuevo: el paquete `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` no se agrega — el check es propio).

Nuevo: `src/SGV.Api/Infrastructure/Health/SgvDbContextReadinessHealthCheck.cs`:

```csharp
public sealed class SgvDbContextReadinessHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("SgvDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
            return HealthCheckResult.Unhealthy("ConnectionStrings:SgvDatabase no está configurada.");

        try
        {
            await using var connection = new MySqlConnector.MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // cancelación del orquestador — propagar
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"MySQL no alcanzable: {ex.Message}");
        }
    }
}
```

El check inyecta `IConfiguration` y abre una conexión cruda con `MySqlConnector.MySqlConnection`. Esto evita por completo la resolución de `SgvDbContext` (o `IDbContextFactory<SgvDbContext>`) desde el proveedor raíz, eliminando el error `Cannot resolve scoped service from root provider`. Además, **no dispara `ServerVersion.AutoDetect`**: ese costo se paga únicamente en el primer request real que use `SgvDbContext`. El timeout efectivo del probe está gobernado por `Connection Timeout` en la connection string.

`src/SGV.Api/Program.cs:64-71` (registro DbContext) vuelve al patrón original:

```csharp
var connectionString = builder.Configuration.GetConnectionString("SgvDatabase");
builder.Services.AddScoped<AuditoriaSaveChangesInterceptor>();
builder.Services.AddDbContext<SgvDbContext>((sp, options) =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
           .AddInterceptors(sp.GetRequiredService<AuditoriaSaveChangesInterceptor>());
});
```

No se registra `AddDbContextFactory<SgvDbContext>`.

Después de los registros de auth (`:100`):

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<SgvDbContextReadinessHealthCheck>("mysql", tags: new[] { "ready" });
```

Pipeline, entre `UseAuthorization` (`:163`) y `MapControllers` (`:165`):

```csharp
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,   // liveness puro: ningún check corre
    ResponseWriter = HealthCheckResponseWriter.WriteJson
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteJson
}).AllowAnonymous();
```

### 4.D — Health Web upstream (FRENTE D)

Nuevo: `src/SGV.Web/Integration/Health/SgvApiUpstreamHealthCheck.cs`:

```csharp
public sealed class SgvApiUpstreamHealthCheck(IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(SgvApiHealthProbeHttpClient.Name);
            using var resp = await client.GetAsync("/health/live", cancellationToken);
            return resp.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Upstream {(int)resp.StatusCode} {resp.ReasonPhrase}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            return HealthCheckResult.Unhealthy($"Upstream timeout ({ex.Message})");
        }
        catch (HttpRequestException ex)
        {
            return HealthCheckResult.Unhealthy($"Upstream error: {ex.Message}");
        }
    }
}
```

`src/SGV.Web/Program.cs` agrega antes de los typed clients (`:72`):

```csharp
builder.Services.AddHttpClient(SgvApiHealthProbeHttpClient.Name, (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(3);
}); // SIN AddHttpMessageHandler<ApiBearerTokenHandler> — es un probe, no user-facing.
```

Después de los typed clients (`:119`):

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<SgvApiUpstreamHealthCheck>("sgv-api-upstream", tags: new[] { "ready" });
```

Mapeo entre `UseAuthorization` (`:135`) y `MapStaticAssets`/`MapRazorPages` (`:137-139`), idéntico al API. La constante `SgvApiHealthProbeHttpClient.Name` vive en `src/SGV.Web/Integration/Health/SgvApiHealthProbeHttpClient.cs` (constante pública).

Web reusa el mismo `HealthCheckResponseWriter` que API (§4.C): la shape JSON es idéntica. Para evitar acoplamiento a `SGV.Api`, el writer se publica en un archivo compartido dentro del proyecto API y se referencia desde Web por **vínculo de archivo** (`<Compile Include="..\SGV.Api\Infrastructure\Health\HealthCheckResponseWriter.cs" Link="Infrastructure\Health\HealthCheckResponseWriter.cs" />` en `SGV.Web.csproj`); alternativa equivalente es duplicar el archivo en `SGV.Web/Integration/Health/` con el mismo cuerpo y test de paridad. Se prefiere la primera por DRY y porque `HealthReport` y `HealthCheckOptions` viven en `Microsoft.Extensions.Diagnostics.HealthChecks` (paquete compartido transitivo en .NET 10).

### 4.E — Validación MySQL (FRENTE E)

`src/SGV.Api/Program.cs:64-71` mantiene `AddDbContext<SgvDbContext>` con el interceptor de auditoría y agrega validación diferida vía `IValidateOptions<DbContextOptions<SgvDbContext>>` con `ValidateOnStart`:

```csharp
var connectionString = builder.Configuration.GetConnectionString("SgvDatabase");

builder.Services.AddScoped<AuditoriaSaveChangesInterceptor>();
builder.Services.AddDbContext<SgvDbContext>((sp, options) =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
           .AddInterceptors(sp.GetRequiredService<AuditoriaSaveChangesInterceptor>());
});

builder.Services.AddSingleton<IValidateOptions<DbContextOptions<SgvDbContext>>,
    SgvDbContextOptionsValidator>();
builder.Services.AddOptions<DbContextOptions<SgvDbContext>>()
    .Validate(_ => true, "noop")  // para que AddOptionsWithValidateOnStart pueda activarse
    .ValidateOnStart();
```

`SgvDbContextOptionsValidator` (nuevo: `src/SGV.Api/Infrastructure/Health/SgvDbContextOptionsValidator.cs`):

```csharp
public sealed class SgvDbContextOptionsValidator : IValidateOptions<DbContextOptions<SgvDbContext>>
{
    private readonly IConfiguration _config;
    public SgvDbContextOptionsValidator(IConfiguration config) => _config = config;

    public ValidateOptionsResult Validate(string? name, DbContextOptions<SgvDbContext> options)
    {
        var cs = _config.GetConnectionString("SgvDatabase");
        if (string.IsNullOrWhiteSpace(cs))
            return ValidateOptionsResult.Fail(
                "Debe configurar ConnectionStrings:SgvDatabase antes de iniciar la API.");
        if (!(cs.Contains("Server=", StringComparison.OrdinalIgnoreCase)
              && cs.Contains("Database=", StringComparison.OrdinalIgnoreCase)))
            return ValidateOptionsResult.Fail(
                "ConnectionStrings:SgvDatabase inválida: debe incluir Server= y Database=.");
        if (!cs.Contains("Connection Timeout=", StringComparison.OrdinalIgnoreCase))
        {
            // .NET 10 no expone ValidateOptionsResult.Warn; el warning pasa a ser
            // documentación operativa (ver §4.F). El host arranca igual.
            return ValidateOptionsResult.Success;
        }
        return ValidateOptionsResult.Success;
    }
}
```

**Hard fail** (lanza al `Build()` con `ValidateOnStart` y también inline en `Program.cs`): ausente / whitespace / malformada. La ausencia de `Connection Timeout` ya no genera un warning programático porque `ValidateOptionsResult.Warn` no existe en .NET 10; se documenta como recomendación operativa en §4.F.

`tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` se modifica para:
- Proveer una connection string válida por defecto (configura `ConnectionStrings:SgvDatabase` en `ConfigureAppConfiguration` antes de cualquier override).
- Conservar el constructor opcional `Action<IConfigurationBuilder>? configureConfig = null` que `StartupValidationTests` usa para inyectar conn string inválida o ausente y verificar el throw deterministico.

### 4.F — Documentación operativa MySQL (FRENTE F)

`docs/decisiones-implementacion.md` agrega una subsección tras `:50` con: (1) contrato `/health/live` vs `/health/ready`; (2) **resolución del trade-off AutoDetect**: el readiness check usa `MySqlConnector.MySqlConnection` directamente y **nunca** resuelve `SgvDbContext`, por lo que **no dispara `ServerVersion.AutoDetect` en absoluto**. El costo de AutoDetect se paga únicamente en el primer request real que use `SgvDbContext` después de un pod start. Mitigaciones operativas: pre-warm externo (curl al `/health/live` no basta; se requiere una ruta que resuelva el contexto, p. ej. un health check "warm-up" opcional) o fijar versión con `MySqlServerVersion(8.0.36)` en runtime (decisión fuera de alcance de este change); (3) `Connection Timeout=5` recomendado en connection string productiva; (4) separación design-time (`SgvDbContextFactory` en `SGV.Infraestructura`) vs runtime (`Program.cs`); (5) ubicación de la connection string por ambiente — `dotnet user-secrets --project src/SGV.Api` para dev, env var `ConnectionStrings__SgvDatabase` en CI/productivo; (6) recordatorio explícito de que `Jwt:SigningKey` placeholder dev NUNCA aparece como valor productivo.

### 4.G — Delta a `sgv-readonly-api` (FRENTE G, resuelve B1)

Nuevo spec delta: `openspec/changes/2026-07-14-fix-126-operational-tech-debt/specs/sgv-readonly-api/spec.md`. El spec vigente `openspec/specs/sgv-readonly-api/spec.md:174-191` declara que `POST /api/v1/auth/login` es la única ruta anónima. Este change requiere exceptuar también `/health/live` y `/health/ready` (en API y Web composition roots) del default-deny, sin relajar la fallback policy `RequireAuthenticatedUser`. El delta agrega:

- **ADDED Requirement** "Excepción de anonimato para probes operacionales": `GET /health/live` y `GET /health/ready` en API y Web MUST ser anónimos y exceptuados del default-deny; la fallback policy `RequireAuthenticatedUser` MUST permanecer intacta; los probes MUST NOT extender la excepción a ninguna otra ruta.
- **Scenario** "Probe anónimo API responde 200/503 sin 401" y "Probe anónimo Web responde 200/503 sin redirect".
- **Source**: cross-referencia a `operational-readiness/spec.md:77-96` (REQ probes anónimos).

Mecánica: `Program.cs` aplica `.AllowAnonymous()` explícito a cada `MapHealthChecks(...)` (ya documentado en §4.C). El delta codifica el contrato para que un futuro change no "optimice" relájando la fallback policy global.

## 5. Estrategia de pruebas

Trazabilidad tests → spec:

| Archivo de test | Escenarios clave | Spec / AC |
|---|---|---|
| `Web/AuthApiClientTimeoutTests.cs` | `AuthApiClient_HasTenSecondTimeout`, `UnidadOrganizativaApiClient_HasTenSecondTimeout`, `Login_SlowUpstream_TaskCanceledBeforeTimeout` (handler que await TCS no completado → `TaskCanceledException` deterministica sin `Task.Delay` real) | AC-1 + `web-apiclient-transport-contract` |
| `Web/SignInTransportTests.cs` | `HttpRequestException_RendersSpanishError`, `TaskCanceledException_NotCancelled_RendersTimeout`, `TaskCanceledException_Cancelled_Propagates`, `Unauthorized_StillInvalidCredentials` | AC-2, AC-3 |
| `Web/HealthTests.cs` | `Live_AnonymousReturns200`, `Ready_UpstreamHealthy_Returns200` (DelegatingHandler → 200), `Ready_UpstreamDown_Returns503` (handler lanza `HttpRequestException`), `Ready_UpstreamSlow_Returns503` (handler await TCS sin completar), `Ready_NoCookie_NoRedirect` | AC-6, AC-7 |
| `Api/HealthTests.cs` | `Live_NoAuth_Returns200`, `Ready_DbUnhealthy_Returns503` (connection string inválida / puerto cerrado), `Ready_MySqlUp_Returns200` (`[MySqlFact]`), `Ready_ResponseHasNoStackTrace` | AC-4, AC-5 |
| `Api/StartupValidationTests.cs` | `Host_Build_ThrowsWhenConnectionStringMissing` (config vacío), `Host_Build_ThrowsWhenWhitespace`, `Host_Build_ThrowsWhenMalformed_NoServerNoDatabase`, `Host_Build_WarnsWhenConnectionTimeoutMissing`, `Host_Build_SucceedsWithValidConnectionString` | AC-8 |
| `Api/ApiWebApplicationFactory.cs` | (modificado) helper `WithoutConnectionString` para inyectar conn string inválida | AC-8 |
| `Web/SgvWebApplicationFactory.cs` | (modificado) override `AuthApiClient` en `:89-101` setea `Timeout = 10s` | AC-1 consistencia |

### Conteos reconciliados (W1)

- `web-apiclient-transport-contract`: **3** ADDED requirements, **6** scenarios.
- `sgv-web-authentication`: **1** ADDED requirement, **3** escenarios.
- `operational-readiness`: **7** requirements, **10** scenarios.
- `sgv-readonly-api` (nuevo delta): **2** ADDED requirements, **5** scenarios.

### 5.3 Cross-cutting

- CI workflow: sin cambios. Frontend ya cubierto. Drift MySQL fact: `verify-report.md` declara conteo ejecutado vs omitido (AC-10).
- `bun run build` + gate `git diff --exit-code -- bun.lock wwwroot` se ejecutan en CI como antes (AC-11).

## 6. Riesgos y mitigaciones (incluye W4)

| Riesgo | Mitigación concreta |
|---|---|
| Regresión `WebAuthenticationTests` | Agregar `AuthApiClientTimeoutTests` + `SignInTransportTests` **antes** de tocar `SignIn.cshtml.cs`; correr suite web antes/después |
| **AutoDetect dispara en primer request real (W4)** | El check propio (ADR-01) NO pre-calienta AutoDetect; primer request que use `SgvDbContext` (no `/health/live`) puede pagar la detección. Documentado en §4.F; mitigaciones operativas (pre-warm externo, fijar versión) fuera de alcance. Operadores deben monitorear latencia del primer request por pod |
| **Conflicto con `2026-07-11-*` in-flight (W4)** | Gate concreto antes de apply: `git log --oneline -- openspec/changes/archive/2026-07-11-*/archive-report.md` debe listar AMBOS cambios. Si falta alguno, diferir ediciones a `docs/decisiones-implementacion.md` y specs transversales hasta que `2026-07-11-fix-active-puesto-id-unique-type` y `2026-07-11-hacer-suite-tests-determinista` estén archivados |
| 503 espurios por latencia MySQL | Documentado en §4.F; sin retry en este change (non-goal #5) |
| Drift cobertura MySQL (146 cacheados vs 166 estáticos) | `verify-report.md` declara conteo ejecutado vs omitido explícitamente |
| Scope creep `UseExceptionHandler("/Error")` | Non-goal; no se toca; si surge durante apply, escalar como propuesta separada |
| Presupuesto de revisión > 400 LoC | Si `tasks.md` > 400 LoC netas, dividir en chained PRs: (a) `/health` en API+Web, (b) `SignIn` UX + timeout + conn string |
| HealthCheckResponseWriter accessibility cross-project (§4.D) | Si `Compile Include` da fricción, fallback: duplicar writer en `SGV.Web/Integration/Health/` con test de paridad |

## 7. Rollback

1. **Timeout login**: quitar `client.Timeout = TimeSpan.FromSeconds(10)` en `SGV.Web/Program.cs:72-84` + revert del override en `SgvWebApplicationFactory.cs:89-101`.
2. **UX login**: remover los dos `catch` en `SignIn.cshtml.cs:34` → vuelve a propagar al pipeline.
3. **Health API/Web**: revertir `AddHealthChecks` + `MapHealthChecks` en ambos `Program.cs` y borrar `Infrastructure/Health/` + `Integration/Health/`.
4. **Validación MySQL**: remover `IValidateOptions` registrado y la llamada `ValidateOnStart` → vuelve a fallar silencioso.
5. **Delta sgv-readonly-api**: el spec es archivo dentro del change folder; rollback = revertir el archivo `specs/sgv-readonly-api/spec.md` (no toca la spec archivada de destino).
6. **Documentación**: revertir la subsección agregada en `decisiones-implementacion.md`.

Sin migraciones EF ni cambios en `appsettings*.json` versionados.

## 8. Orden de implementación recomendado (TDD estricto)

1. **CU-0 RED**: `StartupValidationTests` (4 escenarios conexión) + `ApiHealthTests` (live 200, ready 503 con stub) + `WebHealthTests` (live 200, ready con fake handler). Confirmar rojo.
2. **CU-0 GREEN — Validación MySQL**: registrar `SgvDbContextOptionsValidator` + `ValidateOnStart` en `SGV.Api/Program.cs`. Re-run.
3. **CU-0 GREEN — Health API**: crear `SgvDbContextReadinessHealthCheck` + `HealthCheckResponseWriter`, registrar `AddHealthChecks` + `MapHealthChecks` + `.AllowAnonymous()` en API. Re-run.
4. **CU-0 GREEN — Health Web**: crear `SgvApiHealthProbeHttpClient` (named) + `SgvApiUpstreamHealthCheck`, registrar, mapear con `.AllowAnonymous()`, vincular `HealthCheckResponseWriter`. Re-run.
5. **CU-1 RED**: `AuthApiClientTimeoutTests` con `TaskCompletionSource` (NO `Task.Delay`). Confirmar rojo.
6. **CU-1 GREEN**: editar `SGV.Web/Program.cs:72-84` + override en `SgvWebApplicationFactory.cs:89-101`. Re-run.
7. **CU-2 RED**: `SignInTransportTests` con HttpRequestException, TaskCanceledException, cancelación cooperativa. Confirmar rojo.
8. **CU-2 GREEN**: editar `SignInModel.OnPostAsync` con try/catch usando `logger.LogWarning`. Re-run suite web completa.
9. **CU-3 — Delta sgv-readonly-api**: el spec ya está escrito; el apply simplemente versiona el archivo. Sin código de runtime asociado.
10. **CU-4 — Docs**: agregar subsección en `decisiones-implementacion.md`. Verificación manual.
11. **CU-5 — Verify**: `dotnet test SGV.slnx --configuration Release` con MySQL real en CI; `bun run build`; `git diff --exit-code -- bun.lock wwwroot`. Capturar `verify-report.md` con conteo ejecutado/omitido de `[MySqlFact]`.

Cada CU genera 1 commit trabajo-unidad. Si el tamaño acumulado supera 400 LoC netas, dividir en chained PRs.

## 9. Preguntas abiertas (carry-over del proposal §8, corregidas)

1. ¿Alinear `UnidadOrganizativaApiClient` a 10 s en este mismo change? **Recomendación**: sí, mismo change.
2. ¿`AllowAnonymous()` por endpoint o relajar fallback policy? **Recomendación**: `AllowAnonymous()` por endpoint. El delta `sgv-readonly-api` (§4.G) codifica esta excepción sin tocar la fallback policy.
3. Mensajes exactos en español. **Propuestos en §4.B**.
4. Health upstream Web contra `/health/live` o endpoint dedicado. **Recomendación**: `/health/live` (ya implica proceso vivo upstream; cheapest probe).
5. **`AddDbContextCheck` vs custom con `CanConnectAsync`**. **Decisión revisada**: custom `IHealthCheck` (ADR-01). `AddDbContextCheck` queda descartado por acoplar la resolución del contexto al primer probe y disparar `ServerVersion.AutoDetect` antes de que el cuerpo del check corra.
