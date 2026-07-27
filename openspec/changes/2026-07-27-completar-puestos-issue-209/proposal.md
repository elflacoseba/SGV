# Proposal: Completar módulo de Puestos — endpoint segmentado, paginación server-side y protección de baja por ocupaciones vigentes

> Issue: #209 — feat(web): completar módulo de Puestos — endpoint segmentado y paginación
> Cambio: `2026-07-27-completar-puestos-issue-209` (stacked-to-main, 2 slices anticipadas)

## Contexto y motivación

El módulo de Puestos tiene CRUD completo (Index/Create/Edit/Details/Delete/Reactivate) pero arrastra tres brechas que rompen la paridad con Cargos, Habilidades y Unidades Organizativas:

1. **Listado no segmentado**: `Index.cshtml.cs` invoca `GetAllAsync`, filtra y ordena en memoria; el toggle "Eliminadas" está renderizado como `<span disabled>` con tooltip "Próximamente". Cualquier volumen real de puestos degrada la experiencia y obliga a `WHERE IsActive = 1` en memoria sobre todo el dataset.
2. **Sin paginación server-side**: la grilla no expone controles de página porque el backend no devuelve `TotalCount` ni `PagedResult`. Forward-compat ya está abierta (parámetros `p/search/sort/status` ya viajan en PRG).
3. **Baja lógica sin guarda**: `PuestoServicioComandos.DesactivarAsync` desactiva el Puesto sin consultar `IOcupacionRepository.ExistsActiveByPuestoAsync`. El mismo riesgo ya está mitigado en Cargos (`ICargoRepository.HasActivePuestosAsync`) y Unidades Organizativas. Cerrar la brecha ahora previene que un Puesto con ocupaciones activas quede marcado `IsActive=false`, rompiendo las reglas equivalentes que Ocupaciones ya respeta.

