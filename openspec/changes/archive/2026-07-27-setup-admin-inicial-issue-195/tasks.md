# Tasks — setup-admin-inicial-issue-195

> Issue origen: [#195 — Crear una pantalla para crear el usuario Administrador](https://github.com/elflacoseba/SGV/issues/195)
> Change: `setup-admin-inicial-issue-195` (kebab-case)
> Spec: REQ-SETUP-001 a REQ-SETUP-006 (`openspec/changes/setup-admin-inicial-issue-195/specs/setup/spec.md`)
> Design: `openspec/changes/setup-admin-inicial-issue-195/design.md`
> Delivery strategy: ask-always

## 1. Resumen del desglose

| WU | Nombre | Archivos nuevos | Archivos modificados | Estimación líneas | Requisitos spec | Dependencia |
|----|--------|:---:|:---:|:---:|:---:|:---:|
| WU-1 | Contracts en `SGV.Contracts/Setup/` | 5 | 0 | ~120 | REQ-SETUP-001..006 | ninguna |
| WU-2 | `SetupServicio` (Aplicación + Infraestructura) + tests unitarios | 5 | 1 | ~600 | REQ-SETUP-002..004 | WU-1 |
| WU-3 | `SetupController` + rate limit + `[AllowAnonymous]` catálogo + tests | 1 | 3 | ~500 | REQ-SETUP-001..004 | WU-1, WU-2 |
| WU-4 | Razor Page `/auth/setup` + `SetupApiClient` + tests web | 5 | 1 | ~700 | REQ-SETUP-005, REQ-SETUP-006 | WU-1, WU-3 |
| WU-5 | Filtro redirección en `SignIn` + cache + tests | 1 | 1 | ~150 | REQ-SETUP-005 (scenario redirección) | WU-4 |
| WU-6 | Documentación en `docs/decisiones-implementacion.md` | 0 | 1 | ~80 | — | cualquiera |
| **Total** | | **17** | **7** | **~2150** | **6 requirements, 14 escenarios** | |

## 2. Work-units

### WU-1: Contracts en `SGV.Contracts/Setup/`

**Objetivo**: wire-types, enum de errores y constantes de ruta que todas las capas consumen.

**Archivos nuevos**:
- `src/SGV.Contracts/Setup/SetupRequest.cs` — `sealed record` con 9 propiedades: `Nombres`, `Apellidos`, `Legajo?`, `Email`, `UserName`, `Password`, `TipoDocumentoId?`, `NumeroDocumento?`, `Telefono?`.
- `src/SGV.Contracts/Setup/SetupResult.cs` — `sealed record SetupResult(Guid PersonaId, string UserId, string UserName)`.
- `src/SGV.Contracts/Setup/SetupStatusResponse.cs` — `sealed record SetupStatusResponse(bool RequiresSetup)`.
- `src/SGV.Contracts/Setup/SetupErrorCode.cs` — `public enum SetupErrorCode` con 10 valores: `SetupYaCompletado`, `UserNameDuplicado`, `EmailDuplicado`, `PersonaConUsuario`, `EmailInvalido`, `UserNameInvalido`, `PasswordDebil`, `ValidacionIdentity`, `DatosInvalidos`, `TransaccionFallida`.
- `src/SGV.Contracts/Setup/SetupApiRoutes.cs` — `public static class` con `Base = "api/v1/setup"`, `StatusRelative = "status"`, `Status`, `SetupPolicyName = "Setup"`.

**Criterios de aceptación**:
- [ ] Compila con `dotnet build SGV.slnx` sin warnings.
- [ ] `SetupRequest` es `sealed record` con 9 propiedades, 4 opcionales (`string?`).
- [ ] `SetupErrorCode` vive en `SGV.Contracts.Setup` namespace, sin referencias a otros proyectos.
- [ ] `SetupApiRoutes` contiene `Status` como ruta absoluta `"/api/v1/setup/status"`.
- [ ] No modifica `AuthApiRoutes.cs` (design §3.1).

**Tests**: ninguno (Contracts es leaf).

**Estimación**: ~120 líneas.

**Dependencias**: ninguna. Primer PR viable standalone.

---

### WU-2: `SetupServicio` (Aplicación + Infraestructura)

**Objetivo**: implementar el puerto `ISetupServicio` en capa Aplicación y su implementación en Infraestructura, con validación FluentValidation, orquestación atómica (Persona + Usuario + Auditoría) y tests unitarios.

**Archivos nuevos**:
- `src/SGV.Aplicacion/Setup/ISetupServicio.cs` — interfaz con `ObtenerEstadoAsync()` y `CrearAdminAsync()`.
- `src/SGV.Aplicacion/Setup/SetupCommandResult.cs` — `record SetupCommandResult(bool IsSuccess, SetupResult? Value, SetupError? Error, IReadOnlyDictionary<string, string[]>? FieldErrors)` + factory methods `Success`/`Failure`.
- `src/SGV.Aplicacion/Setup/Validaciones/SetupRequestValidator.cs` — `AbstractValidator<SetupRequest>` con reglas de cada campo.
- `src/SGV.Infraestructura/Setup/SetupServicio.cs` — implementación con `UserManager<SgvIdentityUser>`, `SgvDbContext`, `IUnitOfWork`, `IPersonaRepository`, `IUsuarioIdentityGateway`, `IAuditoriaServicio`, `IValidator<SetupRequest>`, `ILogger`. Ver design §3.3.
- `tests/SGV.Tests/Setup/SetupServicioTests.cs` — unit tests con `FakeUserManager`, `FakePersonaRepository`, `FakeAuditoriaServicio`.

**Archivos modificados**:
- `src/SGV.Infraestructura/DependencyInjection.cs` — `services.AddScoped<ISetupServicio, SetupServicio>()`.

**Criterios de aceptación**:
- [ ] `CrearAdminAsync` retorna `SetupCommandResult` (no null).
- [ ] Guarda `AnyUsersAsync=true` → `SetupYaCompletado` + rollback.
- [ ] FluentValidation falla → `DatosInvalidos` + field errors.
- [ ] Identity error `DuplicateUserName` → `UserNameDuplicado` (vía `IdentityErrorMap`).
- [ ] Identity error password policy → `PasswordDebil`.
- [ ] Éxito → llama `IAuditoriaServicio.RegistrarAsync` con `usuarioOperadorId: "system"`.
- [ ] Transacción única EF: fallo en cualquier paso → rollback completo.
- [ ] `SetupServicio` inyecta `IPersonaServicioComandos`, no crea Persona directa.

**Tests**: ~6 unit tests:
- [ ] DB vacía + datos válidos → success.
- [ ] DB con usuarios → 409 `SetupYaCompletado`.
- [ ] Validación campos inválidos → `DatosInvalidos`.
- [ ] `DuplicateUserName` → mapeo correcto.
- [ ] `PasswordDebil` → mapeo correcto.
- [ ] Auditoría con `userId="system"`.

**Estimación**: ~600 líneas.

**Dependencias**: WU-1.

---

### WU-3: `SetupController` en `SGV.Api` + rate limiting + `[AllowAnonymous]` en catálogo

**Objetivo**: exponer los endpoints `GET /api/v1/setup/status` y `POST /api/v1/setup`, agregar rate limiting, abrir `TiposDocumentoController.GetAll` a anónimos, y tests de integración.

**Archivos nuevos**:
- `src/SGV.Api/Controllers/SetupController.cs` — `[ApiController][Route(SetupApiRoutes.Base)]` con `GetStatus` y `Crear`. Ver design §3.4.
- `tests/SGV.Tests/Setup/SetupStatusEndpointTests.cs` — status endpoint tests (`[MySqlFact]`).
- `tests/SGV.Tests/Setup/SetupHappyPathMySqlFactTests.cs` — creación válida con aserciones de Persona + Usuario + rol + Auditoría.
- `tests/SGV.Tests/Setup/SetupAlreadyCompletedTests.cs` — 409 scenario.
- `tests/SGV.Tests/Setup/SetupValidationTests.cs` — 400 + fieldErrors.
- `tests/SGV.Tests/Setup/SetupTransactionalFailureTests.cs` — 500 + rollback (`[Fact]` con mock).
- `tests/SGV.Tests/Setup/SetupConcurrencyMySqlFactTests.cs` — concurrencia: exactamente 1×200 + 1×409.
- `tests/SGV.Tests/Setup/SetupAuditTrailTests.cs` — auditoría con `userId="system"` (`[MySqlFact]`).
- `tests/SGV.Tests/Setup/SetupRateLimitTests.cs` — 429 con `Retry-After` (`[Fact]`).

**Archivos modificados**:
- `src/SGV.Api/Program.cs` — `options.AddFixedWindowLimiter("Setup", policy => { PermitLimit=5, Window=15min, QueueLimit=0 })`.
- `src/SGV.Api/Controllers/TiposDocumentoController.cs` — `[AllowAnonymous]` en `GetAll`; `GetById` mantiene `[Authorize]` heredado.
- `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` — registrar fakes setup si aplica.

**Criterios de aceptación**:
- [ ] `GET /api/v1/setup/status` → 200 + `SetupStatusResponse` (sin auth).
- [ ] `POST /api/v1/setup` → 200 con `SetupCommandResult` (sin auth, con rate limiting).
- [ ] Rate limit: req #6 → 429 + `Retry-After: 900`.
- [ ] Switch mapea `SetupErrorCode` → HTTP: 400 (`DatosInvalidos`, `PasswordDebil`, etc.), 409 (`SetupYaCompletado`, duplicados), 500 (transacción).
- [ ] `TiposDocumentoController.GetAll` funciona sin token.
- [ ] Test concurrente: 2 requests paralelos → 1 éxito + 1 conflicto.
- [ ] Test transaccional: fallo intermedio → rollback completo (sin Persona ni Usuario).

**Tests**: ~12 tests (unit + `[MySqlFact]` integración).

**Estimación**: ~500 líneas.

**Dependencias**: WU-1, WU-2.

---

### WU-4: Razor Page `/auth/setup` + `SetupApiClient`

**Objetivo**: pantalla de setup en la web con 9 campos, dropdown `TipoDocumento`, layout Inspinia, y typed client anónimo con cache.

**Archivos nuevos**:
- `src/SGV.Web/Integration/Setup/ISetupApiClient.cs` — interfaz con `ObtenerEstadoAsync()`, `CrearAsync()`, `GetTiposDocumentoAsync()`.
- `src/SGV.Web/Integration/Setup/SetupApiClient.cs` — typed client anónimo (sin `ApiBearerTokenHandler`), cache `IMemoryCache` TTL 30s para status, fail-open ante `HttpRequestException`/`TaskCanceledException`.
- `src/SGV.Web/Pages/Auth/Setup.cshtml` — Razor View con `_AuthLayout`, anti-forgery, 9 inputs, dropdown `TipoDocumento`, `asp-validation-summary`.
- `src/SGV.Web/Pages/Auth/Setup.cshtml.cs` — `PageModel` con `[BindProperty] InputModel Input`, `OnGetAsync` (carga dropdown), `OnPostAsync` (PRG a `/auth/sign-in` en éxito, field errors en 400, mensaje recuperable en fallo transporte).
- `tests/SGV.Tests/Web/Auth/SetupPageRenderTests.cs` — render 9 campos + dropdown + anti-forgery.
- `tests/SGV.Tests/Web/Auth/SetupSubmitSuccessTests.cs` — submit exitoso → PRG 302 + TempData.
- `tests/SGV.Tests/Web/Auth/SetupValidationFieldErrorsTests.cs` — 400 con field errors → errores por campo.
- `tests/SGV.Tests/Web/Auth/SetupTransportErrorTests.cs` — `HttpRequestException` → mensaje recuperable.
- `tests/SGV.Tests/Web/Auth/SetupStatusCacheTests.cs` — TTL 30s: 3 GETs en <30s → solo 1 hit al API.

**Archivos modificados**:
- `src/SGV.Web/Program.cs` — `builder.Services.AddMemoryCache()` + `AddHttpClient<ISetupApiClient, SetupApiClient>(client => { Timeout=10s })` sin `ApiBearerTokenHandler`.
- `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` — `WithSetupApiClient` convenience si aplica.

**Criterios de aceptación**:
- [ ] `Setup.cshtml` usa `_AuthLayout` y renderiza 9 campos + dropdown `TipoDocumento`.
- [ ] `SetupApiClient`: cache de status TTL 30s; fail-open devuelve `RequiresSetup=false`.
- [ ] `OnGetAsync` carga `SelectListItem` desde `GET /api/v1/tipos-documento`.
- [ ] `OnPostAsync` con éxito → `RedirectToPage("/auth/sign-in")` + `TempData["SetupSuccess"]`.
- [ ] `OnPostAsync` con fieldErrors → render con errores por campo.
- [ ] `OnPostAsync` con `HttpRequestException` → mensaje recuperable sin reintento automático.
- [ ] `OnPostAsync` con `TaskCanceledException` → mismo mensaje recuperable.

**Tests**: ~8 tests web.

**Estimación**: ~700 líneas.

**Dependencias**: WU-1 (Contracts), WU-3 (controller + `[AllowAnonymous]` catálogo).

---

### WU-5: Filtro de redirección en `SignInModel.OnGetAsync`

**Objetivo**: hacer que `/auth/sign-in` redirija a `/auth/setup` cuando `RequiresSetup=true`.

**Archivos nuevos**:
- `tests/SGV.Tests/Web/Auth/SignInSetupRedirectTests.cs` — tests de redirección y fail-open.

**Archivos modificados**:
- `src/SGV.Web/Pages/Auth/SignIn.cshtml.cs` — `OnGetAsync` inyecta `[FromServices] ISetupApiClient`, consulta `ObtenerEstadoAsync()`, redirige a `/auth/setup` si `RequiresSetup=true`. `OnPostAsync` intacto.

**Criterios de aceptación**:
- [ ] `OnGetAsync` llama `ISetupApiClient.ObtenerEstadoAsync`.
- [ ] `RequiresSetup=true` → `RedirectToPage("/auth/setup")` (no `Response.Redirect` directo, usar `RedirectToPage` para PRG).
- [ ] `RequiresSetup=false` → render normal del sign-in.
- [ ] API caída (fail-open) → render normal del sign-in.
- [ ] Cache hit (TTL 30s) → no round-trip al API en GETs subsecuentes.

**Tests**: ~3-4 tests:
- [ ] DB vacía mock → redirect.
- [ ] DB con usuarios mock → render normal.
- [ ] API caída → render normal (fail-open).
- [ ] Cache hit → solo 1 llamada API en múltiples GETs.

**Estimación**: ~150 líneas.

**Dependencias**: WU-4 (necesita `ISetupApiClient`).

---

### WU-6: Documentación

**Objetivo**: registrar la decisión arquitectónica del setup inicial.

**Archivos modificados**:
- `docs/decisiones-implementacion.md` — nueva sección §"Setup inicial — issue #195" con: problema, decisión arquitectónica, 6 decisiones técnicas (design §2.1-2.6), riesgos residuales (design §9), referencia a la issue.

**Criterios de aceptación**:
- [ ] Sección documentada sin modificar otras secciones existentes.
- [ ] Incluye tradeoff de `REPEATABLE READ` + índice único (design §2.1).
- [ ] Incluye decisión de `[AllowAnonymous]` en catálogo + 409 vs 404.
- [ ] Incluye rate limiting y fail-open con cache.

**Tests**: ninguno.

**Estimación**: ~80 líneas.

**Dependencias**: cualquiera. Puede ir en cualquier PR o al final.

---

## 3. Orden de ejecución sugerido

```
WU-1 (Contracts, ~120) → WU-2 (Servicios, ~600) → WU-3 (Controller + tests, ~500)
                                                           ↓
                                              WU-4 (Razor Page + ApiClient + tests web, ~700)
                                                           ↓
                                              WU-5 (Filtro SignIn + tests, ~150)
                                                           ↓
                                              WU-6 (Docs, ~80)
```

WU-6 puede ejecutarse en cualquier momento después de WU-1.

---

## 4. PR slicing

### Opción A: Single PR
- Un único PR con todas las WU en orden.
- Estimación: **~2150 líneas** — excede ampliamente el budget de 400.
- **No recomendado**.

### Opción B: Chained PRs en 2 slices
| PR | WUs | Scope | Líneas | Budget 400 |
|----|-----|-------|:------:|:----------:|
| PR #1 | WU-1 + WU-2 + WU-3 | Backend completo (Contracts + Servicios + Controller + tests) | ~1220 | **Excede** |
| PR #2 | WU-4 + WU-5 + WU-6 | Frontend + docs (Razor Page + ApiClient + redirect + tests) | ~930 | **Excede** |
- **Razonable**: PR #1 es backend completo y revisable; PR #2 es frontend completo.

### Opción C: Chained PRs en 3 slices (RECOMENDADA)
| PR | WUs | Scope | Líneas | Budget 400 |
|----|-----|-------|:------:|:----------:|
| PR #1 | WU-1 + WU-2 | Contracts + Servicios + tests unitarios | ~720 | Excede |
| PR #2 | WU-3 | Controller + rate limit + `[AllowAnonymous]` + tests `[MySqlFact]` | ~500 | Excede |
| PR #3 | WU-4 + WU-5 + WU-6 | Web + redirect + docs | ~930 | Excede |
- **Mejor relación revisabilidad/volumen**: cada PR tiene un scope vertical completo y autónomo.

### Opción D: Chained PRs en slices finos (budget estricto)
| PR | WUs | Líneas | Budget 400 |
|----|-----|:------:|:----------:|
| PR #1 | WU-1 | ~120 | OK ✅ |
| PR #2 | WU-2 | ~600 | **Excede** — requiere `size:exception` |
| PR #3 | WU-3 | ~500 | **Excede** — requiere `size:exception` |
| PR #4 | WU-4 | ~700 | **Excede** — requiere `size:exception` |
| PR #5 | WU-5 | ~150 | OK ✅ |
| PR #6 | WU-6 | ~80 | OK ✅ |
- **Fragmentación excesiva**: WU-2, WU-3 y WU-4 individualmente exceden 400. No se gana nada contra Opción C.

---

## 5. Decisión recomendada

**Opción C recomendada** (3 PRs encadenados): cada PR es un slice vertical completo (backend lógico → API → frontend) y revisable de forma independiente. PR #1 y PR #2 exceden 400 líneas pero son cohesivos; el reviewer tiene contexto del design completo.

**Chain strategy sugerida**: `feature-branch-chain` con un tracker branch `feat/setup-admin-inicial`:
- PR #1 base = `feat/setup-admin-inicial-issue-195` (tracker).
- PR #2 base = branch de PR #1.
- PR #3 base = branch de PR #2.
- Solo el tracker mergea a `main`.

Si se prefiere velocidad y commits directos a main, usar `stacked-to-main`.

---

## 6. Review Workload Forecast

```
Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High
```

| Campo | Valor |
|-------|-------|
| Estimated changed lines | ~2150 |
| 400-line budget risk | **High** — total + 4 de 6 WUs exceden individualmente |
| Chained PRs recommended | **Yes** |
| Suggested split | PR #1 (WU-1+WU-2) → PR #2 (WU-3) → PR #3 (WU-4+WU-5+WU-6) |
| Delivery strategy | `ask-always` |
| Chain strategy | **pending** — elegir entre `stacked-to-main` o `feature-branch-chain` |
| Decision needed before apply | **Yes** — el orchestrator debe preguntar al usuario qué chain strategy usar |

---

## 7. Criterios de aceptación globales (verificación final)

- [ ] Todos los REQ-SETUP-001 a 006 están cubiertos por tests (unitarios + `[MySqlFact]` + web).
- [ ] `dotnet build SGV.slnx` sin warnings en todos los proyectos.
- [ ] `dotnet test SGV.slnx` sin fallos (incluyendo `[MySqlFact]` si MySQL está disponible; los `[MySqlFact]` se skipean automáticamente sin conexión).
- [ ] `bun run build` en `src/SGV.Web` sin errores.
- [ ] Smoke test manual: DB vacía → setup → login → acceso a página protegida.
- [ ] Auditoría con `userId="system"` verificada en base de datos.
- [ ] Rate limit: 6º request en 15 min → 429 + `Retry-After: 900`.
- [ ] `TiposDocumentoController.GetAll` funciona sin token; `GetById` lo exige.

---

## 8. Out of scope (recordatorio)

- Selección de roles (siempre `Administrador`).
- Email de verificación.
- Cambios en `PersonasController`/`UsuariosController`.
- Seed programático.
- Re-autenticación automática después del setup.
- Migración de esquema (setup usa tablas existentes).
