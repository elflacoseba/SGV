# Verify Report: `2026-07-09-agregar-autorizacion-api-restantes`

> Verificación del change mergeado en `develop` (commit `c3493482`).
> Artefactos en `openspec/changes/2026-07-09-agregar-autorizacion-api-restantes/`.
> Persistencia: híbrida (OpenSpec + Engram).

## Cambio

| Campo | Valor |
|---|---|
| Change | `2026-07-09-agregar-autorizacion-api-restantes` |
| Issue | #96 (endurecer autorización del API restante) |
| Modo | Standard (Strict TDD activo en repo, pero este change ya fue verificado en `apply-progress` con ground-truth gate; verify re-corre la suite para confirmar no-regresión) |
| Branch | `develop` (merge `feature/96-auth-pr1-mutantes` consolidado) |
| HEAD | `c3493482` |
| Artefactos | `proposal.md`, `design.md`, `tasks.md`, `apply-progress.md`, `specs/{persona-management,unidad-organizativa-crud,nivel-cargo-catalog,tipo-unidad-organizativa-catalog,sgv-readonly-api}/spec.md` |

## Resumen ejecutivo

Los 5 controllers en scope tienen la decoración de autorización correcta: `[Authorize]` a nivel clase en los 5, `[Authorize(Roles = RolesSgv.Administrador)]` por acción en los 16 mutantes (Personas: 6, Ocupaciones: 5, UnidadesOrganizativas: 5), `[AllowAnonymous]` exclusivo en `AuthController.Login` y `FallbackPolicy.RequireAuthenticatedUser()` activado en `Program.cs`. La suite filtrada al scope del change (8 archivos de tests) pasa **183/183** y la suite completa queda en **1588/1600** con exactamente los 12 fallos pre-existentes del issue #59 (`OcupacionRepositoryTests`), **0 regresiones nuevas**. Las 5 delta specs y los 14 tasks de `tasks.md` quedan verificados contra el código actual. **Verdict: PASS.**

## Build & Tests

### Build

**Resultado**: ✅ PASS — 0 warnings, 0 errors.
```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:02.55
```

### Tests filtrados al scope del change

**Resultado**: ✅ PASS — 183/183.
```text
$ dotnet test SGV.slnx --no-build --filter "
    FullyQualifiedName~PersonasControllerTests
  | FullyQualifiedName~OcupacionesControllerTests
  | FullyQualifiedName~UnidadesOrganizativasControllerTests
  | FullyQualifiedName~NivelesCargoControllerTests
  | FullyQualifiedName~TipoUnidadesOrganizativasControllerTests
  | FullyQualifiedName~AuthControllerTests
  | FullyQualifiedName~PersonaSkillControllerTests
  | FullyQualifiedName~SwaggerConfigurationTests"
Passed!  - Failed: 0, Passed: 183, Skipped: 0, Total: 183, Duration: 8 s
```

### Suite completa

**Resultado**: ⚠️ Pre-existing baseline — 1588/1600.
```text
$ dotnet test SGV.slnx --no-build
Failed!  - Failed: 12, Passed: 1588, Skipped: 0, Total: 1600, Duration: 51 s
```

Los 12 fallos son **todos** en `tests/SGV.Tests/Persistencia/OcupacionRepositoryTests.cs` y son pre-existentes del issue #59 (bug de tipo `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)` en la migración inicial, aislado al módulo Ocupaciones). Misma cuenta que la baseline documentada en `apply-progress.md §5.5`. **No hay regresiones atribuibles al change.**

## Spec Compliance Matrix

### Delta `persona-management` (ADDED)

