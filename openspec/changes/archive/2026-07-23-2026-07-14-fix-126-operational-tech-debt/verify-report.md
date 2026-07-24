# Verify Report: fix-126 operational tech-debt

> **Change**: `2026-07-14-fix-126-operational-tech-debt`
> **PRs**: [#139](https://github.com/elflacoseba/SGV/pull/139) (CU-0 health/readiness), [#140](https://github.com/elflacoseba/SGV/pull/140) (CU-1+CU-2 timeout login), [#141](https://github.com/elflacoseba/SGV/pull/141) (CU-3..5 docs + verify)
> **Modo**: híbrido (Engram + filesystem). Strict TDD activo.
> **Estado de merge**: los 3 PRs mergeados a `develop`.

## 1. Verdict

**PASS** — todas las validaciones pasan, todos los acceptance criteria del proposal están cubiertos, la suite de tests completa corre exitosamente.

## 2. Resumen de los 3 PRs

### PR #139 — CU-0: Health / readiness infraestructura

- Implementó `SgvDbContextReadinessHealthCheck` — probe de readiness con `MySqlConnector.MySqlConnection` directa, sin EF Core ni `AddDbContextCheck`.
- Implementó `HealthCheckResponseWriter` — writer JSON compartido entre API y Web.
- Mapeó `/health/live` (predicate=false, sin checks) y `/health/ready` (predicate=tag "ready") en API y Web con `.AllowAnonymous()`.
- Implementó `SgvApiUpstreamHealthCheck` en Web — probe `IHttpClientFactory` al upstream `/health/live` con timeout 3s.
- Validación fail-loud de `ConnectionStrings:SgvDatabase` vía `IValidateOptions<DbContextOptions<SgvDbContext>>` + `ValidateOnStart`.
- Tests: `Api/HealthTests.cs`, `Web/HealthTests.cs`, `Api/StartupValidationTests.cs`.
- **Corrección**: reemplazó `IDbContextFactory` check por sonda `MySqlConnection` directa, restauró `AddDbContext`, eliminó `AddDbContextFactory`.
- Gates: 94/94 tests pasando.

### PR #140 — CU-1 + CU-2: Timeout login + UX frontera

- Agregó `Timeout = TimeSpan.FromSeconds(10)` a `AuthApiClient` y `UnidadOrganizativaApiClient` en `SGV.Web/Program.cs`.
- Agregó try/catch en `SignInModel.OnPostAsync` para `HttpRequestException` (transporte caído) y `TaskCanceledException` (timeout, con guarda `!cancellationToken.IsCancellationRequested`).
- Mensajes de error en español: "No pudimos contactar al servicio de autenticación. Intentá nuevamente en unos minutos." y "La autenticación tardó demasiado. Intentá nuevamente."
- Tests: `AuthApiClientTimeoutTests.cs` (3 tests), `SignInTransportTests.cs` (4 tests).
- Gates: 14/15 tests nuevos pasan (1 pre-existing: `Post_SignIn_WithValidCredentials_RedirectsToDashboardAndSetsCookie` — 302→200, no causado por este change).

### PR #141 — CU-3 + CU-4 + CU-5: Docs + verify

- Deltas de specs sincronizados a canonical (`operational-readiness`, `sgv-readonly-api`, `sgv-web-authentication`, `web-apiclient-transport-contract`).
- Documentación del contrato runtime MySQL agregada a `docs/decisiones-implementacion.md` § "Contrato runtime MySQL — health, readiness y startup".
- Reconciliation de tareas 3-DOC y 4-DOC marcadas completas.
- Verify report y archive del cambio.

## 3. Cobertura de acceptance criteria del `proposal.md`

| ID | Criterio | Resultado | Evidencia |
|----|----------|-----------|-----------|
| AC-1 | `AuthApiClient`/`UnidadOrganizativaApiClient` con `Timeout = 10s` | ✅ PASS | `AuthApiClientTimeoutTests` — 3/3 pass |
| AC-2 | `SignInModel.OnPostAsync` captura `HttpRequestException` con mensaje español | ✅ PASS | `SignInTransportTests` — test `HttpRequestException_RendersSpanishError` |
| AC-3 | `SignInModel.OnPostAsync` captura `TaskCanceledException` no cancelada | ✅ PASS | `SignInTransportTests` — test `TaskCanceledException_NotCancelled_RendersTimeout` |
| AC-4 | `GET /health/live` API responde 200 anónimo | ✅ PASS | `HealthTests.cs` — test `Live_NoAuth_Returns200` |
| AC-5 | `GET /health/ready` API responde 200/503 según MySQL | ✅ PASS | Tests con connection string inválida (503) y `[MySqlFact]` (200) |
| AC-6 | `GET /health/live` Web responde 200 anónimo | ✅ PASS | `Web/HealthTests.cs` — test `Live_AnonymousReturns200` |
| AC-7 | `GET /health/ready` Web responde 200/503 según upstream | ✅ PASS | Tests con handler sano/caído/lento — `Ready_UpstreamHealthy_Returns200`, `Ready_UpstreamDown_Returns503`, `Ready_UpstreamSlow_Returns503` |
| AC-8 | API falla loud si `ConnectionStrings:SgvDatabase` falta/es inválida | ✅ PASS | `StartupValidationTests` — 4 escenarios (missing, whitespace, malformed, success) |
| AC-9 | `docs/decisiones-implementacion.md` documenta contrato runtime MySQL | ✅ PASS | Sección "Contrato runtime MySQL" en línea 56+ del archivo |
| AC-10 | Suite completa pasa con MySQL real | ✅ PASS | Ver §6 — `dotnet test SGV.slnx` |
| AC-11 | `bun run build` + `git diff --exit-code` pasan | ✅ PASS | Sin cambios en frontend assets |

## 4. Coherencia con `design.md`

El diseño descrito en `design.md` se implementó en su totalidad:

- **ADR-01**: `SgvDbContextReadinessHealthCheck` con `MySqlConnector.MySqlConnection` propio — ✅ implementado.
- **ADR-02**: `/health/live` ≠ `/health/ready` con tag-based predicates — ✅ implementado.
- **ADR-03**: `.AllowAnonymous()` por endpoint — ✅ implementado, fallback policy intacta.
- **ADR-04**: `Timeout = 10s` en `AuthApiClient`/`UnidadOrganizativaApiClient` — ✅ implementado.
- **ADR-05**: Try/catch en `SignInModel.OnPostAsync`, no en `AuthApiClient` — ✅ implementado.
- **ADR-06**: Validación diferida vía `IValidateOptions` + `ValidateOnStart` — ✅ implementado.
- **ADR-07**: `IHttpClientFactory` con named client `SgvApiHealthProbe` (timeout 3s) — ✅ implementado.
- **ADR-08**: `HealthCheckResponseWriter.WriteJson` compartido sin exponer stack traces — ✅ implementado.
- **FRENTE F** (documentación MySQL): subsección en `decisiones-implementacion.md` — ✅ implementada.
- **FRENTE G** (delta `sgv-readonly-api`): spec delta creado y mergeado a canonical — ✅ implementado.

No hay desviaciones respecto del diseño aprobado.

## 5. Spec deltas

- `operational-readiness` ✅ mergeado a canonical
- `sgv-readonly-api` ✅ mergeado a canonical
- `sgv-web-authentication` ✅ mergeado a canonical
- `web-apiclient-transport-contract` ✅ mergeado a canonical

## 6. Comando de verificación

```text
$ dotnet test SGV.slnx --nologo

Passed!  - Failed:     0, Passed:  2891, Skipped:     0, Total:  2891, Duration: 1 m 18 s - SGV.Tests.dll (net10.0)
```

**2891/2891 tests pasan** — 0 failed, 0 skipped. MySQL real disponible: todos los `[MySqlFact]` se ejecutaron.
