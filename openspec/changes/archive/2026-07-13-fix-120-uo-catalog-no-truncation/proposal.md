# Proposal: Eliminar catálogo de UO truncado en Edit de Puestos (#120)

## Intent

`Edit.cshtml.cs:298-300` invoca `QueryAsync(... pageSize=200 ...)` y asigna a `UnidadOrganizativaOptions`. Pero `_Form.cshtml:39-61` envuelve los selects de UO y Cargo en `@if (!Model.IsEdit)` — la carga no alimenta ningún control. Es dead code. Mismo patrón aplica a `CargoOptions`. El cambio elimina ambos y deja solo `PuestoSuperiorOptions`, cuyo dropdown sí se renderiza en Create y Edit.

## Scope

### In Scope

- `Edit.cshtml.cs`: quitar `unidadesTask`/`cargosTask`, sus ramas post-WhenAll y los parámetros de UO/Cargo del ctor. Mantener las propiedades en `[]` (las exige `IPuestoForm`).
- `PuestoEditPageTests.cs`: test que afirma cero llamadas a UO/Cargo en GET autenticado.
- `decisiones-implementacion.md`: sección "Catálogo vs listado".
- `puesto-web-crear-editar/spec.md`: requirement de no-carga con scenario.

### Out of Scope

- Refactor de PageModels grandes (out of scope en la issue).
- Cambios en el contrato backend o en `IUnidadOrganizativaApiClient`.
- `Create.cshtml.cs` (ya correcto).
- Endpoint `GET /all` (descartado — opción D del explore).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `puesto-web-crear-editar`: añadir requirement con scenario que verifique cero llamadas a UO/Cargo en GET de Edit.

## Approach

Refactor en `LoadCatalogsAsync` (≈ 60 líneas borradas): drop de dos tasks paralelas y sus ramas post-WhenAll; conservar `puestosTask` para `PuestoSuperiorOptions`. Propiedades en `[]` por `IPuestoForm`. `ErrorMessage` y la firma no cambian; los 5 call sites se preservan. El test usa `QueryCalls`/`GetAllCalls` que ya exponen los fakes; el RED/GREEN exige resolver el baseline de auth web (21 fallos en explore).

## Affected Areas

| Area | Impact |
|------|--------|
| `Edit.cshtml.cs` | Modified |
| `PuestoEditPageTests.cs` | Modified |
| `decisiones-implementacion.md` | Modified |
| `puesto-web-crear-editar/spec.md` | Modified |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Edit futuro deba reintroducir la carga. | Low | Documentado; spec lo deja explícito. |
| Test falle por baseline de auth web. | Medium | Aislar localmente; resolver antes de cerrar apply. |
| Drop de parámetros del ctor rompa dependencia oculta. | Low | `EditModel` no expone firma; call sites solo invocan `LoadCatalogsAsync`. |

## Rollback Plan

`git revert`. Sin migraciones ni cambios de contrato — la lógica eliminada es dead code. Si RED/GREEN no se estabiliza, revertir solo el archivo de tests.

## Dependencies

- Branch `fix/120-uo-catalog-no-truncation` (sin commits).
- Previo: `exploration.md`. Sin NuGet ni migración EF.

## Success Criteria

- [ ] `Edit.cshtml.cs` no invoca UO ni Cargo; catálogos en `[]`.
- [ ] `LoadCatalogsAsync` solo carga `PuestoSuperiorOptions`.
- [ ] `QueryCalls.Count == 0` y `GetAllCalls.Count == 0` en GET.
- [ ] Doc documenta catálogo vs listado.
- [ ] Spec añade requirement de no-carga.
- [ ] `dotnet test` pasa global o aislado.
