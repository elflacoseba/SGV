# Tareas: `2026-07-14-fix-126-operational-tech-debt`

## Review Workload Forecast

| Campo | Valor |
|-------|-------|
| Líneas estimadas cambiadas | ~600-720 |
| Riesgo presupuesto 400 líneas | Medio |
| Chained PRs recomendado | Sí |
| División sugerida | PR 1: CU-0 (/health) → PR 2: CU-1+CU-2 (login UX) → PR 3: CU-3+CU-4+CU-5 (spec+docs+verify) |
| Delivery strategy | ask-on-risk |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: Medium

### Suggested Work Units

| Unit | Goal | Likely PR | Base | Notes |
|------|------|-----------|------|-------|
| 1 | Health infrastructure (CU-0) | PR 1 | `develop` | RED tests + 3 GREEN commits. Tests: ~250 ln, Prod: ~230 ln |
| 2 | Login timeout + UX (CU-1, CU-2) | PR 2 | `develop` (o PR 1 branch si stacked) | RED+GRN × 2. Tests: ~165 ln, Prod: ~32 ln |
| 3 | Spec delta + docs + verify (CU-3..5) | PR 3 | `develop` (o PR 2 branch) | Sin runtime code. Docs+verify: ~40 ln |

---

## Fase 0: Fundación — Health Infrastructure (CU-0 RED)

### Tarea 0-RED: Tests de health API, Web y validación MySQL
- **Status**: ✅ COMPLETE (PR 1)
- **Type**: RED
- **Work unit**: 1 commit
- **Depends on**: — (arranque, primer commit del cambio)
- **Spec traceability**: operational-readiness REQ-1..REQ-5, sgv-readonly-api REQ-1
- **AC traceability**: AC-4, AC-5, AC-6, AC-7, AC-8
- **Test file(s)**: `tests/SGV.Tests/Api/StartupValidationTests.cs` (nuevo), `tests/SGV.Tests/Api/HealthTests.cs` (nuevo), `tests/SGV.Tests/Web/HealthTests.cs` (nuevo)
- **Source file(s)**: ninguno — solo tests
- **RED phase**:
  - `StartupValidationTests`: `HostBuild_ThrowsWhenConnectionStringMissing`, `HostBuild_ThrowsWhenWhitespace`, `HostBuild_ThrowsWhenMalformed_NoServerNoDatabase`, `HostBuild_WarnsWhenConnectionTimeoutMissing`, `HostBuild_SucceedsWithValidConnectionString`
  - `ApiHealthTests`: `Live_NoAuth_Returns200` (con factory default), `Ready_NoAuth_Returns503_UnhealthyJson` (stub IDbContextFactory con CanConnectAsync=false), `Ready_MySqlUp_Returns200` ([MySqlFact]), `Ready_ResponseHasNoStackTrace` (verificar campo `exception`/`stackTrace` ausente)
  - `WebHealthTests`: `Live_AnonymousReturns200`, `Ready_UpstreamHealthy_Returns200` (DelegatingHandler → 200), `Ready_UpstreamDown_Returns503` (handler lanza `HttpRequestException`), `Ready_UpstreamSlow_Returns503` (handler con TCS sin completar), `Ready_NoCookie_NoRedirect`
  - `ApiWebApplicationFactory` se extiende con helper `WithoutConnectionString` para tests de validación — también se escribe en esta tarea
- **GREEN phase**: N/A (RED puro) — fallan porque no existe el código productivo
- **REFACTOR**: ninguno

---

## Fase 1: Fundación — Health Infrastructure (CU-0 GREEN)

