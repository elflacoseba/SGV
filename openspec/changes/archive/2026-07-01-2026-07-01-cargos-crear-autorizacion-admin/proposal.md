# Proposal: Autorización diferenciada en CargosController

## Intent

Cerrar el gap detectado en la review 4R del PR #61: hoy `CargosController` expone lecturas y mutaciones sin autorización, aunque el slice web ya asume acceso protegido. Este change formaliza una regla consistente en la API: lectura autenticada y cambios de estado solo para `Administrador`.

## Scope

### In Scope
- Proteger `CargosController` con `[Authorize]` a nivel controller.
- Restringir `POST`, `PUT`, `PATCH`, `DELETE` y mutaciones de `/api/v1/cargos/{id}/skills` a `RolesSgv.Administrador`.
- Ajustar tests API para cubrir anónimo→`401`, autenticado sin rol admin→`403`, admin→`2xx`.

### Out of Scope / Non-goals
- Auditoría de otros controllers fuera de `CargosController`.
- Nuevos roles, policies custom o cambios en `Program.cs`.
- Seeders de Identity o cambios en `DatosSemilla`.
- Frontend Razor Pages y contratos funcionales no relacionados con autorización.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `cargo-management`: los endpoints de consulta pasan a requerir autenticación y las mutaciones pasan a requerir rol `Administrador`.
- `cargo-skill-query-contract`: `GET /api/v1/cargos/{cargoId}/skills` pasa a requerir autenticación; las mutaciones del subrecurso requieren rol `Administrador`.
- `sgv-readonly-api`: se acota la regla de acceso anónimo para excluir lecturas de cargos y su subrecurso de skills.

## Approach

Aplicar baseline autenticado en `CargosController` y elevar permisos solo en mutaciones con `[Authorize(Roles = RolesSgv.Administrador)]`. Reusar el patrón ya vigente en `UsuariosController`, sin policies nuevas, y extender el harness de auth fake para probar `403` con un usuario autenticado no-admin.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Api/Controllers/CargosController.cs` | Modified | Autorización base autenticada y overrides admin en mutaciones. |
| `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` | Modified | Nuevo principal fake autenticado sin rol admin. |
| `tests/SGV.Tests/Api/CargosControllerTests.cs` | Modified | Invertir expectativas actuales y cubrir 401/403/2xx. |
| `tests/SGV.Tests/Api/CargoSkillControllerTests.cs` | Modified | Validar protección de `GET /skills` y mutaciones admin-only. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Usar `"Admin"` literal en vez de `RolesSgv.Administrador` | Media | Reusar la constante canónica existente. |
| Cobertura incompleta de `403` | Media | Extender fake auth y agregar casos explícitos no-admin. |

## Rollback Plan

Revertir los atributos `[Authorize]` agregados en `CargosController` y restaurar el harness/tests previos de acceso anónimo.

## Dependencies

- Roles ya sembrados mediante `RolesSgv.Administrador`.
- Patrón de autorización existente en `UsuariosController`.

## Success Criteria

- [ ] `GET /api/v1/cargos`, `GET /api/v1/cargos/{id}` y `GET /api/v1/cargos/{id}/skills` devuelven `401` sin credenciales.
- [ ] Las mutaciones de `CargosController` y `/skills` devuelven `403` para autenticado no-admin.
- [ ] Las mismas mutaciones siguen devolviendo `2xx` para `Administrador` con payload válido.
- [ ] La implementación usa `RolesSgv.Administrador` sin introducir policies ni seeds nuevos.
