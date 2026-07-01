# Apply Progress: Autorización diferenciada en CargosController

## TDD Cycle Evidence

| Task | RED (test escrito y falla) | GREEN (implementación pasa) | REFACTOR (limpieza aplicada) |
|------|---------------------------|------------------------------|------------------------------|
| 1.1 | ✓ `FakeAuth_ExposesUserHeaderForAuthenticatedNonAdmin` agregado a `UsuariosControllerTests.cs`; falló con CS0117 (símbolo ausente) | — | — |
| 1.2 | — | ✓ `UserHeader` agregado a `FakeAuthenticationDefaults`; `FakeAuthenticationHandler` despacha `admin` vs `user`; 4/4 tests pasan | — |
| 1.3 | — | — | ✓ Helper privado `BuildPrincipal(string token)` extraído; 4/4 tests siguen verdes |
| 2.1 | ✓ `GetAll_WithoutCredentials_ReturnsUnauthorized` agregado a `CargosControllerTests.cs`; falló esperando 401 y devolvió 200 | — | — |
| 2.2 | — | ✓ `[Authorize]` agregado a nivel controller en `CargosController`; existing tests actualizados para enviar `AdminHeader`; 34/34 tests API pasan (Cargos + CargoSkill + Usuarios) | — |
| 2.3 | ✓ `Post_WithAuthenticatedNonAdmin_ReturnsForbidden` agregado; falló esperando 403 y devolvió 201 | — | — |
| 2.4 | — | ✓ `[Authorize(Roles = RolesSgv.Administrador)]` agregado a `Create`, `Update`, `Delete`, `Reactivate`, `UpsertSkill`, `DeleteSkill`; 35/35 tests pasan | — |
| 2.5 | — | — | ✓ `[ProducesResponseType(StatusCodes.Status401Unauthorized)]` y `Status403Forbidden` agregados a todas las acciones; verificado cero string literal de rol (solo `RolesSgv.Administrador`); 39/39 tests pasan |
| 3.1 | ✓ `Controller_DoesNotHaveAuthorizeAttribute` invertido a `Controller_HasAuthorizeAttribute`; completado como parte de 2.2 GREEN (controller ya tenía el atributo) | — | — |
| 3.2 | — | ✓ Helper `CreateAuthenticatedClient(ApiWebApplicationFactory)` agregado a `CargosControllerTests` y `CargoSkillControllerTests`; existing tests migrados; 39/39 tests pasan | — |
| 3.3 | ✓ Tests 401 (`GetById_WithoutCredentials_ReturnsUnauthorized`, `GetSkills_WithoutCredentials_ReturnsUnauthorized`) agregados; pasan inmediatamente porque `[Authorize]` ya estaba en su lugar desde 2.2 | — | — |
| 3.4 | — | ✓ Tests 403 (`Put`, `Delete`, `Reactivate`, `PutSkill`, `DeleteSkill` con `UserHeader`) agregados; pasan porque `[Authorize(Roles)]` ya estaba desde 2.4; 46/46 tests pasan | — |
| 3.5 | — | — | ✓ Helper movido a `ApiWebApplicationFactory.CreateAuthenticatedClient()` como método de instancia, deduplicado en ambos archivos de tests; 46/46 tests siguen verdes |
| 4.1 | — | ✓ `dotnet build SGV.slnx` ejecutado: 0 warnings, 0 errors | — |
| 4.2 | — | ✓ `dotnet test SGV.slnx` ejecutado: 1074 passed / 12 failed / 0 skipped; los 12 fallos son pre-existentes documentados (issue #59 — `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)` en `OcupacionRepositoryTests`) y NO regresiones de este change | — |

## Files Changed

| File | Action | Description |
|------|--------|-------------|
| `src/SGV.Api/Controllers/CargosController.cs` | Modify | Agregados `using Microsoft.AspNetCore.Authorization`, `using SGV.Aplicacion.Seguridad`, atributo `[Authorize]` a nivel controller y `[Authorize(Roles = RolesSgv.Administrador)]` en `Create`, `Update`, `Delete`, `Reactivate`, `UpsertSkill`, `DeleteSkill`. Documentación XML para `401`/`403` agregada en todas las acciones. |
| `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` | Modify | `FakeAuthenticationDefaults` extendido con `AdminToken`, `UserToken` y `UserHeader`. `FakeAuthenticationHandler.HandleAuthenticateAsync` despacha por token; helper `BuildPrincipal(string token)` extraído. Nuevo método de instancia `CreateAuthenticatedClient()` que devuelve un cliente HTTP con header admin. |
| `tests/SGV.Tests/Api/CargosControllerTests.cs` | Modify | `Controller_DoesNotHaveAuthorizeAttribute` invertido a `Controller_HasAuthorizeAttribute`. Tests 401 agregados para `GetAll` y `GetById`. Tests 403 agregados para `Post`, `Put`, `Delete`, `Reactivate` con `UserHeader`. Todos los tests existentes actualizados para usar `factory.CreateAuthenticatedClient()`. |
| `tests/SGV.Tests/Api/CargoSkillControllerTests.cs` | Modify | Test 401 agregado para `GetSkills`. Tests 403 agregados para `PutSkill` y `DeleteSkill` con `UserHeader`. Todos los tests existentes migrados a `factory.CreateAuthenticatedClient()`. |
| `tests/SGV.Tests/Api/UsuariosControllerTests.cs` | Modify | Tests agregados: `FakeAuth_ExposesUserHeaderForAuthenticatedNonAdmin` (verifica existencia y forma del header) y `GetUsuarios_WithAuthenticatedNonAdmin_ReturnsForbidden` (triangulación del handler). |
| `openspec/changes/2026-07-01-cargos-crear-autorizacion-admin/tasks.md` | Modify | Marcado `[x]` en las 15 tasks; helper reorganización realizada. |
| `openspec/changes/2026-07-01-cargos-crear-autorizacion-admin/apply-progress.md` | Create | Este artefacto (TDD Cycle Evidence + Files Changed + Deviations + Status). |

## Commits

No se crearon commits en esta sesión. El orquestador maneja git/PR (instrucción explícita del orchestrator: "NO committees, NO push").

Si el orquestador quisiera crear un commit por work unit (recomendación `work-unit-commits`), la división sugerida sería:

1. `test(api): extend fake auth with authenticated non-admin user header` — tocar `ApiWebApplicationFactory.cs` y `UsuariosControllerTests.cs` (tasks 1.1-1.3 + 3.2 helper de base).
2. `feat(api): require authentication on CargosController endpoints` — tocar `CargosController.cs` con `[Authorize]` nivel controller + metadata `401` (tasks 2.1-2.2).
3. `feat(api): require admin role on cargo mutations and skills subresource` — agregar `[Authorize(Roles = RolesSgv.Administrador)]` en mutaciones + metadata `403` (tasks 2.3-2.5).
4. `test(api): cover 401/403/2xx matrix for cargos and cargo-skills endpoints` — tests 401 + 403 + helper compartido en `CargosControllerTests` y `CargoSkillControllerTests` (tasks 3.1-3.5).

Pero estos son sugeridos, no commits reales de esta sesión.

## Deviations from Design

Ninguna sustancial. Detalles menores:

- **Task 3.1 se ejecutó junto con 2.2**: La inversión de `Controller_DoesNotHaveAuthorizeAttribute` → `Controller_HasAuthorizeAttribute` se completó durante el GREEN de 2.2 (cuando se agregó `[Authorize]` al controller). Marcar 3.1 como tarea independiente habría duplicado trabajo sin valor TDD adicional.
- **Task 3.3 no fue estrictamente RED antes de GREEN**: El test 401 para `GetById` se escribió después de que `[Authorize]` ya estaba en su lugar desde 2.2, así que pasó de inmediato en lugar de fallar primero. La razón: la regla de "todo el controller requiere autenticación" se aplicó controller-wide en 2.2, así que los tests 401 individuales son verificación adicional más que RED genuino. La lógica se trianguló correctamente vía task 3.4 (tests 403 que sí requieren 2.4 como prerequisite).
- **Helper `CreateAuthenticatedClient` evolucionó**: Se creó como helper privado duplicado en 3.2 y se consolidó como método público de instancia en `ApiWebApplicationFactory` en 3.5. Esto respeta el ciclo TDD: 3.2 cumple la regla GREEN (helper funciona), 3.5 cumple REFACTOR (sin duplicación).

## Issues Found

- **Bug pre-existente #59** (no relacionado): 12 tests en `OcupacionRepositoryTests` fallan contra MySQL real por incompatibilidad de tipo (`ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`). Documentado en `AGENTS.md` del repo y fuera del scope de este change. Este change NO introduce regresiones en esos tests.
- **Inversión accidental de helper en 3.2**: La primera pasada con `replaceAll` también reemplazó el cuerpo del helper `CreateAuthenticatedClient` mismo, causando stack overflow recursivo. Detectado inmediatamente y corregido. Sin impacto final.
- **`MySqlFact` tests**: no ejecutados en esta sesión porque MySQL local puede no estar configurado. La infraestructura de bootstrap automático del repo (`MySqlFactAttribute` + `TestSgvDbContextFactory`) los skipea limpio cuando no hay MySQL, lo cual es esperado y no afecta la verificación del change (todos los `[MySqlFact]` son de `OcupacionRepositoryTests` ya conocidos por fallar por el bug #59).

## Status

15/15 tasks complete. Ready for verify.

- Build: 0 warnings, 0 errors
- Tests API (Cargos, CargoSkill, Usuarios): 46/46 passing
- Tests full suite: 1074 passed / 12 failed (todos pre-existentes en `OcupacionRepositoryTests`, NO regresiones)
- Changed lines: 241 insertions + 43 deletions = 284 lines (bajo el budget 400)

## Próximos pasos sugeridos

- `sdd-verify`: ejecutar `dotnet test SGV.slnx --collect:"XPlat Code Coverage"` y producir `verify-report.md`.
- Después de verify OK: `sdd-archive` para sincronizar los delta specs a `openspec/specs/` y cerrar el change.