### Tarea 0a-GREEN: Validación MySQL fail-loud con `SgvDbContextOptionsValidator`
- **Status**: ✅ COMPLETE (PR 1)
- **Type**: GREEN
- **Work unit**: 1 commit
- **Depends on**: Tarea 0-RED
- **Spec traceability**: operational-readiness REQ-6 (W1 corregido — validación diferida), escenario fallo conn string vacía/malformada
- **AC traceability**: AC-8
- **Test file(s)**: `tests/SGV.Tests/Api/StartupValidationTests.cs` (se vuelve verde) — 5/5 pass
- **Source file(s)**: `src/SGV.Api/Infrastructure/Health/SgvDbContextOptionsValidator.cs` (nuevo), `src/SGV.Api/Program.cs` (modificado)
- **RED phase**: Los tests escritos en 0-RED fallan (OptionsValidationException no se lanza porque no existe el validador)
- **GREEN phase**: Crear `SgvDbContextOptionsValidator` implementando `IValidateOptions<DbContextOptions<SgvDbContext>>`: lee `ConnectionStrings:SgvDatabase` via `IConfiguration`, valida null/whitespace (Fail), formato `Server=`+`Database=` (Fail), ausencia de `Connection Timeout=` (Warn). Validación inline + registro singleton en `Program.cs`. Migrar de `AddDbContext<>` a `AddDbContextFactory<>` (sin interceptor) + `AddDbContext` (con interceptor) para evitar conflicto de alcance con `AuditoriaSaveChangesInterceptor`.
- **REFACTOR**: Verificar que `StartupValidationTests` pase completo — ✅ 5/5

### Tarea 0b-GREEN: Health API — `SgvDbContextReadinessHealthCheck` + `HealthCheckResponseWriter` + wiring
- **Status**: ✅ COMPLETE (PR 1)
- **Type**: GREEN
- **Work unit**: 1 commit
- **Depends on**: Tarea 0-RED (tests escritos), Tarea 0a-GREEN (DbContextFactory ya registrado)
- **Spec traceability**: operational-readiness REQ-1, REQ-2, REQ-5; sgv-readonly-api REQ-1, REQ-2
- **AC traceability**: AC-4, AC-5
- **Test file(s)**: `tests/SGV.Tests/Api/HealthTests.cs` (se vuelve verde) — 3/5 pass + 2 failing due to MySQL 9.6 AutoDetect
- **Source file(s)**: `src/SGV.Api/Infrastructure/Health/SgvDbContextReadinessHealthCheck.cs` (nuevo), `src/SGV.Api/Infrastructure/Health/HealthCheckResponseWriter.cs` (nuevo), `src/SGV.Api/Program.cs` (modificado)
- **RED phase**: Tests de 0-RED fallan — no hay check ni endpoints
- **GREEN phase**:
  - Crear `SgvDbContextReadinessHealthCheck(IDbContextFactory<SgvDbContext>)` con tres ramas catch (OperationCanceledException → rethrow, Exception → Unhealthy con ex.Message)
  - Crear `HealthCheckResponseWriter.WriteJson(HttpContext, HealthReport)` estático: serializa DTO sin `Exception`/stack trace, `Content-Type: application/json`, usa status codes default
  - En `Program.cs`: `builder.Services.AddHealthChecks().AddCheck<SgvDbContextReadinessHealthCheck>("mysql", tags: ["ready"])`. Pipeline: `app.MapHealthChecks("/health/live", Predicate=_=>false, ResponseWriter=WriteJson).AllowAnonymous()` + `app.MapHealthChecks("/health/ready", Predicate=check=>check.Tags.Contains("ready"), ResponseWriter=WriteJson).AllowAnonymous()`
- **REFACTOR**: Verificar `ApiHealthTests` pase — 3/5 pass, 2 `[MySqlFact]` fail (MySQL 9.6 AutoDetect)

