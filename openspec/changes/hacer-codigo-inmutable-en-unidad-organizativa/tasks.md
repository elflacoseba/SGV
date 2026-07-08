# Tasks: Hacer inmutable el `Codigo` de `UnidadOrganizativa`

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 430-520 |
| 400-line budget risk | Medium |
| Chained PRs recommended | Yes |
| Suggested split | PR1 Dominio+App → PR2 Persistencia+Docs → PR3 Web |
| Delivery strategy | ask-always (resolved: stacked-to-main) |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: Medium

### Suggested Work Units

| Unit | Goal | PR | Base |
|------|------|----|------|
| 1 | Record + `Actualizar` sin `Codigo`; request/validator/servicio; tests Dominio+App (`PreservaCodigoOriginal`). | PR1 | main |
| 2 | `PersistenceToDomainMapper.ToDomain` via ctor+`with`; smoke API; nota en `decisiones-implementacion.md`. | PR2 | PR1 |
| 3 | `IUnidadOrganizativaForm.IsEdit`; `_Form.cshtml` oculta `Codigo`; `Edit.cshtml.cs` omite `Codigo` del PUT; tests Web. | PR3 | PR2 |

## Phase 1 (PR1): RED — Dominio + Aplicacion

- [x] 1.1 `tests/SGV.Tests/Dominio/Organizacion/UnidadOrganizativaTests.cs`: agregar `Codigo_EsInmutableTrasCreacion` y `Actualizar_CodigoNoCambia` (mirror `Puesto`).
- [x] 1.2 `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs`: agregar `ActualizarAsync_PreservaCodigoOriginal` (regression critica) y refactorizar `CambiarDatos_*` a `Actualizar_*`.
- [x] 1.3 `tests/SGV.Tests/Aplicacion/Organizacion/ActualizarUnidadOrganizativaRequestValidatorTests.cs`: eliminar bloque "Codigo" y adaptar `RequestValido` sin `Codigo`.

## Phase 2 (PR1): GREEN — Dominio + Aplicacion

- [x] 2.1 `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs`: migrar a `sealed record class : EntidadAuditable` con `init`; ctor primario `(codigo, nombre, tipoUnidadOrganizativaId, descripcion?, unidadPadreId?)`; añadir `Actualizar(...)`, `DefinirVigencia`, `CambiarUnidadPadre`, `Activar`, `Desactivar` que devuelven `with`; eliminar `CambiarDatos`.
- [x] 2.2 `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaRequests.cs`: quitar `Codigo` de `ActualizarUnidadOrganizativaRequest`.
- [x] 2.3 `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/ActualizarUnidadOrganizativaRequestValidator.cs`: eliminar `RuleFor(x => x.Codigo)`.
- [x] 2.4 `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs`: en `ActualizarAsync` capturar `unidad = unidad.Actualizar(...)` y borrar `ExistsActiveCodeAsync(request.Codigo, id, ...)`; en `CrearAsync` pasar `descripcion` al ctor y usar `with` para vigencia.

## Phase 3 (PR1): REFACTOR + verify local

- [x] 3.1 `src/SGV.Dominio/Comun/EntidadAuditable.cs`: documentar via XML la asimetria deliberada (`UnidadOrganizativa` migra a `init`, `EntidadAuditable` mantiene `public set`) para que `AuditoriaSaveChangesInterceptor` siga escribiendo `CreatedAt`/`UpdatedAt`/`IsDeleted`.
- [x] 3.2 VERIFY: `dotnet build SGV.slnx` + `dotnet test SGV.slnx --filter "FullyQualifiedName~UnidadOrganizativa"` verde, excluyendo 12 tests del issue #59.

## Phase 4 (PR2): Persistencia + API + Docs

- [x] 4.1 `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs`: asertar que el mapper no usa `BindingFlags.NonPublic` para `IsActive`/`UnidadPadre`/`TipoUnidadOrganizativa`.
- [x] 4.2 `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs`: smoke `Put_ConCodigoExtraEnJson_PreservaCodigoOriginal`.
- [x] 4.3 `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs`: reescribir `ToDomain(UnidadOrganizativaEntity)` con ctor primario + object initializer para auditable + `with { IsActive, UnidadPadre, TipoUnidadOrganizativa, Descripcion, VigenteDesde, VigenteHasta }`.
- [x] 4.4 `src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs`: confirmar `UpdateAsync` sigue llamando `DomainToPersistenceMapper.UpdateEntity(entity, unidad)`.
- [x] 4.5 `docs/decisiones-implementacion.md`: agregar entrada "Inmutabilidad de `Codigo` en `UnidadOrganizativa`" con: identidad logica inmutable post-create; `codigo` extra en PUT fuera de contrato; reactivacion valida colision por codigo persistido.

## Phase 5 (PR3): Web edit UI

- [ ] 5.1 `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs`: agregar `Get_Edit_OcultaInputCodigo` y `Post_Edit_NoEnviaCodigoEnPayload`.
- [ ] 5.2 `src/SGV.Web/Integration/Organizacion/IUnidadOrganizativaForm.cs`: añadir `bool IsEdit { get; }`.
- [ ] 5.3 `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Create.cshtml.cs`: añadir `bool IsEdit => false;`.
- [ ] 5.4 `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml.cs`: añadir `bool IsEdit => true;` y construir `ActualizarUnidadOrganizativaRequest` sin `Input.Codigo`.
- [ ] 5.5 `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/_Form.cshtml`: envolver el input `asp-for="Input.Codigo"` con `@if (!Model.IsEdit)` (mirror de `Puestos/_Form.cshtml`).

## Phase 6 (PR3): REFACTOR + verify full

- [ ] 6.1 VERIFY: `dotnet build SGV.slnx`, `dotnet test SGV.slnx --no-build`, `bun run build` en `src/SGV.Web`; organigrama + reactivacion verdes; documentar exclusion explicita de los 12 tests del issue #59.