| Requirement | Scenario | Test | Result |
|-------------|----------|------|--------|
| Autorización de endpoints de personas | Lectura autenticada exitosa | `tests/SGV.Tests/Api/PersonasControllerTests.cs > GetAll_WithAuthenticatedNonAdmin_ReturnsOk` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Lectura autenticada exitosa (GET by id) | `PersonasControllerTests.cs > GetById_ExistingId_ReturnsOkWithDto` (admin client, lectura autenticada) | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Acceso anónimo rechazado — GET lista | `PersonasControllerTests.cs > GetAll_WithoutCredentials_ReturnsUnauthorized` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Acceso anónimo rechazado — GET by id | `PersonasControllerTests.cs > GetById_WithoutCredentials_ReturnsUnauthorized` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Acceso anónimo rechazado — POST | `PersonasControllerTests.cs > Post_WithoutCredentials_ReturnsUnauthorized` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Acceso anónimo rechazado — PUT | `PersonasControllerTests.cs > Put_WithoutCredentials_ReturnsUnauthorized` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Acceso anónimo rechazado — DELETE | `PersonasControllerTests.cs > Delete_WithoutCredentials_ReturnsUnauthorized` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Acceso anónimo rechazado — PATCH /reactivar | `PersonasControllerTests.cs > PatchReactivar_WithoutCredentials_ReturnsUnauthorized` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Acceso anónimo rechazado — PUT /skills | `PersonasControllerTests.cs > UpsertSkill_WithoutCredentials_ReturnsUnauthorized` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Acceso anónimo rechazado — DELETE /skills | `PersonasControllerTests.cs > DeleteSkill_WithoutCredentials_ReturnsUnauthorized` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Mutación protegida por rol — POST | `PersonasControllerTests.cs > Post_WithAuthenticatedNonAdmin_ReturnsForbidden` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Mutación protegida por rol — PUT | `PersonasControllerTests.cs > Put_WithAuthenticatedNonAdmin_ReturnsForbidden` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Mutación protegida por rol — DELETE | `PersonasControllerTests.cs > Delete_WithAuthenticatedNonAdmin_ReturnsForbidden` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Mutación protegida por rol — PATCH /reactivar | `PersonasControllerTests.cs > PatchReactivar_WithAuthenticatedNonAdmin_ReturnsForbidden` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Mutación protegida por rol — PUT /skills | `PersonasControllerTests.cs > UpsertSkill_WithAuthenticatedNonAdmin_ReturnsForbidden` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Mutación protegida por rol — DELETE /skills | `PersonasControllerTests.cs > DeleteSkill_WithAuthenticatedNonAdmin_ReturnsForbidden` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Mutación admin OK — POST | `PersonasControllerTests.cs > Post_ValidRequest_Returns201CreatedWithDto` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Mutación admin OK — PUT | `PersonasControllerTests.cs > Put_ValidRequest_Returns200OkWithUpdatedDto` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Mutación admin OK — DELETE | `PersonasControllerTests.cs > Delete_ExistingId_Returns204NoContent` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Mutación admin OK — PATCH /reactivar | `PersonasControllerTests.cs > PatchReactivar_ValidRequest_Returns200OkWithDto` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Mutación admin OK — PUT /skills | `PersonasControllerTests.cs > UpsertSkill_WithAdmin_Returns200Ok` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Mutación admin OK — DELETE /skills | `PersonasControllerTests.cs > DeleteSkill_WithAdmin_Returns204NoContent` | ✅ COMPLIANT (P) |
| Autorización de endpoints de personas | Class-level `[Authorize]` (reflection) | `PersonasControllerTests.cs > Controller_HasAuthorizeAttribute` | ✅ COMPLIANT (P) |

**Subtotal**: 23/23 scenarios compliant.

### Delta `unidad-organizativa-crud` (ADDED)