Cerrar este gap ahora evita que el módulo se desincronice con los otros tres cuando lleguen integraciones de Personas/Ocupaciones (issue #208) que dependen de puestos vigentes.

## Decisiones de diseño

| # | Decisión | Estado |
|---|----------|--------|
| 1 | Espejo del patrón Cargos: `PuestoListQuery` + `PuestoSegmentoListado` en `SGV.Contracts`, `IPuestoRepository.QueryAsync` server-side (AsNoTracking + Skip/Take), `IPuestoServicioConsulta.QueryAsync` thin pass-through, `GET /api/v1/puestos/consulta` con query string `page/pageSize/search/sort/status`. | **Locked** |
| 2 | Toggle "Eliminadas" se activa como `<a>` con `BuildToggleSegmentoRouteValues("eliminadas")`; el path ya existe en el PageModel. El botón "Crear" sigue MUST NOT en vista Eliminadas (simetría con Cargos REQ-CW-06). | **Locked** |
| 3 | `DesactivarAsync` invoca `IOcupacionRepository.ExistsActiveByPuestoAsync` antes de mutar. Si retorna `true` → `PuestoCommandResult.Failure(PuestoErrorType.Conflict, "PuestoConOcupacionesActivas", …)`. `ApiResults.MapCategoria` mapea `Conflict → 409`. El código de error estable es `PuestoConOcupacionesActivas` (string constante, no aleatorio). | **Locked** |
| 4 | `GET /api/v1/puestos` (GetAll existente) **se preserva**; `GET /consulta` es nuevo y coexiste. Sin breaking change para consumidores que ya dependan de GetAll (Create/Edit/Details). | **Locked** |
| 5 | `PuestoListQuery` legacy en `PuestoListItemViewModel.cs` se reemplaza por `using PuestoListQuery = SGV.Contracts.Organizacion.Consultas.Dtos.PuestoListQuery;` siguiendo el patrón que Cargos ya adoptó en `CargoIndexModel`. | **Locked** |
| 6 | Wire query del controller: `status` mapea `activas` (default) / `eliminadas` igual que `CargosController.GetConsulta`. `search` filtra sobre `Codigo/Nombre/Descripcion`. Sort soportado: `codigo_asc/codigo_desc/nombre_asc/nombre_desc` (default `codigo_asc`). | **Locked** |

## Alcance

### In Scope
- Crear `PuestoSegmentoListado` enum + `PuestoListQuery` record en `SGV.Contracts/Organizacion/Consultas/Dtos/`.
- Agregar `QueryAsync` a `IPuestoRepository` + implementación en `PuestoRepository` (server-side, segmento, search, sort, paginación).
- Agregar `QueryAsync` a `IPuestoServicioConsulta` + implementación en `PuestoServicioConsulta` (thin pass-through).
- Agregar `GET /api/v1/puestos/consulta` al `PuestosController`.
- Agregar `QueryAsync(PuestoListQuery)` a `IPuestosApiClient` + `PuestosApiClient`.
- Refactor `Puestos/Index.cshtml.cs` para usar `QueryAsync`, eliminar filtro/orden en memoria y activar toggle Eliminadas.
- Protección de baja en `PuestoServicioComandos.DesactivarAsync` vía `IOcupacionRepository.ExistsActiveByPuestoAsync`.

### Out of Scope
- Cambios en `Create/Edit/Details/Delete/Reactivate` (mantienen `GET /api/v1/puestos/{id}`, contratos actuales).
- Nuevas migraciones (no hay cambio de esquema).
- Cambios en `Ocupaciones` o `Personas` (la guarda es lectura, no escritura).
- Refactor del legacy `PuestoListQuery` web (se reemplaza atómicamente con la nueva; no coexisten).

## Capabilities

### New Capabilities
- `puesto-listado-segmentado`: contrato paginado server-side con segmento activas/eliminadas, búsqueda, sort y paginación.

### Modified Capabilities
- `puesto-management`: agregar guarda de ocupaciones vigentes en `DesactivarAsync` (nuevo error `PuestoConOcupacionesActivas` → 409).

## Plan por capas

| Capa | Archivos | Cambio |
|------|----------|--------|
| Contracts | `src/SGV.Contracts/Organizacion/Consultas/Dtos/PuestoListQuery.cs` (nuevo) | `PuestoSegmentoListado` enum + `PuestoListQuery` record (espejo `CargoListQuery`). |
| Aplicación | `src/SGV.Aplicacion/Organizacion/Consultas/IPuestoRepository.cs` | Agregar `QueryAsync`. |
| Aplicación | `src/SGV.Aplicacion/Organizacion/Consultas/IPuestoServicioConsulta.cs` + `PuestoServicioConsulta.cs` | Agregar `QueryAsync` thin pass-through. |
| Aplicación | `src/SGV.Aplicacion/Organizacion/Comandos/PuestoServicioComandos.cs` | Inyectar `IOcupacionRepository`; guardia en `DesactivarAsync`. |
| Infraestructura | `src/SGV.Infraestructura/Persistencia/Repositorios/PuestoRepository.cs` | Implementar `QueryAsync` con `ApplySort` (espejo `CargoRepository`). |
| API | `src/SGV.Api/Controllers/PuestosController.cs` | Agregar `GetConsulta` (espejo `CargosController.GetConsulta`). |
| Web | `src/SGV.Web/Integration/Organizacion/IPuestosApiClient.cs` + `PuestosApiClient.cs` | Agregar `QueryAsync` con `BuildQueryUri` (espejo `CargoApiClient`). |
| Web | `src/SGV.Web/Integration/Organizacion/PuestoListItemViewModel.cs` | Reemplazar `PuestoListQuery` legacy por `using SGV.Contracts.Organizacion.Consultas.Dtos.PuestoListQuery;`. |
| Web | `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs` + `Index.cshtml` | Refactor `LoadAsync` a `QueryAsync`; toggle Eliminadas activo. |

## Plan de pruebas

- **Contratos** (`tests/SGV.Tests/Aplicacion/Organizacion/PuestoListQueryTests.cs` nuevo): `Default_SegmentoEsActivas`, `PuedeConstruirQueryParaEliminadas` (espejo `CargoListQueryTests`).
- **Repositorio** (`PuestoRepositoryTests`): `QueryAsync_MySql_*` (segmentos no se mezclan, sort se aplica antes de paginar, paginación correcta, search filtra) — `[MySqlFact]`.
- **Servicio** (`PuestoServicioComandosTests`): `DesactivarAsync_ConOcupacionesVigentes_RetornaConflictSinGuardar`, `DesactivarAsync_SinOcupaciones_Procede`, `DesactivarAsync_PuestoInexistente_RetornaNoEncontrado`.
- **API** (`PuestosControllerTests`): `GetConsulta_ConSearchDevuelveSoloMatch`, `GetConsulta_SinStatusDevuelveActivas` (espejo `CargosControllerTests.GetConsulta`).
- **Web client** (`FakePuestosApiClientTests` nuevo + `PuestosApiClientTests`): `QueryAsync_BuildsUriWithSortStatusSearch`, `QueryAsync_DefaultPageSize20`.
- **Web Index** (`PuestoIndexPageTests`): `OnGet_WithEliminadasStatus_RendersReactivarButtons`, `OnGet_WithSearch_QueriesApiClientWithSearch`, `OnGet_ToggleEliminadas_RendersAsLink` (espejo `CargoIndexPageTests`).

## Riesgos y supuestos

| Riesgo | Mitigación |
|--------|-----------|
| `PuestoServicioComandos` necesita un segundo ctor convenience (compatibilidad con `FakePuestoWriteRepository`/`PuestoWebTestFixture` ya existentes). | Mantener el ctor legacy `(IPuestoRepository, IUnidadOrganizativaRepository, ICargoRepository, IUnitOfWork)` que ahora delega al primario con `IOcupacionRepository` fake-throwing. |
| Cambiar `PuestoListQuery` web → contracts puede romper `FakePuestosApiClient`/`PuestoWebTestFixture`. | Migrar atómicamente en Slice 2; tests fixture ya aceptan `PuestoDto` por lo que el cambio es de tipo, no de comportamiento. |
| Tests `[MySqlFact]` se skipean sin MySQL → cobertura condicional. | Documentar en `verify-report.md`; 146 tests skipped es el comportamiento esperado. |
| Ocupaciones (#208) podrían llegar con su propia taxonomía → choque con `PuestoConOcupacionesActivas`. | El código de error es estable por convención del repo (`CodigoDuplicado`, `PuestoNoEncontrado`). Ocupaciones no definen error codes para el módulo Puesto. |
| `GET /api/v1/puestos` existente pierde consumidores. | Preservar el endpoint; coexisten con `/consulta` por diseño del issue. |
| 400-line budget puede quedar corto si se incluyen tests de repositorio EF pesados. | Slice 1 (backend) ≈ 250 líneas, Slice 2 (web) ≈ 280 líneas; ambas bajo presupuesto. |

## Rollback Plan

- Slice 1 (backend): revertir merge elimina `QueryAsync` del repo/servicio/controller; el path legacy `GET /` sigue intacto. `DesactivarAsync` vuelve a desactivar sin guarda (se documenta en commit de rollback).
- Slice 2 (web): revertir merge restaura `GetAllAsync + filtro en memoria`; el toggle "Eliminadas" vuelve a `<span disabled>`. Sin impacto en producción si Slice 1 ya está mergeada (el cliente puede coexistir con backend viejo).

## Entrega esperada (stacked-to-main)

| Slice | PR | Contenido | Líneas aprox. |
|-------|----|-----------|---------------|
| 1 | PR #N | Contracts + Repo.QueryAsync + Servicio.QueryAsync + Controller.GetConsulta + DesactivarAsync protección + tests repo/servicio/api | ~250 |
| 2 | PR #N+1 | Web client.QueryAsync + PuestoListQuery wire + Index.cshtml.cs refactor + Index.cshtml toggle + tests web | ~280 |

**Estrategia**: stacked-to-main con PR #1 → `main` (backend completo + protección) y PR #2 → `main` (web consume el nuevo endpoint). Ambas slices pasan `dotnet build` y `dotnet test SGV.slnx`. Validación manual: navegar a `/organizacion/puestos`, alternar toggle, paginar, intentar eliminar un Puesto con ocupación activa y verificar 409 con `PuestoConOcupacionesActivas`.

## Success Criteria

- [ ] `GET /api/v1/puestos/consulta?status=eliminadas&page=1&pageSize=10` retorna `PagedResult<PuestoDto>` con `TotalCount` correcto y solo eliminados.
- [ ] `GET /api/v1/puestos/consulta?status=activas` (u omitiendo `status`) retorna solo activos.
- [ ] `GET /api/v1/puestos` (GetAll) sigue retornando `IReadOnlyList<PuestoDto>` sin cambios de shape.
- [ ] `DesactivarAsync` con ocupación vigente retorna 409 + código `PuestoConOcupacionesActivas`; sin ocupación sigue retornando 204.
- [ ] `Puestos/Index` activa toggle Eliminadas; sin status ni search, llama a `/consulta` y renderiza paginación con `TotalPages`.
- [ ] Búsqueda `?search=foo` filtra server-side; orden `?sort=nombre_desc` se aplica antes de Skip/Take (mismas filas visibles en cada página).
- [ ] PRG de delete/reactivate preserva `p/search/sort/status` sin cambios.
- [ ] Cobertura de tests: `CargoListQueryTests`, `CargoRepositoryTests.QueryAsync_*`, `PuestoListQueryTests`, `PuestoRepositoryTests.QueryAsync_*`, `PuestoServicioComandosTests.DesactivarAsync_*`, `PuestosControllerTests.GetConsulta_*`, `PuestoIndexPageTests.QueryAsync_*`, `FakePuestosApiClientTests.QueryAsync_*`.
- [ ] `dotnet build SGV.slnx` y `dotnet test SGV.slnx` pasan (incluyendo `[MySqlFact]` cuando MySQL está disponible).