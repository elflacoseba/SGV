# Proposal: Expone el botón Editar por fila en Puestos y asegura la frontera admin en PuestosController

## Intent

La página `Edit` de Puestos ya está mergeada en `develop` (PR #93) con entry point desde `Details` (PR #94, `Details.cshtml:87`). Quedan dos brechas:

1. **UI**: `Index.cshtml` (líneas 187-214) solo renderiza `Detalle` + `Eliminar` por fila activa. El spec `openspec/specs/puesto-web-listado-detalle-baja/spec.md:27` exige también `Editar`. La brecha pasó inadvertida porque `PuestoIndexPageTests` no asserta el botón.
2. **Seguridad**: `PuestosController.cs` no expone `[Authorize]`. Paridad con `CargosController` (archive `2026-07-01-2026-07-01-cargos-crear-autorizacion-admin`): GETs autenticados, writes restringidos a `RolesSgv.Administrador`.

Cambio quirúrgico (frontend UI + guard transversal) dentro del budget 400 LoC.

## Scope

### In Scope

- `Index.cshtml.cs`: `BuildEditRouteValues(Guid id)` (espejo de `CargoIndexModel.BuildEditRouteValues`).
- `Index.cshtml`: botón `Editar` (`btn-warning` + `ti ti-edit`) entre `Detalle` y `<form data-puesto-delete-form>`; eliminar comment obsoleto.
- `PuestosController.cs`: `[Authorize]` a nivel clase + `[Authorize(Roles = RolesSgv.Administrador)]` en `Create`/`Update`/`Delete`/`Reactivate`.
- Tests: aserciones de botón Editar en `PuestoIndexPageTests` + tests 401/403/2xx en `PuestosControllerTests`.

### Out of Scope / Non-goals

- **No** se reescriben `Edit/Details/_Form` (ya mergeados, cobertura PASS).
- **No** se agrega `Editar` al sidenav (convención cross-módulo: fila).
- **No** se tocan `PuestosApiClient`, dominio, aplicación ni migraciones.
- **No** se introduce `?status=activas|eliminadas` (follow-up ya documentado).
- **No** se modifican `DatosSemilla.cs` ni la migración `20260706221558_*` (cambios no relacionados en working tree).
- **No** se relajan guards de Cargos/Habilidades/Unidades Organizativas.

## Capabilities

### New Capabilities

- `puesto-management`: requisito "Autorización de endpoints de puestos" (par espejo de `cargo-management`:259-280) — GETs autenticados, writes `Administrador`.

### Modified Capabilities

- **None.** El spec canónico `puesto-web-listado-detalle-baja/spec.md:27` ya exige el botón Editar; este change **cumple** ese requisito.

## Approach

Frontend: replicar el patrón Cargos verbatim — helper en PageModel + `<a>` con `Url.Page("/Organizacion/Puestos/Edit", Model.BuildEditRouteValues(item.Id))`. Backend: aplicar el precedent archivado de Cargos — `[Authorize]` a nivel clase + overrides admin por método, reusando `RolesSgv.Administrador` y el principal fake ya extendido en `ApiWebApplicationFactory`. Strict TDD: RED en test Index → RED en tests API 401/403 → GREEN → REFACTOR.

## Affected Areas

| Area | Impacto | Descripción |
|------|---------|-------------|
| `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs` | Modified | Helper `BuildEditRouteValues(Guid id)`. |
| `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml` | Modified | Botón `Editar` por fila; comment obsoleto borrado. |
| `src/SGV.Api/Controllers/PuestosController.cs` | Modified | `[Authorize]` clase + `[Authorize(Roles=Administrador)]` en writes. |
| `tests/SGV.Tests/Web/Puesto/PuestoIndexPageTests.cs` | Modified | Aserciones presencia/ausencia botón Editar. |
| `tests/SGV.Tests/Api/PuestosControllerTests.cs` | Modified | Cobertura 401/403/2xx. |
| `openspec/specs/puesto-management/spec.md` | New | Spec nuevo con requisito de autorización. |

## Risks

| Riesgo | Likelihood | Mitigación |
|--------|------------|------------|
| Working tree trae `DatosSemilla.cs` + migración nueva sin commitear que se mezclen en el PR | Alta | `sdd-apply` debe aislar solo archivos listados vía `git diff -- src/SGV.Web/... src/SGV.Api/Controllers/PuestosController.cs tests/SGV.Tests/...` y abortar si aparecen archivos no listados. |
| Tests API existentes rompen al introducir `[Authorize]` porque `ApiWebApplicationFactory` no cubre Puestos | Media | Reusar principal fake extendido en `2026-07-01-cargos-crear-autorizacion-admin`; si falta uno no-admin, agregarlo sin policies nuevas. |
| `PATCH .../reactivar` olvidado en la lista de writes | Baja | Lista explícita en este proposal; test `Reactivate_Returns403ForNonAdmin` blinda. |

## Rollback Plan

Revertir `[Authorize]` en `PuestosController.cs`, helper/botón en `Index.cshtml(.cs)`, aserciones/tests de auth y el spec `puesto-management`. Si va en chained PR, revertir PR #N; si single PR, `git revert <sha>`.

## Dependencies

- `RolesSgv.Administrador` sembrado; `ApiWebApplicationFactory` con principal fake extendido (archive Cargos).
- Página `Puestos/Edit` (PR #93) y botón Editar en `Details` (PR #94) mergeados.
- Spec canónico `puesto-web-listado-detalle-baja/spec.md:27`.

## Success Criteria

- [ ] `Index` activo de Puestos renderiza botón `Editar` por fila (`btn-warning`, `data-bs-title="Editar"`).
- [ ] Test del Index asserta `/organizacion/puestos/editar/{id}` + tooltip + ausencia en `status=eliminadas`.
- [ ] `PuestosController`: `[Authorize]` en todos los métodos, `[Authorize(Roles=Administrador)]` en POST/PUT/PATCH/DELETE.
- [ ] `GET /api/v1/puestos` sin credenciales → `401`.
- [ ] `POST /api/v1/puestos` autenticado no-admin → `403`; admin → `2xx`.
- [ ] `dotnet test SGV.slnx` pasa sin regresión (suite 406+ tests).
- [ ] `PuestoEditPageTests`/`Details`/`Create`/`PuestosApiClientTests` permanecen verdes sin cambios.