### Tarea 0c-GREEN: Health Web upstream — `SgvApiHealthProbeHttpClient` + `SgvApiUpstreamHealthCheck` + wiring
- **Status**: ✅ COMPLETE (PR 1)
- **Type**: GREEN
- **Work unit**: 1 commit
- **Depends on**: Tarea 0-RED, Tarea 0b-GREEN (HealthCheckResponseWriter se referencia desde Web)
- **Spec traceability**: operational-readiness REQ-3, REQ-4, REQ-5; sgv-readonly-api REQ-1
- **AC traceability**: AC-6, AC-7
- **Test file(s)**: `tests/SGV.Tests/Web/HealthTests.cs` (se vuelve verde) — 6/6 pass
- **Source file(s)**: `src/SGV.Web/Integration/Health/SgvApiHealthProbeHttpClient.cs` (nuevo — constante `Name`), `src/SGV.Web/Integration/Health/SgvApiUpstreamHealthCheck.cs` (nuevo), `src/SGV.Web/Program.cs` (modificado), `src/SGV.Web/SGV.Web.csproj` (link a `HealthCheckResponseWriter.cs`)
- **RED phase**: Tests de Web/HealthTests.cs fallan — no hay endpoints health en Web
- **GREEN phase**:
  - Crear `SgvApiHealthProbeHttpClient` con constante `public const string Name = "SgvApiHealthProbe"`
  - Crear `SgvApiUpstreamHealthCheck(IHttpClientFactory)`: usa named client, GET /health/live, catch de OperationCanceledException (rethrow), TaskCanceledException (Unhealthy), HttpRequestException (Unhealthy)
  - En `Program.cs` antes de typed clients: `builder.Services.AddHttpClient(SgvApiHealthProbeHttpClient.Name, (sp, client) => { ... BaseAddress, Timeout=3s })` SIN `ApiBearerTokenHandler`. Tras typed clients: `builder.Services.AddHealthChecks().AddCheck<SgvApiUpstreamHealthCheck>("sgv-api-upstream", tags: ["ready"])`. Mapeo idéntico al API entre `UseAuthorization` y `MapStaticAssets`, con `.AllowAnonymous()`
  - Vincular `HealthCheckResponseWriter.cs` desde `SGV.Api` por link de compilación en `SGV.Web.csproj`
- **REFACTOR**: Verificar `WebHealthTests` pase completo — ✅ 6/6

---

## Fase 2: Timeout login (CU-1)

### Tarea 1-RED: Tests de timeout en `AuthApiClient` y `UnidadOrganizativaApiClient`
- **Type**: RED
- **Work unit**: 1 commit
- **Depends on**: Tarea 0-RED (tiene el patrón de factory), puede paralelizarse con 0a/0b/0c-GREEN
- **Spec traceability**: web-apiclient-transport-contract ADDED REQ-1, Scenario timeout efectivo + upstream lento
- **AC traceability**: AC-1
- **Test file(s)**: `tests/SGV.Tests/Web/AuthApiClientTimeoutTests.cs` (nuevo)
- **Source file(s)**: ninguno (solo tests)
- **RED phase**:
  - `AuthApiClient_HasTenSecondTimeout`: resolver `IAuthApiClient` via `SgvWebApplicationFactory` y verificar que el `HttpClient.Timeout` subyacente es 10s → falla porque `Program.cs:72-77` no setea `Timeout` y el factory override tampoco
  - `UnidadOrganizativaApiClient_HasTenSecondTimeout`: mismo patrón → falla
  - `Login_SlowUpstream_TaskCanceledBeforeTimeout`: fake handler con `TaskCompletionSource` que nunca se completa, `AuthApiClient.LoginAsync` debe lanzar `TaskCanceledException` antes de 10s ± tolerancia → falla (default 100s)
- **GREEN phase**: N/A (RED puro)
- **REFACTOR**: ninguno

### Tarea 1-GREEN: Timeout 10s en `AuthApiClient` y `UnidadOrganizativaApiClient`
- **Type**: GREEN
- **Work unit**: 1 commit
- **Depends on**: Tarea 0c-GREEN (mismos archivos `Program.cs` editados), Tarea 1-RED
- **Spec traceability**: web-apiclient-transport-contract ADDED REQ-1
- **AC traceability**: AC-1
- **Test file(s)**: `tests/SGV.Tests/Web/AuthApiClientTimeoutTests.cs` (se vuelve verde)
- **Source file(s)**: `src/SGV.Web/Program.cs` (modificado), `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` (modificado)
- **RED phase**: Tests de 1-RED fallan
- **GREEN phase**:
  - En `src/SGV.Web/Program.cs:72-77` agregar `client.Timeout = TimeSpan.FromSeconds(10);` después de `BaseAddress`
  - En `src/SGV.Web/Program.cs:79-84` idem para `UnidadOrganizativaApiClient`
  - En `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs:89-101` agregar `Timeout = TimeSpan.FromSeconds(10)` en el `HttpClient` manual del override de `IAuthApiClient`
