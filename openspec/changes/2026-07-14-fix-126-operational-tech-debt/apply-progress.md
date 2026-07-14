# Apply Progress — Deuda operativa #126 (fix operational tech debt)

## PR 1 (CU-0) — Health / readiness infraestructura

**Branch**: `fix/126-operational-pt1` → `develop`

**Completado**: Correction pass applied.

### Corrección aplicada

Se reemplazó el health check basado en `IDbContextFactory<SgvDbContext>` por una sonda directa con `MySqlConnector.MySqlConnection`. Se restauró el registro original `AddDbContext<SgvDbContext>` y se eliminó `AddDbContextFactory`. Se corrigieron los tests `CorsAllowedOriginsValidationTests` y se revirtió el cambio de timeout en `SgvWebApplicationFactory` por ser scope creep.

### Archivos modificados
| Archivo | Acción | Descripción |
|---------|--------|-------------|
| `src/SGV.Api/Infrastructure/Health/SgvDbContextReadinessHealthCheck.cs` | Creado | Health check con conexión MySQL directa (sin EF Core) |
| `src/SGV.Api/Program.cs` | Modificado | Restaurado `AddDbContext<SgvDbContext>`, eliminado `AddDbContextFactory` |
| `tests/SGV.Tests/Api/HealthTests.cs` | Modificado | readiness unhealthy test usa connection string inválida |
| `tests/SGV.Tests/Api/CorsAllowedOriginsValidationTests.cs` | Modificado | Agregado `UseSetting` para connection string |
| `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` | Modificado | Eliminado `Timeout=10s` del override manual de `IAuthApiClient` |
| `tests/SGV.Tests/Api/StubUnhealthyDbContextFactory.cs` | Eliminado | Ya no necesario |
| `openspec/changes/2026-07-14-fix-126-operational-tech-debt/design.md` | Modificado | Actualizados §4.C, §4.E, ADR-01 |

### Resultados de verificación
- StartupValidationTests: 5/5 ✅
- API HealthTests (incl. MySqlFact): 5/5 ✅
- Web HealthTests: 6/6 ✅
- CorsAllowedOriginsValidationTests: 4/4 ✅
- JwtRealAuthTests: 3/3 ✅
- PersonasControllerTests: 34/34 ✅
- SkillsControllerTests: 37/37 ✅
- **Combined gates**: 94/94 ✅

---

## PR 2 (CU-1 + CU-2) — Timeout login + UX frontera

**Branch**: `fix/126-operational-pt2` → `develop`

### TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 1-RED | `AuthApiClientTimeoutTests.cs` | Integration | ✅ 7/8 | ✅ Written | ✅ Passed | ➖ Single | ✅ Clean |
| 1-GREEN | `Program.cs` + `SgvWebApplicationFactory.cs` | — | N/A | N/A | ✅ 3/3 | ➖ N/A | ✅ Extract comment |
| 2-RED | `SignInTransportTests.cs` | Integration | ✅ 7/8 | ✅ Written | ✅ Passed | ✅ 4 cases | ✅ Clean |
| 2-GREEN | `SignIn.cshtml.cs` | — | N/A | N/A | ✅ 14/15 | ➖ N/A | ✅ Clean |

### Completed Tasks

- [x] **1-RED**: AuthApiClientTimeoutTests — 3 tests (timeout verification + slow upstream)
- [x] **1-GREEN**: Timeout=10s en registros de AuthApiClient y UnidadOrganizativaApiClient + factory override
- [x] **2-RED**: SignInTransportTests — 4 tests (HttpRequestException, TaskCanceledException, cancellación propagada, 401 regresión)
- [x] **2-GREEN**: try/catch en SignInModel.OnPostAsync con mensajes en español

### Files Changed

| File | Action | What Was Done |
|------|--------|---------------|
| `src/SGV.Web/Program.cs` | Modified | Added `client.Timeout = TimeSpan.FromSeconds(10)` to `AuthApiClient` and `UnidadOrganizativaApiClient` registrations |
| `src/SGV.Web/Pages/Auth/SignIn.cshtml.cs` | Modified | Wrapped `LoginAsync` call in try/catch for `HttpRequestException` (transporte) and `TaskCanceledException` (timeout) with `when (!cancellationToken.IsCancellationRequested)` guard |
| `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` | Modified | Added `Timeout = TimeSpan.FromSeconds(10)` to the `IAuthApiClient` manual override |
| `tests/SGV.Tests/Web/AuthApiClientTimeoutTests.cs` | Created | 3 tests: timeout property verification for both clients + slow upstream cancellation |
| `tests/SGV.Tests/Web/SignInTransportTests.cs` | Created | 4 tests: HTTP transport errors, timeout, cancelled propagation, 401 regression |

### Deviations from Design

None — implementation matches design.

- **Mensajes en español**: `"No pudimos contactar al servicio de autenticación. Intentá nuevamente en unos minutos."` y `"La autenticación tardó demasiado. Intentá nuevamente."` según design.
- **Field name**: El primary constructor de `SignInModel` usa `ILogger<SignInModel> logger` (no `_logger`). Confirmado en código: línea 17.
- **Test reflection**: Para verificar `HttpClient.Timeout` en typed clients, se usó reflection con field name `<httpClient>P` (C# 12 primary constructor synthesized name).
- **Cancelled propagation**: Se implementó mediante invocación directa de `SignInModel.OnPostAsync` con token pre-cancelado.

### Verification Gates

| Gate | Result |
|------|--------|
| `dotnet build SGV.slnx --configuration Release` | ✅ 0 errors |
| `dotnet test --filter AuthApiClientTimeoutTests` | ✅ 3/3 pass |
| `dotnet test --filter SignInTransportTests` | ✅ 4/4 pass |
| `dotnet test --filter WebAuthenticationTests` | ✅ 7/8 pass (1 pre-existing failure: `Post_SignIn_WithValidCredentials_RedirectsToDashboardAndSetsCookie` — expected 302, got 200 — not caused by this change) |
| Combined new tests | ✅ 14/15 pass (only pre-existing failure) |

### Workload / PR Boundary

- **Mode**: stacked PR slice (PR 2 of 3)
- **Boundary**: CU-1 (timeout login) + CU-2 (UX frontera) — autonomously verifiable
- **Estimated review budget impact**: ~250 lines (new tests + implementation changes)

### Remaining Tasks (PR 3)

- [ ] 3-DOC: Delta de specs (operational-readiness + docs)
- [ ] 4-DOC: Subsección "Contrato runtime MySQL" en `docs/decisiones-implementacion.md`
- [ ] 5-VERIFY: Ejecutar suite completa y archivar change

### Next Recommended

PR 3 (CU-3..5: spec delta + docs + verify) or fresh-context reviewer for PR 2.