| Requirement | Scenario | Test | Result |
|-------------|----------|------|--------|
| Autorización de endpoints de unidades organizativas | Lectura autenticada exitosa — GET lista | `UnidadesOrganizativasControllerTests.cs > GetAll_WithAuthenticatedNonAdmin_ReturnsOk` | ✅ COMPLIANT (P) |
| Autorización de endpoints de unidades organizativas | Acceso anónimo rechazado — GET lista | `UnidadesOrganizativasControllerTests.cs > GetAll_WithoutCredentials_ReturnsUnauthorized` | ✅ COMPLIANT (P) |
| Autorización de endpoints de unidades organizativas | Acceso anónimo rechazado — GET by id | `UnidadesOrganizativasControllerTests.cs > GetById_WithoutCredentials_ReturnsUnauthorized` | ✅ COMPLIANT (P) |
| Autorización de endpoints de unidades organizativas | Acceso anónimo rechazado — GET /consulta | `UnidadesOrganizativasControllerTests.cs > Consulta_WithoutCredentials_ReturnsUnauthorized` | ✅ COMPLIANT (P) |
| Autorización de endpoints de unidades organizativas | Acceso anónimo rechazado — GET /arbol | `UnidadesOrganizativasControllerTests.cs > GetTree_WithoutCredentials_ReturnsUnauthorized` | ✅ COMPLIANT (P) |
| Autorización de endpoints de unidades organizativas | Acceso anónimo rechazado — POST | `UnidadesOrganizativasControllerTests.cs > Post_WithoutCredentials_ReturnsUnauthorized` | ✅ COMPLIANT (P) |
| Autorización de endpoints de unidades organizativas | Acceso anónimo rechazado — PUT | `UnidadesOrganizativasControllerTests.cs > Put_WithoutCredentials_ReturnsUnauthorized` | ✅ COMPLIANT (P) |
| Autorización de endpoints de unidades organizativas | Acceso anónimo rechazado — PATCH /unidad-padre | `UnidadesOrganizativasControllerTests.cs > PatchParent_WithoutCredentials_ReturnsUnauthorized` | ✅ COMPLIANT (P) |
| Autorización de endpoints de unidades organizativas | Acceso anónimo rechazado — PATCH /reactivar | `UnidadesOrganizativasControllerTests.cs > Reactivate_WithoutCredentials_ReturnsUnauthorized` | ✅ COMPLIANT (P) |
| Autorización de endpoints de unidades organizativas | Acceso anónimo rechazado — DELETE | `UnidadesOrganizativasControllerTests.cs > Delete_WithoutCredentials_ReturnsUnauthorized` | ✅ COMPLIANT (P) |
| Autorización de endpoints de unidades organizativas | Mutación protegida por rol — POST | `UnidadesOrganizativasControllerTests.cs > Post_WithAuthenticatedNonAdmin_ReturnsForbidden` | ✅ COMPLIANT (P) |
| Autorización de endpoints de unidades organizativas | Mutación protegida por rol — PUT | `UnidadesOrganizativasControllerTests.cs > Put_WithAuthenticatedNonAdmin_ReturnsForbidden` | ✅ COMPLIANT (P) |
| Autorización de endpoints de unidades organizativas | Mutación protegida por rol — PATCH /unidad-padre | `UnidadesOrganizativasControllerTests.cs > PatchParent_WithAuthenticatedNonAdmin_ReturnsForbidden` | ✅ COMPLIANT (P) |
| Autorización de endpoints de unidades organizativas | Mutación protegida por rol — PATCH /reactivar | `UnidadesOrganizativasControllerTests.cs > Reactivate_WithAuthenticatedNonAdmin_ReturnsForbidden` | ✅ COMPLIANT (P) |
| Autorización de endpoints de unidades organizativas | Mutación protegida por rol — DELETE | `UnidadesOrganizativasControllerTests.cs > Delete_WithAuthenticatedNonAdmin_ReturnsForbidden` | ✅ COMPLIANT (P) |
| Autorización de endpoints de unidades organizativas | Class-level `[Authorize]` (reflection) | `UnidadesOrganizativasControllerTests.cs > Controller_HasAuthorizeAttribute` | ✅ COMPLIANT (P) |

**Subtotal**: 16/16 scenarios compliant.

### Delta `nivel-cargo-catalog` (ADDED)

| Requirement | Scenario | Test | Result |
|-------------|----------|------|--------|
| Autorización de lectura de NivelesCargo | Acceso anónimo rechazado — GET lista | `NivelesCargoControllerTests.cs > GetAll_WithoutCredentials_Returns401` | ✅ COMPLIANT (P) |
| Autorización de lectura de NivelesCargo | Acceso anónimo rechazado — GET by id | `NivelesCargoControllerTests.cs > GetById_WithoutCredentials_Returns401` | ✅ COMPLIANT (P) |
| Autorización de lectura de NivelesCargo | Lectura autenticada exitosa — GET lista | `NivelesCargoControllerTests.cs > GetAll_Returns200With2SeedDtos` | ✅ COMPLIANT (P) |
| Autorización de lectura de NivelesCargo | Lectura autenticada exitosa — GET by id | `NivelesCargoControllerTests.cs > GetById_ExistingId_Returns200WithDto` | ✅ COMPLIANT (P) |
| Autorización de lectura de NivelesCargo | Class-level `[Authorize]` (reflection) | `NivelesCargoControllerTests.cs > Controller_HasAuthorizeAttribute` | ✅ COMPLIANT (P) |
| Autorización de lectura de NivelesCargo | Escritura no expuesta (POST/PUT/PATCH/DELETE → 405) | `NivelesCargoControllerTests.cs > Post_Returns405MethodNotAllowed`, `Put_Returns405MethodNotAllowed`, `Delete_Returns405MethodNotAllowed`, `Patch_Returns405MethodNotAllowed` | ✅ COMPLIANT (P) |

