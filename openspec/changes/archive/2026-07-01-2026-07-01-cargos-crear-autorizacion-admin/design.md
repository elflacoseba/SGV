# Design: Autorización diferenciada en CargosController

## Technical Approach

La implementación seguirá el patrón ya usado en `src/SGV.Api/Controllers/UsuariosController.cs`: autorización declarativa con atributos en ASP.NET Core, sin policies custom ni cambios en `Program.cs`. `src/SGV.Api/Controllers/CargosController.cs` pasará a tener `[Authorize]` a nivel controller para imponer autenticación base en todas las acciones, y cada mutación (`Create`, `Update`, `Delete`, `Reactivate`, `UpsertSkill`, `DeleteSkill`) agregará `[Authorize(Roles = RolesSgv.Administrador)]` para elevar el requisito de rol. Los GET (`GetAll`, `GetById`, `GetSkills`) conservarán sus contratos actuales y solo cambiarán su requisito de acceso, alineado con los delta specs de `cargo-management`, `cargo-skill-query-contract` y `sgv-readonly-api`.

## Architecture Decisions

### Decision: Nivel de granularidad del `[Authorize]`
**Choice**: `[Authorize]` a nivel controller + `[Authorize(Roles = RolesSgv.Administrador)]` en mutaciones.
**Alternatives considered**: decorar cada acción individualmente; policy global en `Program.cs`.
**Rationale**: reduce repetición, sigue el patrón vigente en `UsuariosController` y evita side effects sobre otros controllers públicos, como exige el change.

### Decision: Uso de constante `RolesSgv.Administrador`
**Choice**: referenciar `src/SGV.Aplicacion/Seguridad/RolesSgv.cs` desde `SGV.Api`.
**Alternatives considered**: string literal `"Admin"` o `"Administrador"`; nueva policy nominal.
**Rationale**: la constante ya está sembrada en Identity y elimina drift entre autorización declarativa, seed y tests.

### Decision: Extensión mínima del harness de auth fake
**Choice**: ampliar `FakeAuthenticationDefaults` y `FakeAuthenticationHandler` para distinguir admin vs usuario autenticado no-admin por header de prueba.
**Alternatives considered**: segundo scheme de autenticación; mockear `IAuthorizationService`.
**Rationale**: el harness actual ya controla identidad por `Authorization` header. Extenderlo mantiene los tests de integración cerca del pipeline real (`UseAuthentication` + `UseAuthorization`) y permite probar `401`, `403` y `2xx` sin tocar producción.

## Data Flow

```text
HTTP request
   |
   v
Authentication middleware
   |-- sin header válido -> 401
   v
ClaimsPrincipal fake/JWT
   |
   v
Authorize attribute en CargosController
   |-- GET: requiere usuario autenticado
   |-- POST/PUT/DELETE/PATCH/skills mutación: requiere rol Administrador
   |      `-- autenticado sin rol -> 403
   v
Action method
   v
Servicios de Aplicacion (consultas/comandos)
   v
Respuesta existente (2xx/4xx funcional)
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/SGV.Api/Controllers/CargosController.cs` | Modify | Agregar `using Microsoft.AspNetCore.Authorization`, `using SGV.Aplicacion.Seguridad`, baseline `[Authorize]` y overrides admin por acción mutante; documentar `401/403` en metadata HTTP relevante. |
| `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` | Modify | Incorporar un segundo principal fake autenticado sin rol `Administrador` y exponer headers reutilizables para tests. |
| `tests/SGV.Tests/Api/CargosControllerTests.cs` | Modify | Invertir expectativas de acceso: GET anónimo `401`, GET autenticado `2xx`, mutaciones no-admin `403`, admin `2xx`, y actualizar el test de atributos. |
| `tests/SGV.Tests/Api/CargoSkillControllerTests.cs` | Modify | Cubrir `GET /skills` autenticado y mutaciones admin-only con matriz `401/403/2xx`. |

## Interfaces / Contracts

No hay contratos públicos nuevos en producción. En el harness de pruebas se ampliará el contrato interno de autenticación fake:

- `FakeAuthenticationDefaults.AdminHeader`: principal con rol `RolesSgv.Administrador`.
- `FakeAuthenticationDefaults.UserHeader` (o equivalente): principal autenticado sin claim de administrador.
- `FakeAuthenticationHandler`: mapeará el valor del header `Authorization: Test <tipo>` a claims distintos, preservando `NoResult()` cuando no haya credenciales para que ASP.NET Core emita `401` real.

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| API integration | Atributos y enforcement de `CargosController` | WebApplicationFactory con auth fake: anónimo `401`, usuario `403` en mutaciones, admin `2xx`. |
| API integration | No regresión de contratos de lectura/mutación | Reusar asserts actuales de payload/ProblemDetails bajo credenciales correctas. |
| API integration | Aislamiento del resto de la API pública | Mantener `UsuariosControllerTests` y no tocar `Program.cs`; `sgv-readonly-api` exige que otros GET públicos sigan anónimos. |

## Migration / Rollout

No migration required.

## Open Questions

- [ ] None.
