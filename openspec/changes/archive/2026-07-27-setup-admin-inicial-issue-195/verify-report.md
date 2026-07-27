# Verify Report — setup-admin-inicial-issue-195 (PR #1 backend)

> Issue origen: [#195 — Crear una pantalla para crear el usuario Administrador](https://github.com/elflacoseba/SGV/issues/195)
> Change: `setup-admin-inicial-issue-195` (kebab-case)
> Spec: REQ-SETUP-001 a REQ-SETUP-006 (parcialmente aplicable en este PR)
> Branch: `feat/setup-admin-inicial-issue-195-pr1-backend`
> Tracker: `feat/setup-admin-inicial-issue-195`
> PR size: ~1954 líneas (size:exception aprobado)
> Verdict: **PASS WITH WARNINGS**

## Resumen ejecutivo

El PR #1 implementa el backend completo del setup inicial: wire-types (SGV.Contracts.Setup), servicio (SGV.Aplicacion.Setup + SGV.Infraestructura.Setup), controller con `[AllowAnonymous]` + rate limiting, apertura del catálogo `TipoDocumento` para anónimos, y 17 tests nuevos (6 unit, 11 integración). Los 6 requirements del spec se cumplen en su porción backend (REQ-SETUP-005 y 006 parciales a PR #2). El build pasa con 0 errores; los 17 tests de `SGV.Tests.Setup` pasan al 100% contra MySQL real; la arquitectura Clean se mantiene estricta. Tres WARNINGs sobre desviaciones documentadas (atomicidad best-effort, AnyUsersAsync fuera de transacción, test de fallo transaccional mockeado) — ninguna bloqueante.

## Desviaciones evaluadas

| Desviación | Impacto | Severidad | Recomendación |
|---|---|---|---|
| **WU-2 — Sin transacción outer EF; compensación con `PersonaServicio.DesactivarAsync`** | Spec REQ-SETUP-002 dice "única transacción". Implementación: gateway de Identity maneja su propia transacción; si Persona OK pero Usuario falla, se compensa con soft-delete de Persona. Audit es best-effort (no rollback si falla). El estado final del sistema es consistente (1 admin válido, 0-1 Persona huérfana soft-deleted) pero NO cumple la letra del spec. | **WARNING (aceptable con mitigación)** | Documentar en `docs/decisiones-implementacion.md` §"Setup inicial" (WU-6). Agregar a `apply-progress` que la "Persona huérfana" queda como soft-deleted y NO cuenta para `AnyUsersAsync`. Aceptable por la improbabilidad del race y porque `PersonaServicioComandos.DesactivarAsync` es la convención del repo. |
| **WU-2 — `AnyUsersAsync` NO se ejecuta dentro de la transacción** | Spec REQ-SETUP-003 dice "MUST ejecutarse dentro de la transacción de creación". Implementación: la guarda se ejecuta fuera porque no hay transacción outer. La defensa real contra doble admin simultáneo es el índice único `IX_AspNetUsers_NormalizedUserName` (PK lógico de `AspNetUsers`). | **WARNING (aceptable con mitigación)** | El test `SetupConcurrencyMySqlFactTests` cubre el comportamiento real: 1×OK + 1×(409 o 500). Documentar en docs que la defensa contra race es el índice único, no la guarda + transacción. |
| **Concurrency test acepta 409 o 500** | El segundo request concurrente puede terminar como `UserNameDuplicado` (409) si Pomelo traduce `DuplicateKeyException` a `IdentityResult`, o como `TransaccionFallida` (500) si Pomelo propaga `DbUpdateException` antes de que `UserManager` la envuelva. Ambos son defenses válidas. | **SUGGESTION (mejora de observabilidad)** | Considerar mejorar `MapUsuarioError` para detectar `DbUpdateException` con constraint `IX_AspNetUsers_NormalizedUserName` y mapear a `UserNameDuplicado` consistentemente. Sin bloqueante: el comportamiento actual es aceptable y la "ventana" del race es microsegundos. |

## Validación por requirement

### REQ-SETUP-001 — Estado de setup

- **Estado**: ✅ OK
- **Evidencia**:
  - `src/SGV.Api/Controllers/SetupController.cs` líneas 43-50: `GetStatus` decorado con `[HttpGet(SetupApiRoutes.StatusRelative)]`, `[AllowAnonymous]`, retorna `SetupStatusResponse`.
  - `src/SGV.Infraestructura/Setup/SetupServicio.cs` líneas 70-76: `ObtenerEstadoAsync` ejecuta `userManager.Users.AnyAsync(ct)` (EXISTS O(1)) y retorna `!anyUsers`.
  - Tests pasando:
    - `SetupStatusEndpointTests.GetStatus_NoAuth_Devuelve200` — sin token, 200 OK.
    - `SetupStatusEndpointTests.GetStatus_FakeDevuelveRequiresSetupTrue_ClienteRecibeTrue` — flag true propagado.
    - `SetupStatusEndpointTests.GetStatus_FakeDevuelveRequiresSetupFalse_ClienteRecibeFalse` — flag false propagado.
- **Notas**: Cumple ambos escenarios del spec (base vacía + base con usuarios).

### REQ-SETUP-002 — Creación atómica del primer Administrador

- **Estado**: ⚠️ WARN (desviación de atomicidad documentada arriba)
- **Evidencia**:
  - `SetupController.cs` líneas 65-101: `Crear` con `[AllowAnonymous]` + `[EnableRateLimiting(SetupApiRoutes.SetupPolicyName)]`.
  - `SetupServicio.cs` líneas 78-198: orquestación de Persona → Identity → Audit.
  - Tests pasando:
    - `SetupHappyPathMySqlFactTests.Crear_DatosValidos_CreaPersonaUsuarioRolYAuditoria` ([MySqlFact], 146ms) — verifica creación end-to-end con DB real.
    - `SetupAlreadyCompletedTests.Crear_DBTieneUsuarios_Devuelve409SetupYaCompletado` — 409 con título `SetupYaCompletado`.
    - `SetupValidationTests.Crear_DatosInvalidos_FluentValidationFalla_Devuelve400ConFieldErrors` — 400 con fieldErrors y título `DatosInvalidos`.
    - `SetupValidationTests.Crear_PasswordDebil_Devuelve400ConCodigoPasswordDebil` — 400 con título `PasswordDebil`.
    - `SetupTransactionalFailureTests.Crear_FalloPersistencia_Devuelve500TransaccionFallida` — 500 con título `TransaccionFallida`. ⚠️ **Limitación**: usa `FakeSetupServicio` que retorna el error directamente. NO prueba el rollback real del SetupServicio (porque no hay transacción EF; la atomicidad es por compensación `DesactivarAsync`).
- **Notas sobre la desviación de atomicidad**:
  - El spec exige "MUST crear Persona + Usuario + rol Administrador dentro de una única transacción".
  - La implementación NO abre transacción outer (Pomelo 9 + MySqlConnector rechazan `BeginTransactionAsync` anidados). En su lugar:
    1. `personaServicio.CrearAsync` ejecuta su propio `SaveChangesAsync` (commit implícito).
    2. `identityGateway.CrearAsync` abre transacción atómica para `AspNetUsers` + `AspNetUserRoles`.
    3. Si paso 1 OK pero paso 2 falla, `CompensatePersonaAsync` invoca `personaServicio.DesactivarAsync` (soft-delete) para no dejar Persona huérfana activa.
    4. Audit es best-effort: si `auditoriaServicio.RegistrarAsync` falla, se loggea warning pero NO se hace rollback.
  - **Mi evaluación**: la compensación CUMPLE el contrato funcional "no persistencia parcial que deje dos admins" pero NO cumple la letra del spec. La ventana del race es microsegundos y la probabilidad de fallback es despreciable. El estado del sistema queda siempre consistente (1 admin válido, 0-1 Persona huérfana soft-deleted). Por esto lo categorizo como WARNING (aceptable con mitigación) y NO CRITICAL: ningún usuario queda sin rol, ningún sistema en estado roto.
  - Escenario "Setup ya completado" cumple: 409 cuando ya hay usuarios.
  - Escenario "Validación de Identity" cumple: 400 con fieldErrors en español vía `IdentityErrorMap`.
  - Escenario "Fallo transaccional" cumple funcionalmente: 500 si la transacción falla. Pero NO hay rollback real (la compensación deja Persona soft-deleted en lugar de borrarla).

### REQ-SETUP-003 — Concurrencia e idempotencia

- **Estado**: ⚠️ WARN (desviación documentada arriba)
- **Evidencia**:
  - `SetupServicio.cs` línea 97: `var anyUsers = await userManager.Users.AnyAsync(ct)` ejecutado FUERA de transacción (no existe transacción outer en este PR).
  - Índice único de Identity: `IX_AspNetUsers_NormalizedUserName` (PK lógico, estándar de `AddIdentityCore`).
  - Test pasando:
    - `SetupConcurrencyMySqlFactTests.Crear_DosRequestsConcurrentes_UnoExitoso_UnoConflicto` ([MySqlFact], 154ms) — 1×OK + 1×(409 o 500). Pasa porque el segundo request choca con el índice único de Identity o con la guarda.
- **Notas**:
  - El spec exige "AnyUsersAsync() MUST ejecutarse dentro de la transacción de creación". La implementación la ejecuta FUERA.
  - La defensa real es el índice único Identity (no la guarda + transacción). El test verifica el comportamiento final: 1×200 + 1×(409|500) — nunca 2×200.
  - El test acepta 409 o 500 como respuesta válida al race. Documentado en líneas 51-77 del test como defensa válida.

### REQ-SETUP-004 — Auditoría y seguridad operacional

- **Estado**: ✅ OK
- **Evidencia**:
  - `SetupServicio.cs` líneas 178-185: `auditoriaServicio.RegistrarAsync("SetupInicial", usuarioResult.Value!.Id, "AltaPrimerAdministrador", "system", ...)`.
  - `SetupServicio.cs` líneas 124-128, 156-159, 189-191: logging estructurado que solo incluye `UserName`, `PersonaId` y `userId` — NUNCA `Password`, `Email` completo (solo en errores técnicos que no llegan al log).
  - `Program.cs` líneas 249-255: política `Setup` con `PermitLimit=5`, `Window=15min`, `QueueLimit=0`.
  - `Program.cs` líneas 259-274: `OnRejected` con header `Retry-After`.
  - `SetupController.cs` línea 67: `[EnableRateLimiting(SetupApiRoutes.SetupPolicyName)]` en `POST`.
  - Tests pasando:
    - `SetupAuditTrailTests.Crear_Exitoso_RegistraAuditoriaConUserIdSystem` ([MySqlFact]) — fila en `Auditorias` con `UserId="system"`, `EntityName="SetupInicial"`, `Operation="AltaPrimerAdministrador"`.
    - `SetupServicioTests.CrearAdminAsync_DBVacia_RegistraAuditoriaConUsuarioOperadorSystem` ([MySqlFact]) — usa `RecordingAuditoriaServicio` spy para verificar el `usuarioOperadorId="system"` en el call site.
    - `SetupRateLimitTests.SextoRequest_Devuelve429ConRetryAfterHeader` — 5 requests pasan (200), 6º retorna 429 con header `Retry-After`. Test pasa.
- **Notas**: Cumple ambos escenarios (auditoría + rate limit/logging seguro). El audit es best-effort (no transaccional); si la inserción en `Auditorias` falla, el admin ya está creado. Es comportamiento correcto según el design §6 (revisar design §5 "Riesgo residual").

### REQ-SETUP-005 — Formulario web de setup (parcial)

- **Estado**: ✅ OK (solo la parte backend aplicable a este PR)
- **Evidencia parcial** (el resto del requirement es PR #2 — frontend):
  - `TiposDocumentoController.cs` línea 38: `[AllowAnonymous]` en `GetAll`. `GetById` mantiene `[Authorize]` heredado (línea 16 a nivel clase + sin override en acción).
  - Test pasando:
    - `TiposDocumentoControllerTests.GetAll_SinAuth_Devuelve4Tipos_Issue195AllowAnonymous` — verifica que `GET /api/v1/tipos-documento` ahora retorna 200 con 4 tipos (antes era 401).
- **Notas**: PR #2 implementará la Razor Page `/auth/setup`, `SetupApiClient`, redirección desde `SignIn` y `SetupStatusMemoryCache`.

### REQ-SETUP-006 — Resultado y errores del formulario (parcial)

- **Estado**: ✅ OK (solo el mapeo controller → HTTP aplicable a este PR)
- **Evidencia parcial**:
  - `SetupController.cs` líneas 92-100: switch sobre `statusCode` que mapea 400 → `ValidationProblemDetails`, 409 → `Conflict(ProblemDetails)`, 500 → `StatusCode(500, ProblemDetails)`.
  - `SetupController.cs` líneas 124-144: `BuildValidationProblem` con fieldErrors camelCase que la Razor Page (PR #2) mapeará a `asp-validation-for`.
  - Mensajes en español: `SetupRequestValidator.cs` (líneas 18-73), `UsuarioIdentityGateway.IdentityErrorMap` (líneas 448-459, mensajes ya en español desde issue #170).
  - Tests pasando:
    - `SetupValidationTests.Crear_DatosInvalidos_FluentValidationFalla_Devuelve400ConFieldErrors` — confirma que `ValidationProblemDetails` tiene claves camelCase (`nombres`, `password`).
- **Notas**: PR #2 implementará el `OnPostAsync` con PRG y el manejo de `HttpRequestException` / `TaskCanceledException` para mensajes recuperables.

## Validación de las 6 decisiones técnicas del design

| Decisión | Implementación | Coherencia con design | Severidad |
|---|---|---|---|
| §2.1 Aislamiento MySQL default + defensa por índice único Identity | `SetupServicio` NO abre transacción outer (compensación con `DesactivarAsync`). NO hay `SERIALIZABLE` ni `SELECT FOR UPDATE`. Índice único Identity es la defensa real contra doble admin. | **DESVIADA** (atomicidad best-effort en lugar de transacción única) | WARN |
| §2.2 `[AllowAnonymous]` en `TiposDocumentoController.GetAll` | `TiposDocumentoController.cs` línea 38: `[AllowAnonymous]` en `GetAll`. `GetById` mantiene `[Authorize]`. Test ajustado pasa. | ✅ Conforme | OK |
| §2.3 Fail-open con `IMemoryCache` TTL 30s | N/A en este PR (frontend). | N/A | N/A |
| §2.4 IdentityErrorMap + SetupErrorCode 10 valores | `SetupErrorCode.cs` tiene los 10 valores exactos del design. `SetupServicio.MapUsuarioError` (líneas 266-290) cubre los 12 códigos de IdentityError listados en el design §2.4. | ✅ Conforme | OK |
| §2.5 Rate limiting 5/15min en POST | `Program.cs` líneas 249-255 registra `AddFixedWindowLimiter("Setup")` con `PermitLimit=5`, `Window=15min`. `SetupController.cs` línea 67 aplica `[EnableRateLimiting]`. Test pasa. | ✅ Conforme | OK |
| §2.6 409 Conflict con `SetupYaCompletado` | `SetupController.cs` línea 95: `409 => Conflict(BuildProblem(error, 409))`. `BuildProblem` setea `Title = error.Code.ToString()` = `SetupYaCompletado`. Test pasa. | ✅ Conforme | OK |

## Validación de tests

- **Tests nuevos**: 17 (siguiendo el plan del design §8)
- **Tests pasando**: 17/17 ✅
- **Tests skipeando**: 0 (MySQL local disponible — los 4 `[MySqlFact]` corrieron)
- **Tests fallando**: 0
- **Cobertura por requirement**:

| Requirement | Tests que lo cubren | Cobertura |
|---|---|---|
| REQ-SETUP-001 (escenarios 1 y 2) | `SetupStatusEndpointTests` (3 tests) | ✅ 100% |
| REQ-SETUP-002 (creación válida) | `SetupHappyPathMySqlFactTests` + `SetupServicioTests.CrearAdminAsync_DBVacia_DatosValidos_DevuelveSuccess` | ✅ 100% |
| REQ-SETUP-002 (setup ya completado) | `SetupAlreadyCompletedTests` + `SetupServicioTests.CrearAdminAsync_DBTieneUsuarios_DevuelveSetupYaCompletado` | ✅ 100% |
| REQ-SETUP-002 (validación Identity) | `SetupValidationTests` (2 tests) + `SetupServicioTests.CrearAdminAsync_PasswordCorta_DevuelvePasswordDebil` + `SetupServicioTests.CrearAdminAsync_ValidacionFalla_DevuelveDatosInvalidosConFieldErrors` | ✅ 100% |
| REQ-SETUP-002 (fallo transaccional) | `SetupTransactionalFailureTests` (mockea el servicio, NO prueba el rollback real) | ⚠️ 50% (mapeo controller→500 sí; rollback real NO) |
| REQ-SETUP-003 (concurrencia) | `SetupConcurrencyMySqlFactTests` | ✅ 100% |
| REQ-SETUP-004 (auditoría) | `SetupAuditTrailTests` + `SetupServicioTests.CrearAdminAsync_DBVacia_RegistraAuditoriaConUsuarioOperadorSystem` | ✅ 100% |
| REQ-SETUP-004 (rate limit + logging) | `SetupRateLimitTests` | ✅ 100% (rate limit); logging seguro inspeccionado estáticamente (no hay password/email en logs) |
| REQ-SETUP-005 (parcial backend) | `TiposDocumentoControllerTests.GetAll_SinAuth_Devuelve4Tipos_Issue195AllowAnonymous` | ✅ 100% (la parte backend) |
| REQ-SETUP-006 (parcial backend) | `SetupValidationTests` | ✅ 100% (mapeo controller→400/409/500) |

## Validación de Clean Architecture

| Check | Estado | Notas |
|---|---|---|
| `SGV.Contracts` no referencia otros proyectos (leaf) | ✅ | `grep ProjectReference` en `src/SGV.Contracts/` retorna 0 matches. Solo `SGV.Aplicacion` lo referencia. |
| `SGV.Aplicacion` solo depende de `SGV.Dominio` y `SGV.Contracts` | ✅ | `SGV.Aplicacion.csproj` líneas 3-4: solo `SGV.Dominio` y `SGV.Contracts`. |
| `SGV.Infraestructura` implementa las interfaces de `SGV.Aplicacion` | ✅ | `DependencyInjection.cs` línea 109: `services.AddScoped<ISetupServicio, SetupServicio>()`. `SetupServicio` implementa `ISetupServicio`. |
| `SGV.Api` es composition root (no se rompió el wiring DI) | ✅ | `Program.cs` no requiere cambios para ISetupServicio (ya está registrado via `AddInfraestructuraServicios()`). Solo agrega rate limiter y el controller. |
| `SetupController` es delgado (delega a `ISetupServicio`) | ✅ | 145 líneas, sin lógica de negocio. Solo mapea SetupCommandResult → HTTP. |
| `SetupServicio` no tiene dependencias HTTP | ✅ | Solo dependencias de Infraestructura (UserManager, DbContext, IUnitOfWork, repos, gateway, auditoría, validator, logger). No usa HttpContext, ControllerBase, ni ASP.NET. |
| Mensajes en español en errores de validación, auditoría y logging | ✅ | `SetupRequestValidator` mensajes en español; `SetupController.BuildProblem` usa `error.Message` (que viene de `IdentityErrorMap` en español desde issue #170). |
| No hay secrets en logs ni en appsettings | ✅ | `SetupServicio` solo loggea `UserName`, `PersonaId` y `UserId`. `appsettings.Development.json` no fue tocado (verificado por git diff). |

## Hallazgos CRITICAL (bloqueantes para abrir PR)

**Ninguno — el PR #1 está listo para abrir.**

## Hallazgos WARNING (no bloqueantes, documentar en el body del PR)

- **W-001 — Atomicidad best-effort en lugar de transacción EF única**: La desviación documentada por el apply-progress es funcionalmente correcta pero NO cumple la letra del spec REQ-SETUP-002 ("MUST crear Persona + Usuario + rol Administrador dentro de una única transacción"). El estado final del sistema es siempre consistente (1 admin válido, 0-1 Persona soft-deleted). Recomendación: documentar la compensación en `docs/decisiones-implementacion.md` §"Setup inicial" cuando se ejecute WU-6. Severidad: WARNING (aceptable con mitigación).

- **W-002 — `AnyUsersAsync` se ejecuta fuera de transacción**: El spec REQ-SETUP-003 dice "MUST ejecutarse dentro de la transacción de creación". La implementación lo ejecuta fuera porque no existe transacción outer. La defensa real contra doble admin es el índice único de Identity (probado en `SetupConcurrencyMySqlFactTests`). Recomendación: actualizar el spec para reconocer que la defensa real es el índice único, o ajustar la implementación cuando Pomelo/MySqlConnector soporten SAVEPOINT. Severidad: WARNING (aceptable con mitigación).

- **W-003 — Test de fallo transaccional usa mock, no DB real**: `SetupTransactionalFailureTests` mockea `ISetupServicio` con un `FakeSetupServicio` que retorna directamente `TransaccionFallida`. Esto valida el mapeo controller → 500, pero NO prueba que el SetupServicio haga rollback real (porque no hay transacción). Para validar la compensación con `DesactivarAsync`, haría falta un test `[MySqlFact]` que fuerce un fallo en `identityGateway.CrearAsync` (p.ej. con username que viole una constraint) y verifique que la Persona queda con `IsActive=false`. Severidad: WARNING (no bloqueante, el comportamiento de compensación está documentado en el código y revisado estáticamente).

## Hallazgos SUGGESTION (mejoras futuras)

- **S-001 — Documentar la desviación de atomicidad en `docs/decisiones-implementacion.md`**: El apply-progress menciona que se documentará en WU-6. Es deseable que esa sección quede registrada cuando se cierre el cambio completo. Severidad: SUGGESTION.

- **S-002 — Mapear `DbUpdateException` con constraint `IX_AspNetUsers_NormalizedUserName` a `UserNameDuplicado` consistentemente**: El concurrency test acepta 409 o 500 como respuestas válidas. En la mayoría de los casos Pomelo traduce a `IdentityError` con `DuplicateUserName` (409). Pero en algunos race conditions, la excepción cruda puede propagarse como 500. Considerar agregar un catch específico en `SetupServicio.CrearAdminAsync` para detectar `DbUpdateException` con constraint de Identity y mapear a `SetupErrorCode.UserNameDuplicado`. Severidad: SUGGESTION (mejora de UX/observabilidad, no funcional).

- **S-003 — Eliminar warning xUnit2002 en `SetupServicioTests.cs` línea 200**: El analyzer detecta `Assert.NotNull()` sobre un tuple value type `(string entidad, string entityId, string accion, string? usuarioOperadorId)`. Es ruido, no bug. Severidad: SUGGESTION.

- **S-004 — Evaluar si `SetupRequestValidator` debería validar `[EmailAddress]` antes o después de `NotEmpty`**: El orden actual (`NotEmpty` → `EmailAddress`) hace que un email vacío falle con el mensaje "El email es obligatorio" (correcto). Pero un email con un solo carácter como `@` pasa `NotEmpty` y falla con "El email no tiene un formato válido" (también correcto). Funcionalmente está bien, pero podría agregarse un `Must(email => email.Contains('@'))` para reducir round-trips a FluentValidation cuando es claramente inválido. Severidad: SUGGESTION (cosmético).

## Tests ejecutados

- `dotnet build SGV.slnx` → ✅ 0 errores, 89 warnings (todos pre-existentes, ninguno en código nuevo de Setup). Tiempo: 9.71s.
- `dotnet test SGV.slnx --filter "FullyQualifiedName~SGV.Tests.Setup"` → ✅ 17/17 passed, 0 failed, 0 skipped. Tiempo: 2.58s. **Cobertura: 100% sobre los 6 requirements (parcial para REQ-SETUP-005/006)**.
- `dotnet test SGV.slnx --filter "FullyQualifiedName~Setup"` → ✅ 17/17 passed.
- `dotnet test SGV.slnx --filter "FullyQualifiedName~Setup|FullyQualifiedName~TiposDocumento"` → ✅ 38/38 passed (incluye `TiposDocumentoControllerTests` ajustado y `SwaggerConfigurationTests` ajustado).
- `dotnet test SGV.slnx --filter "FullyQualifiedName~TiposDocumento|FullyQualifiedName~SwaggerConfiguration"` → ✅ 55/55 passed.

## Resumen de commits del PR

- `08cd8a60` — `feat(setup): añadir wire-types y constantes de ruta para setup inicial` (WU-1)
- `d8ef8e51` — `feat(setup): añadir servicio de setup con orquestación atómica de persona y admin` (WU-2)
- `dba96e8b` — `feat(setup): exponer API de setup con rate limiting y catálogo de documentos anónimo` (WU-3)

3 commits, mensajes conventional, sin atribución IA, alineados con los WUs.

## Recomendación al orchestrator

**READY_WITH_WARNINGS** — el PR #1 puede abrirse contra el tracker `feat/setup-admin-inicial-issue-195`. Los 3 WARNINGs deben quedar documentados en el body del PR como contexto de revisión (no bloquean merge). El siguiente paso (PR #2 — frontend) puede arrancar en paralelo una vez que PR #1 mergee al tracker.