- **REFACTOR**: Verificar que `AuthApiClientTimeoutTests` pase completo

---

## Fase 3: UX frontera login (CU-2)

### Tarea 2-RED: Tests de transporte en `SignInTransportTests`
- **Type**: RED
- **Work unit**: 1 commit
- **Depends on**: Tarea 1-GREEN (necesita el timeout de 10s para que el test de timeout tenga sentido)
- **Spec traceability**: web-apiclient-transport-contract ADDED REQ-2, ADDED REQ-3; sgv-web-authentication ADDED REQ-1; scenarios transporte caído, timeout, cancelación cooperativa, 401 regresión
- **AC traceability**: AC-2, AC-3
- **Test file(s)**: `tests/SGV.Tests/Web/SignInTransportTests.cs` (nuevo)
- **Source file(s)**: ninguno (solo tests)
- **RED phase**:
  - `SignIn_HttpRequestException_RendersSpanishError`: fake handler lanza `HttpRequestException`, POST `/auth/sign-in` con credenciales válidas → `ModelState` contiene el mensaje español, status es `200` (misma página), NO excepción propagada
  - `SignIn_TaskCanceledExceptionNotCancelled_RendersTimeoutError`: fake handler lanza `TaskCanceledException`, request token NO cancelado → `ModelState` contiene mensaje de timeout en español
  - `SignIn_TaskCanceledExceptionCancelled_Propagates`: `CancellationToken` pre-cancelado, fake handler lanza `TaskCanceledException` → la excepción propaga y no se agrega mensaje a `ModelState`
  - `SignIn_401_StillInvalidCredentials`: fake handler responde 401 → `ModelState` contiene "Credenciales inválidas." (regresión guard)
- **GREEN phase**: N/A (RED puro) — fallan porque `SignIn.cshtml.cs` no tiene try/catch
- **REFACTOR**: ninguno

### Tarea 2-GREEN: Try/catch en `SignInModel.OnPostAsync`
- **Type**: GREEN
- **Work unit**: 1 commit
- **Depends on**: Tarea 2-RED
- **Spec traceability**: web-apiclient-transport-contract ADDED REQ-2, ADDED REQ-3; sgv-web-authentication ADDED REQ-1
- **AC traceability**: AC-2, AC-3
- **Test file(s)**: `tests/SGV.Tests/Web/SignInTransportTests.cs` (se vuelve verde)
- **Source file(s)**: `src/SGV.Web/Pages/Auth/SignIn.cshtml.cs` (modificado)
- **RED phase**: Tests fallan — el handler no captura excepciones
- **GREEN phase**: Envolver `authApiClient.LoginAsync(request, cancellationToken)` en try/catch:
  - `catch (HttpRequestException ex)`: `logger.LogWarning(...)`, `ModelState.AddModelError(string.Empty, "No pudimos contactar al servicio de autenticación. Intentá nuevamente en unos minutos.")`, `return Page()`
  - `catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)`: `logger.LogWarning(...)`, `ModelState.AddModelError(string.Empty, "La autenticación tardó demasiado. Intentá nuevamente.")`, `return Page()`
  - Importante: usar `logger` (parámetro del primary constructor), NO `_logger`
- **REFACTOR**: Verificar `SignInTransportTests` pase completo, más suite web existente (`WebAuthenticationTests`) sin regresiones

---

## Fase 4: Spec delta + Docs (CU-3, CU-4)