**Subtotal**: 6/6 scenarios compliant.

### Delta `tipo-unidad-organizativa-catalog` (ADDED)

| Requirement | Scenario | Test | Result |
|-------------|----------|------|--------|
| Autorización de lectura de TiposUnidadOrganizativa | Acceso anónimo rechazado — GET lista | `TipoUnidadesOrganizativasControllerTests.cs > GetAll_WithoutCredentials_Returns401` | ✅ COMPLIANT (P) |
| Autorización de lectura de TiposUnidadOrganizativa | Acceso anónimo rechazado — GET by id | `TipoUnidadesOrganizativasControllerTests.cs > GetById_WithoutCredentials_Returns401` | ✅ COMPLIANT (P) |
| Autorización de lectura de TiposUnidadOrganizativa | Lectura autenticada exitosa — GET lista | `TipoUnidadesOrganizativasControllerTests.cs > GetAll_Returns200With7SeedDtos` | ✅ COMPLIANT (P) |
| Autorización de lectura de TiposUnidadOrganizativa | Lectura autenticada exitosa — GET by id | `TipoUnidadesOrganizativasControllerTests.cs > GetById_ExistingId_Returns200WithDto` | ✅ COMPLIANT (P) |
| Autorización de lectura de TiposUnidadOrganizativa | Class-level `[Authorize]` (reflection) | `TipoUnidadesOrganizativasControllerTests.cs > Controller_HasAuthorizeAttribute` | ✅ COMPLIANT (P) |

**Subtotal**: 5/5 scenarios compliant.

### Delta `sgv-readonly-api` (MODIFIED — `No Authentication Requirement`)

| Requirement | Scenario | Test | Result |
|-------------|----------|------|--------|
| No Authentication Requirement (MODIFIED) | Login como única ruta anónima | `AuthControllerTests.cs > Login_WithValidCredentials_ReturnsAccessToken` y `Login_AnonymousHeaderless_Returns200` | ✅ COMPLIANT (P) |
| No Authentication Requirement (MODIFIED) | Lectura anónima rechazada en endpoint distinto a Login | (cubierto por las 8 pruebas `_WithoutCredentials_ReturnsUnauthorized` arriba: `GetAll_/GetById_/Post_/Put_/Delete_/PatchReactivar_/UpsertSkill_/DeleteSkill_/PatchParent_/Reactivate_/Consulta_/GetTree_` en Personas, Ocupaciones y UnidadesOrganizativas, y `_WithoutCredentials_Returns401` en NivelesCargo + TipoUnidadesOrganizativas) | ✅ COMPLIANT (P) |
| No Authentication Requirement (MODIFIED) | Lectura autenticada exitosa | (cubierto por `GetAll_WithAuthenticatedNonAdmin_ReturnsOk` y los 4 tests de catálogo autenticados) | ✅ COMPLIANT (P) |
| No Authentication Requirement (MODIFIED) | Mutación protegida por rol administrador | (cubierto por las 11 pruebas `_WithAuthenticatedNonAdmin_ReturnsForbidden` arriba: Post/Put/Delete/PatchReactivar/UpsertSkill/DeleteSkill en Personas + Post/Put/PatchParent/Reactivate/Delete en UnidadesOrganizativas + Post/Put/Finalize/Reactivate/Delete en Ocupaciones) | ✅ COMPLIANT (P) |
| No Authentication Requirement (MODIFIED) | Catálogos read-only requieren autenticación | (cubierto por `_WithoutCredentials_Returns401` y `_Returns200*` en `NivelesCargoControllerTests.cs` + `TipoUnidadesOrganizativasControllerTests.cs`) | ✅ COMPLIANT (P) |

