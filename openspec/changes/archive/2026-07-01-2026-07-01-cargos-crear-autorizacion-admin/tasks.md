# Tasks: Autorización diferenciada en CargosController

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~180-280 |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | single PR |
| Delivery strategy | ask-always |
| Chain strategy | pending |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Low

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Auth admin en CargosController y subrecurso /skills con matriz 401/403/2xx | PR único | Toca controller, harness de auth y dos archivos de tests; bajo el budget de 400 |

## Phase 1: Foundation — extender harness de auth fake (RED → GREEN → REFACTOR)

- [x] 1.1 RED — Test en `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` que afirma que `FakeAuthenticationDefaults.UserHeader` existe (debe fallar por símbolo ausente).
- [x] 1.2 GREEN — Agregar `UserHeader = new(Scheme, "user")` y extender `FakeAuthenticationHandler` para emitir `ClaimsPrincipal` sin `ClaimTypes.Role` cuando el token sea `user`; preservar rol `Administrador` cuando sea `admin`.
- [x] 1.3 REFACTOR — Extraer helper privado `BuildPrincipal(string token)` en `FakeAuthenticationHandler` para deduplicar construcción de `ClaimsIdentity`.

## Phase 2: Core — atributos en CargosController (RED → GREEN → REFACTOR)

- [x] 2.1 RED — Test que verifica que `GET /api/v1/cargos` sin `Authorization` devuelve `401` (debe fallar).
- [x] 2.2 GREEN — En `src/SGV.Api/Controllers/CargosController.cs`: agregar `using Microsoft.AspNetCore.Authorization;`, `using SGV.Aplicacion.Seguridad;` y atributo `[Authorize]` a nivel controller.
- [x] 2.3 RED — Test que verifica que `POST /api/v1/cargos` con `UserHeader` devuelve `403` (debe fallar).
- [x] 2.4 GREEN — Agregar `[Authorize(Roles = RolesSgv.Administrador)]` en `Create`, `Update`, `Delete`, `Reactivate`, `UpsertSkill`, `DeleteSkill`.
- [x] 2.5 REFACTOR — Documentar `[ProducesResponseType(StatusCodes.Status401Unauthorized)]` en GETs y `[ProducesResponseType(StatusCodes.Status403Forbidden)]` en mutaciones; verificar cero string literal de rol.

## Phase 3: Tests — invertir expectativas y agregar matriz (RED → GREEN → REFACTOR)

- [x] 3.1 RED — Invertir `Controller_DoesNotHaveAuthorizeAttribute` en `tests/SGV.Tests/Api/CargosControllerTests.cs` a `Controller_HasAuthorizeAttribute` (debe fallar).
- [x] 3.2 GREEN — Agregar helper `CreateAuthenticatedClient()` en ambas clases de tests que setea `FakeAuthenticationDefaults.AdminHeader` por defecto; actualizar tests existentes para usarlo.
- [x] 3.3 RED — Agregar tests 401 (sin header) para `GetAll`, `GetById`, `GetSkills` en `CargosControllerTests.cs` y `CargoSkillControllerTests.cs` (deben fallar hasta que se ejecute 2.4).
- [x] 3.4 GREEN — Agregar tests 403 (con `UserHeader`) para `Create`, `Update`, `Delete`, `Reactivate`, `UpsertSkill`, `DeleteSkill`; verificar 2xx con `AdminHeader` para regresión.
- [x] 3.5 REFACTOR — Reusar helper compartido entre `CargosControllerTests.cs` y `CargoSkillControllerTests.cs` y eliminar duplicación de setup.

## Phase 4: Cleanup

- [x] 4.1 Correr `dotnet build SGV.slnx` y resolver advertencias de metadata HTTP.
- [x] 4.2 Correr `dotnet test SGV.slnx` y validar matriz 401/403/2xx sin regresiones en otros controllers.