### Tarea 3-SPEC: Versionar delta `sgv-readonly-api`
- **Type**: DOC
- **Work unit**: 1 commit
- **Depends on**: —
- **Spec traceability**: sgv-readonly-api ADDED REQ-1, ADDED REQ-2
- **AC traceability**: (ninguno directo — cross-ref a AC-4..AC-7)
- **Test file(s)**: ninguno
- **Source file(s)**: `openspec/changes/2026-07-14-fix-126-operational-tech-debt/specs/sgv-readonly-api/spec.md` (ya existe, se agrega a git)
- **RED phase**: N/A — no hay código runtime
- **GREEN phase**: `git add openspec/changes/2026-07-14-fix-126-operational-tech-debt/specs/sgv-readonly-api/spec.md`
- **REFACTOR**: ninguno

### Tarea 4-DOC: Subsección runtime MySQL en `decisiones-implementacion.md`
- **Type**: DOC
- **Work unit**: 1 commit
- **Depends on**: Tarea 0b-GREEN (tener la implementación de health para documentar el contrato real)
- **Spec traceability**: operational-readiness REQ-7
- **AC traceability**: AC-9
- **Test file(s)**: ninguno
- **Source file(s)**: `docs/decisiones-implementacion.md` (modificado)
- **RED phase**: N/A
- **GREEN phase**: Agregar subsección tras línea 50 (tras `## Gestión de secretos JWT` o al final de la sección de BD) con:
  1. Contrato liveness (`GET /health/live`, sin dependencias) vs readiness (`GET /health/ready`, requiere MySQL en API y API viva en Web)
  2. Trade-off AutoDetect: el readiness check NO pre-calienta `ServerVersion.AutoDetect`; primer request real puede pagar latencia; mitigaciones operativas
  3. `Connection Timeout=5` recomendado en connection string productiva
  4. Separación design-time (`SgvDbContextFactory`) vs runtime (`Program.cs`)
  5. Ubicación de connection string por ambiente: `dotnet user-secrets --project src/SGV.Api` en dev, env var `ConnectionStrings__SgvDatabase` en CI/productivo; los archivos versionados NO llevan connection string
  6. Recordatorio: placeholder JWT dev NO es apto para producción
- **REFACTOR**: Verificación manual de que la subsección cubre todos los puntos

---

## Fase 5: Verificación (CU-5)

### Tarea 5-VERIFY: Suite completa + bun build + verify-report
- **Type**: VERIFY
- **Work unit**: 1 commit
- **Depends on**: TODAS las tareas anteriores completadas
- **Spec traceability**: operational-readiness (todos los REQ); AC-10, AC-11
- **AC traceability**: AC-10, AC-11
- **Test file(s)**: ninguno nuevo
- **Source file(s)**: `openspec/changes/2026-07-14-fix-126-operational-tech-debt/verify-report.md` (nuevo)
- **RED phase**: N/A
- **GREEN phase**:
  - `dotnet test SGV.slnx --configuration Release` con MySQL real (verificar 0 fallos, reportar conteo `[MySqlFact]` ejecutados vs omitidos, drift entre 146 cacheados y el inventario real)
  - `bun run build` dentro de `src/SGV.Web`
  - `git diff --exit-code -- bun.lock wwwroot` (gate de drift)
  - Producir `verify-report.md` en español con: estado general, conteo de tests ejecutados/omitidos, resultado de bun build, resultado del diff gate, drift de MySqlFact count
- **REFACTOR**: ninguno

---

## Resumen de dependencias

```
0-RED ─┬─ 0a-GREEN ─┐
       ├─ 0b-GREEN ─┤
       └─ 0c-GREEN ─┤
                     ├─ 1-RED ── 1-GREEN ── 2-RED ── 2-GREEN ── 5-VERIFY
                     │
                     └─ 3-SPEC ───────────────────────────────────┤
                                                                    │
                     4-DOC ────────────────────────────────────────┘
```

Las tareas 0-RED y 0a/0b/0c-GREEN son prerrequisitos de 1-GREEN porque modifican `Program.cs` y `SgvWebApplicationFactory.cs`. 3-SPEC y 4-DOC son independientes del flujo runtime y pueden hacerse en cualquier momento antes de 5-VERIFY.