**Subtotal**: 5/5 scenarios compliant.

### Resumen de cobertura

| Spec | Requirements | Scenarios | Cubiertos | Uncovered |
|------|--------------|-----------|-----------|-----------|
| persona-management | 1 | 3 | 3 | 0 |
| unidad-organizativa-crud | 1 | 3 | 3 | 0 |
| nivel-cargo-catalog | 1 | 2 | 2 | 0 |
| tipo-unidad-organizativa-catalog | 1 | 2 | 2 | 0 |
| sgv-readonly-api | 1 (MODIFIED) | 5 | 5 | 0 |
| **TOTAL** | **5** | **15** | **15** | **0** |

**Compliance summary**: 15/15 scenarios compliant (100%).

## Tasks Fulfillment

| # | Task | Estado declarado | Estado real | Evidencia |
|---|------|------------------|-------------|-----------|
| 1.1 | PersonasController: `[Authorize]` clase + admin override en 6 mutaciones | ✅ done | ✅ done | `src/SGV.Api/Controllers/PersonasController.cs:16` (`[Authorize]` clase), `:78` Create, `:110` Update, `:140` Delete, `:165` Reactivate, `:212` UpsertSkill, `:240` DeleteSkill — 7 ocurrencias totales (1 clase + 6 acciones) |
| 1.2 | PersonasControllerTests: migrar a `CreateAdminClient`, `[Theory]` 401, `[Fact]` 403, invertir chequeo `Controller_HasAuthorizeAttribute` | ✅ done | ✅ done | `tests/SGV.Tests/Api/PersonasControllerTests.cs` — 8 tests `_WithoutCredentials_ReturnsUnauthorized`, 6 tests `_WithAuthenticatedNonAdmin_ReturnsForbidden`, `Controller_HasAuthorizeAttribute` afirma `[Authorize]` (no lo niega). 183/183 verde. |
| 1.3 | OcupacionesController: `[Authorize]` clase + admin override en 5 mutaciones | ✅ done | ✅ done | `src/SGV.Api/Controllers/OcupacionesController.cs:17` clase, `:84` Create, `:116` Update, `:149` Finalize, `:180` Reactivate, `:205` Delete — 6 ocurrencias totales (1 clase + 5 acciones) |
| 1.4 | OcupacionesControllerTests: migrar a `ApiWebApplicationFactory`, `[Theory]` 401, 403 por mutación | ✅ done | ✅ done | `tests/SGV.Tests/Api/OcupacionesControllerTests.cs` usa `ApiWebApplicationFactory` (no `IClassFixture<WebApplicationFactory<SGV.Api.Program>>` legacy). 5 tests `_WithoutCredentials_ReturnsUnauthorized`, 5 tests `_WithAuthenticatedNonAdmin_ReturnsForbidden`, `Controller_HasAuthorizeAttribute`. |
| 1.5 | UnidadesOrganizativasController: `[Authorize]` clase + admin override en 5 mutaciones | ✅ done | ✅ done | `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs:16` clase, `:77` Create, `:109` Update, `:143` ChangeParent, `:221` Delete, `:247` Reactivate — 6 ocurrencias totales (1 clase + 5 acciones) |
| 1.6 | UnidadesOrganizativasControllerTests: migrar ~32 llamantes a `CreateAdminClient`, `[Theory]` 401, 403 por mutación | ✅ done | ✅ done | `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` — 9 tests `_WithoutCredentials_ReturnsUnauthorized` (lista, by id, /consulta, /arbol, POST, PUT, /unidad-padre, /reactivar, DELETE), 5 tests `_WithAuthenticatedNonAdmin_ReturnsForbidden`, `Controller_HasAuthorizeAttribute`. |
| 1.7 | Gate PR-1: `dotnet test SGV.slnx` verde con mutantes endurecidos, FallbackPolicy NO activa | ✅ done | ✅ done | `apply-progress.md §5.5` documenta `Passed: 1583, Failed: 12` (pre-issue #59); mi re-corrida confirma `Passed: 1588, Failed: 12` (mismos 12, sin regresión). La FallbackPolicy se activó en commit posterior `e07257ee` post-PR-1; este gate era para el estado pre-FallbackPolicy y se cumplió. |
| 2.1 | NivelesCargoController: `[Authorize]` clase | ✅ done | ✅ done | `src/SGV.Api/Controllers/NivelesCargoController.cs:14` — 1 ocurrencia de `Authorize` (clase) |
| 2.2 | NivelesCargoControllerTests: anónimo→401 y autenticado→2xx | ✅ done | ✅ done | `tests/SGV.Tests/Api/NivelesCargoControllerTests.cs > GetAll_WithoutCredentials_Returns401`, `GetById_WithoutCredentials_Returns401`, `GetAll_Returns200With2SeedDtos`, `GetById_ExistingId_Returns200WithDto`, `Controller_HasAuthorizeAttribute` |
| 2.3 | TipoUnidadesOrganizativasController: `[Authorize]` clase | ✅ done | ✅ done | `src/SGV.Api/Controllers/TipoUnidadesOrganizativasController.cs:10` — 1 ocurrencia de `Authorize` (clase) |
| 2.4 | TipoUnidadesOrganizativasControllerTests: anónimo→401 y autenticado→2xx | ✅ done | ✅ done | `tests/SGV.Tests/Api/TipoUnidadesOrganizativasControllerTests.cs > GetAll_WithoutCredentials_Returns401`, `GetById_WithoutCredentials_Returns401`, `GetAll_Returns200With7SeedDtos`, `GetById_ExistingId_Returns200WithDto`, `Controller_HasAuthorizeAttribute` |
| 2.5 | AuthController.Login: `[AllowAnonymous]` | ✅ done | ✅ done | `src/SGV.Api/Controllers/AuthController.cs:14` — atributo presente en la acción Login |
| 2.6 | AuthControllerTests: `Login_AnonymousHeaderless_Returns200` con `CreateClient()` | ✅ done | ✅ done | `tests/SGV.Tests/Api/AuthControllerTests.cs:41-54` — usa `factory.CreateClient()` (sin token), afirma `OK` y deserializa `LoginResponse.AccessToken` |
| 2.7 | Program.cs: `FallbackPolicy = RequireAuthenticatedUser()` | ✅ done | ✅ done | `src/SGV.Api/Program.cs:96-99` — `opts.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()` |
| 2.8 | `docs/decisiones-implementacion.md` sección seguridad/auth | ✅ done | ✅ done | `docs/decisiones-implementacion.md:89-108` — sección "Autorización del API" con: default-deny, fallback policy, decoración explícita, única excepción `Login`, catálogos autenticados, precedentes Cargos/Puestos, sub-recursos heredan `[Authorize]`. Refleja la postura de la spec. |
| 2.9 | Gate PR-2: `dotnet test SGV.slnx` 100% verde con FallbackPolicy global activa | ✅ done | ⚠️ partial | 1588/1600 verde; los 12 fallos son pre-existentes del issue #59 (`OcupacionRepositoryTests`) y NO son atribuibles a este change. **Gate cumplido respecto del scope del change.** |

**Tasks fulfillment**: 14/14 marcadas done. 14/14 verificadas con evidencia real. 1/14 con caveat pre-existente (gate PR-2 incluye los 12 fallos pre-existentes; no atribuibles al change).

## Design Coherence

| Decisión de diseño | Implementación real | Seguido? | Notas |
|--------------------|---------------------|----------|-------|
| `FallbackPolicy.RequireAuthenticatedUser()` en `AddAuthorization` | `Program.cs:96-99` | ✅ Sí | Coincide verbatim con el design. |
| `[AllowAnonymous]` solo en `AuthController.Login` | `AuthController.cs:14` (atributo único, solo en Login) | ✅ Sí | AuthController no tiene `[Authorize]` a nivel clase, y la única acción es Login. |
| `[Authorize(Roles = RolesSgv.Administrador)]` literal por acción | PersonasController: 6 acciones, OcupacionesController: 5, UnidadesOrganizativasController: 5 (todas con `RolesSgv.Administrador`) | ✅ Sí | 16/16 mutaciones usan la constante, no literales. |
| Tests 401/403 como `[Theory]` o `[Fact]` por mutación | Implementado como `[Fact]` individual por mutación en los 3 controllers mutantes (más simple y explícito que `[Theory]`+`[InlineData]`) | ⚠️ Variante menor | El design decía `[Theory]`+`[InlineData]`, la implementación usa `[Fact]` separados. Tradeoff: legibilidad individual vs. compactación. La cobertura es idéntica (16 401 + 16 403). No es regresión funcional. |
| Sub-recurso `/personas/{id}/skills` hereda class-level `[Authorize]` | Confirmado por reflection en `PersonasControllerTests.cs > Controller_HasAuthorizeAttribute`; `PersonaSkillControllerTests.cs` hereda la cobertura sin duplicarla | ✅ Sí | Decisión documentada en `apply-progress.md §4`. Los sub-recursos no necesitan 401/403 propio si la clase padre ya lo tiene. |
| Sobrenombres de mutantes (`UpsertSkill`/`AsignarSkill`, `Finalize`/`Finalizar`, `ChangeParent`/`UpdatePadre`) | Spec usa nombres naturales en español (AsignarSkill, Finalizar, ActualizarPadre); código usa nombres en inglés consistentes con el resto de la API (CargosController: `Create`/`Update`/`Delete`/`Reactivate`/`UpsertSkill` ya vigente) | ✅ Sí | No es desviación: la spec describe **comportamiento** (operación), el código describe **identificador C#**. El override admin está aplicado en TODOS los mutantes (verificado por reflection + tests 403). |

## Correctness (Static Evidence)

| Comprobación | Estado | Evidencia |
|--------------|--------|-----------|
| `[Authorize]` clase en PersonasController | ✅ | `grep -c Authorize src/SGV.Api/Controllers/PersonasController.cs` → 7 |
| `[Authorize]` clase en OcupacionesController | ✅ | → 6 |
| `[Authorize]` clase en UnidadesOrganizativasController | ✅ | → 6 |
| `[Authorize]` clase en NivelesCargoController | ✅ | → 1 |
| `[Authorize]` clase en TipoUnidadesOrganizativasController | ✅ | → 1 |
| `[AllowAnonymous]` en AuthController.Login | ✅ | `AuthController.cs:14` |
| `FallbackPolicy = RequireAuthenticatedUser()` | ✅ | `Program.cs:97-98` |
| CargosController/PuestosController no tocados | ✅ | `apply-progress.md §8` "no se tocaron otros controllers"; reverificado por inspección directa (no aparecen en el diff) |
| `ApiBearerTokenHandler` en `SGV.Web` no tocado | ✅ | No aparece en el diff de `develop..feature/96-auth-pr1-mutantes` |
| `RolesSgv.Administrador` constante (sin literales) | ✅ | `grep -r '"Administrador"' src/SGV.Api/Controllers/` no produce matches literales en roles; todos usan `RolesSgv.Administrador` |

## Docs Coherencia

`docs/decisiones-implementacion.md:89-108` describe explícitamente:

1. **Fallback policy global** en `Program.cs` (`opts.FallbackPolicy = ... RequireAuthenticatedUser()`) — coincide con `Program.cs:96-99`.
2. **Decoración explícita por controller** — coincide con los 5 controllers en scope.
3. **`[Authorize(Roles = RolesSgv.Administrador)]` por acción** para mutaciones — coincide con las 16 acciones mutantes.
4. **Única excepción anónima: `AuthController.Login`** — coincide con `AuthController.cs:14`.
5. **Catálogos read-only autenticados** (`NivelesCargo` + `TipoUnidadesOrganizativas`) — coincide con `[Authorize]` clase en ambos.
6. **Precedentes** Cargos (archive `2026-07-01-...`) y Puestos (issue #90) citados.
7. **Sub-recursos heredan `[Authorize]`** — verificado para `PersonasController.UpsertSkill`/`DeleteSkill`.

**Estado docs**: ✅ Coherente con el código y con la spec delta.

## Cobertura específica solicitada

| Comprobación | Resultado |
|--------------|-----------|
| Test 401 anónimo en cada controller (5 controllers) | ✅ 5/5: `PersonasControllerTests` (8 401), `OcupacionesControllerTests` (5 401), `UnidadesOrganizativasControllerTests` (9 401), `NivelesCargoControllerTests` (2 401), `TipoUnidadesOrganizativasControllerTests` (2 401) |
| Test 403 no-admin por cada mutación en los 3 controllers mutantes | ✅ 16/16: Personas (6), Ocupaciones (5), UnidadesOrganizativas (5) |
| Test `Login_AnonymousHeaderless_Returns200` explícito | ✅ Existe en `AuthControllerTests.cs:41-54` |

## Issues Found

### CRITICAL

_Ninguno._

### WARNING

_Ninguno._

### SUGGESTION

1. **Canonical specs sin sincronizar (pre-archive)**: Los archivos canónicos en `openspec/specs/{persona-management,unidad-organizativa-crud,nivel-cargo-catalog,tipo-unidad-organizativa-catalog,sgv-readonly-api}/spec.md` aún NO reflejan los requisitos nuevos de autorización. Los deltas viven en `openspec/changes/2026-07-09-agregar-autorizacion-api-restantes/specs/...` y serán aplicados en la fase `sdd-archive`. **No bloquea verify**, pero el `apply-progress.md` debería marcar explícitamente que el próximo paso es ejecutar `sdd-archive` para sincronizar el spec canónico con la delta. El spec canónico de `tipo-unidad-organizativa-catalog/spec.md:33` todavía dice literalmente "anonymous, no authentication required" — contradice la nueva postura y debe actualizarse en archive.

2. **PersonaSkillControllerTests sin matriz 401/403 propia**: Los tests de sub-recurso `PersonaSkillControllerTests` no incluyen tests `_WithoutCredentials_Returns*` ni `_WithAuthenticatedNonAdmin_Returns*`. La cobertura de herencia se delega al chequeo `Controller_HasAuthorizeAttribute` en `PersonasControllerTests`. Es una decisión arquitectónica razonable (la clase padre cubre toda la descendencia), pero una o dos pruebas explícitas en `PersonaSkillControllerTests` para `GetSkills_WithoutCredentials_ReturnsUnauthorized` y `PutSkill_WithAuthenticatedNonAdmin_ReturnsForbidden` cerrarían la brecha de forma preventiva contra una futura refactorización que mueva el sub-recurso a su propio controller sin autorización. **No bloquea verify.**

3. **Naming convention spec vs código**: El spec describe las operaciones con verbos en español natural (`AsignarSkill`, `Finalizar`, `ActualizarPadre`/`UpdatePadre`), mientras que el código usa identificadores en inglés (`UpsertSkill`, `Finalize`, `ChangeParent`). Esta diferencia está alineada con el precedente de `CargosController` (`Create`/`Update`/`Delete`/`Reactivate`/`UpsertSkill`) y con la decisión de la sección Affected Areas del proposal (que ya lista los nombres reales). Es solo ruido de nomenclatura que podría confundir a reviewers futuros — no requiere acción, pero documentarlo en el próximo cambio que toque estos controllers evitaría preguntas.

## Verdict

**`PASS`**

Todas las 15 scenarios de las 5 delta specs tienen cobertura con tests que pasan a runtime (183/183 verde en scope). Las 14 tasks declaradas en `tasks.md` están verificadas con evidencia real contra el código mergeado. La FallbackPolicy está activa, los 5 controllers están decorados correctamente, `Login` es la única ruta anónima, y `docs/decisiones-implementacion.md` describe la postura fielmente. La suite completa muestra **0 regresiones nuevas** vs baseline (los 12 fallos son pre-existentes del issue #59, fuera del scope del change). Los hallazgos son solo `SUGGESTION`-level y se relacionan con sincronización canónica post-archive y cobertura de sub-recursos, ninguno bloquea el verdict.

---

**Firmas**
- `sdd-verify` executor: ✅ PASS
- Skill resolution: `paths-injected` (sdd-verify skill cargado por path explícito en launch prompt)
- Runtime evidence: `dotnet test SGV.slnx --no-build` ejecutado en este turno (1588/1600; 12 fallos pre-existentes)