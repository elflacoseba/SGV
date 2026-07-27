# Tasks: Completar módulo de Puestos — endpoint segmentado, paginación server-side y protección de baja

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~530 (PR1 ~250, PR2 ~280) |
| 400-line budget risk | Low (ningún PR individual >400) |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (backend) → PR 2 (web) |
| Delivery strategy | stacked-to-main |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: Low

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Backend: Contracts + Repo.QueryAsync + Servicio + Controller.GetConsulta + protección baja | PR 1 (→ main) | `dotnet test SGV.slnx --filter "Puesto"` | Arrancar API, `GET /api/v1/puestos/consulta?status=activas` | Revertir merge restaura `GET /api/v1/puestos` legacy; `DesactivarAsync` vuelve sin guarda |
| 2 | Web: ApiClient.QueryAsync + PageModel refactor + Index toggle + paginación | PR 2 (→ main) | `dotnet test SGV.slnx --filter "Puesto"` | Navegar `/organizacion/puestos`, alternar toggle, paginar | Revertir merge restaura `GetAllAsync` + filtro en memoria y toggle deshabilitado |

## DAG de dependencias

```
T-01 (Contracts Dtos)
  ├─ T-02 (Type alias ViewModel)
  ├─ T-03 (Comandos protección)
  │   └─ T-04 (tests comandos)
  ├─ T-05 (Repo QueryAsync + impl)
  │   ├─ T-06 (tests repo [MySqlFact])
  │   └─ T-07 (ServicioConsulta QueryAsync)
  │       └─ T-08 (tests servicio consulta)
  └─ T-09 (Controller GetConsulta + 409)
      └─ T-10 (tests API)

PR1 ──→ PR2
         ├─ T-11 (ApiClient QueryAsync)
         │   └─ T-12 (tests api client)
         ├─ T-13 (PageModel LoadAsync refactor)
         │   └─ T-14 (tests PageModel)
         └─ T-15 (Index.cshtml toggle + paginación)
```

## Estado de implementación

- [x] T-01: Contrato `PuestoListQuery` y `PuestoSegmentoListado` en Contracts
- [x] T-02: Alias/migración inicial de `PuestoListQuery` en Web
- [x] T-03: Guarda de baja contra ocupaciones vigentes
- [x] T-04: Tests unitarios de la guarda de baja
- [x] T-05: `IPuestoRepository.QueryAsync` e implementación server-side
- [x] T-06: Tests MySQL de consulta segmentada y paginada
- [x] T-07: `IPuestoServicioConsulta.QueryAsync`
- [x] T-08: Tests unitarios del servicio de consulta
- [x] T-09: Endpoint HTTP `/consulta` y mapeo 409
- [x] T-10: Tests API del endpoint y baja protegida
- [x] T-11: `IPuestosApiClient.QueryAsync` y serialización de query
- [x] T-12: Tests del cliente HTTP de puestos
- [x] T-13: Refactor de `PuestoIndexModel.LoadAsync` a consulta paginada
- [x] T-14: Tests del PageModel y feedback 409
- [x] T-15: Toggle Eliminadas y controles de paginación en la vista

## PR 1 — Backend (≈250 líneas, 4 commits)

### T-01: Crear `PuestoListQuery` y `PuestoSegmentoListado` en Contracts
- **Capa**: Contracts
- **Archivos**: `src/SGV.Contracts/Organizacion/Consultas/Dtos/PuestoListQuery.cs` (nuevo)
- **Acción**: Espejo de `CargoListQuery`/`CargoSegmentoListado`: enum `PuestoSegmentoListado { Activas=0, Eliminadas=1 }` + record `PuestoListQuery(int Page, int PageSize, string? Search, string? Sort, PuestoSegmentoListado Segmento = Activas)`.
- **Done**: Compila, tests unit afirman defaults.
- **Estimación**: ~25 líneas añadidas
- **Commits**: Commit 1

### T-02: Type alias `PuestoListQuery` en `PuestoListItemViewModel.cs` (DEC-1)
- **Capa**: Web (Integration)
- **Archivos**: `src/SGV.Web/Integration/Organizacion/PuestoListItemViewModel.cs` (modificar)
- **Acción**: Eliminar record `PuestoListQuery` legacy (sin Page ni PageSize), agregar `using PuestoListQuery = SGV.Contracts.Organizacion.Consultas.Dtos.PuestoListQuery;`.
- **Done**: No hay referencias rotas en Web (consumidores usan `PuestoListQuery.Empty` que se reemplaza por constructor del nuevo record).
- **Estimación**: ~5 líneas añadidas, ~15 eliminadas
- **Dependencias**: T-01
- **Tests**: Ninguno (solo compilación)
- **Commits**: Commit 1

### T-03: Proteger `DesactivarAsync` con guarda de ocupaciones vigentes (DEC-2, DEC-3)
- **Capa**: Aplicación
- **Archivos**: `src/SGV.Aplicacion/Organizacion/Comandos/PuestoServicioComandos.cs` (modificar)
- **Acción**: Agregar `IOcupacionRepository` al ctor primario (6→7 params). Ctor legacy 4-params se conserva con `NullOcupacionRepository` que retorna `false`. En `DesactivarAsync`, tras `GetByIdForUpdateAsync` y antes de mutar: invocar `_ocupacionRepository.ExistsActiveByPuestoAsync(id, ct)`. Si `true` → `PuestoCommandResult.Failure(new PuestoError(PuestoErrorType.Conflict, "PuestoConOcupacionesActivas", …, ErrorCategoria.Conflict))`.
- **Done**: `PuestoError.Categoria = Conflict` explícito; fixtures legacy no rotos.
- **Estimación**: ~40 líneas añadidas
- **Dependencias**: T-01
- **Tests**: T-04
- **Commits**: Commit 2

### T-04: Tests unit `PuestoServicioComandos` baja bloqueada y baja permitida
- **Capa**: Tests
- **Archivos**: `tests/SGV.Tests/Aplicacion/Organizacion/PuestoServicioComandosTests.cs` (modificar)
- **Acción**: Agregar `FakeOcupacionRepository` (o usar `FakeOcupacionWriteRepository` existente) + tests: `DesactivarAsync_ConOcupacionesVigentes_RetornaConflictSinGuardar`, `DesactivarAsync_SinOcupaciones_Procede`, `DesactivarAsync_PuestoInexistente_RetornaNoEncontrado`.
- **Done**: 3 escenarios cubiertos.
- **Estimación**: ~60 líneas añadidas
- **Dependencias**: T-03
- **Commits**: Commit 4 (con T-09, T-10)

### T-05: Agregar `QueryAsync` a `IPuestoRepository` + implementación en `PuestoRepository` (DEC-4, DEC-5)
- **Capa**: Aplicación + Infraestructura
- **Archivos**: `src/SGV.Aplicacion/Organizacion/Consultas/IPuestoRepository.cs` (modificar), `src/SGV.Infraestructura/Persistencia/Repositorios/PuestoRepository.cs` (modificar)
- **Acción**: En `IPuestoRepository` agregar `Task<(IReadOnlyList<Puesto> Items, int TotalCount)> QueryAsync(string? search, int page, int pageSize, string? sort, PuestoSegmentoListado segmento, CancellationToken)`. En `PuestoRepository` implementar espejo de `CargoRepository.QueryAsync`: `AsNoTracking()` propio con `Where(segmento==Activas ? IsActive && !IsDeleted : !IsActive && IsDeleted)`, Includes UnidadOrganizativa + Cargo, search LIKE Codigo/Nombre/Descripcion, `ApplySort` (codigo_asc/desc, nombre_asc/desc default codigo_asc), CountAsync antes de Skip/Take.
- **Done**: Compila; endpoint `/consulta` retorna datos correctos.
- **Estimación**: ~70 líneas añadidas
- **Dependencias**: T-01
- **Tests**: T-06
- **Commits**: Commit 3

### T-06: Tests `[MySqlFact]` de `PuestoRepository.QueryAsync`
- **Capa**: Tests
- **Archivos**: `tests/SGV.Tests/Persistencia/PuestoRepositoryTests.cs` (modificar)
- **Acción**: Agregar tests `[MySqlFact]`: segmento Activas/Eliminadas no se mezclan, sort aplicado antes de paginar, paginación correcta, search LIKE filtra. Espejo de `CargoRepositoryTests.QueryAsync_*`.
- **Done**: Tests pasan con MySQL; se skipean limpios sin MySQL.
- **Estimación**: ~80 líneas añadidas
- **Dependencias**: T-05
- **Commits**: Commit 4 (con T-04, T-08, T-10)

### T-07: Agregar `QueryAsync` a `IPuestoServicioConsulta` + thin pass-through (paridad CargoServicioConsulta)
- **Capa**: Aplicación
- **Archivos**: `src/SGV.Aplicacion/Organizacion/Consultas/IPuestoServicioConsulta.cs` (modificar), `src/SGV.Aplicacion/Organizacion/Consultas/PuestoServicioConsulta.cs` (modificar)
- **Acción**: En interface agregar `Task<PagedResult<PuestoDto>> QueryAsync(PuestoListQuery, CancellationToken)`. En `PuestoServicioConsulta` implementar como thin pass-through que llama a `repository.QueryAsync` y construye `PagedResult<PuestoDto>`.
- **Done**: Compila; servicio consulta expone QueryAsync.
- **Estimación**: ~15 líneas añadidas
- **Dependencias**: T-05
- **Tests**: T-08
- **Commits**: Commit 3

### T-08: Tests unit `PuestoServicioConsulta.QueryAsync` con fake del repo
- **Capa**: Tests
- **Archivos**: `tests/SGV.Tests/Aplicacion/Organizacion/PuestoServicioConsultaTests.cs` (modificar)
- **Acción**: Agregar test unit con fake `IPuestoRepository` que verifica que `QueryAsync` delega al repo y construye `PagedResult`.
- **Done**: 1 test parametrizado cubre el pass-through.
- **Estimación**: ~15 líneas añadidas
- **Dependencias**: T-07
- **Commits**: Commit 4

### T-09: `PuestosController.GetConsulta` + mapeo 409 en `Delete`
- **Capa**: API
- **Archivos**: `src/SGV.Api/Controllers/PuestosController.cs` (modificar)
- **Acción**: Agregar `GetConsulta([FromQuery] page=1, pageSize=20, search, sort, status, ct)` espejo de `CargosController.GetConsulta`. NO normalizar `page<1`/`pageSize<1` (DEC-6). `status` mapea `eliminadas` → Eliminadas, resto → Activas. Retorna `Ok(PagedResult<PuestoDto>)`. En `Delete`, el mapeo 409 se produce automáticamente porque `ApiResults.ToProblemResult` respeta `ErrorCategoria.Conflict`.
- **Done**: GET /consulta funciona; DELETE con ocupaciones activas retorna 409.
- **Estimación**: ~40 líneas añadidas
- **Dependencias**: T-07
- **Commits**: Commit 4

### T-10: Tests API `GET /consulta` + DELETE 409/204/404
- **Capa**: Tests
- **Archivos**: `tests/SGV.Tests/Api/PuestosControllerTests.cs` (modificar)
- **Acción**: Agregar tests con `ApiWebApplicationFactory`: `GetConsulta_ConStatusEliminadas`, `GetConsulta_SinStatusDevuelveActivas`, `GetConsulta_AnonimoDevuelve401`, `GetAll_NoModificaShape`, `Delete_ConOcupacionesActivas_Devuelve409`, `Delete_PuestoInexistente_Devuelve404`.
- **Done**: Escenarios API cubiertos.
- **Estimación**: ~70 líneas añadidas
- **Dependencias**: T-09
- **Commits**: Commit 4

### Plan de commits PR 1

| Commit | Tareas | Mensaje convencional | Líneas |
|--------|--------|---------------------|--------|
| 1 | T-01, T-02 | `feat(contracts): add PuestoListQuery + PuestoSegmentoListado + type alias` | ~30 |
| 2 | T-03 | `feat(application): guard puesto delete against active ocupaciones with 409` | ~40 |
| 3 | T-05, T-07 | `feat(puestos): add server-side QueryAsync with pagination and sorting` | ~85 |
| 4 | T-04, T-06, T-08, T-09, T-10 | `feat(api): add /consulta endpoint, 409 mapping, and backend tests` | ~95 |

## PR 2 — Web (≈280 líneas, 3 commits)

### T-11: `IPuestosApiClient.QueryAsync` + impl `PuestosApiClient` (DEC-7)
- **Capa**: Web (Integration)
- **Archivos**: `src/SGV.Web/Integration/Organizacion/IPuestosApiClient.cs` (modificar), `src/SGV.Web/Integration/Organizacion/PuestosApiClient.cs` (modificar)
- **Acción**: En interface agregar `Task<PagedResult<PuestoDto>> QueryAsync(PuestoListQuery, CancellationToken)`. En `PuestosApiClient`, implementar con `BuildQueryUri(StringBuilder)` espejo de `CargoApiClient.BuildQueryUri`: `page=N&pageSize=M` obligatorios, `search/sort/status` opcionales con `Uri.EscapeDataString`. `status="eliminadas"` cuando `Segmento == Eliminadas`.
- **Done**: ApiClient expone QueryAsync.
- **Estimación**: ~50 líneas añadidas
- **Dependencias**: T-01, T-09
- **Tests**: T-12
- **Commits**: Commit 1

### T-12: Tests `PuestosApiClient.QueryAsync` con `SgvWebApplicationFactory`
- **Capa**: Tests
- **Archivos**: `tests/SGV.Tests/Web/Puesto/PuestosApiClientTests.cs` (modificar)
- **Acción**: Agregar tests: `QueryAsync_BuildsUriWithSortStatusSearch`, `QueryAsync_DefaultPageSize20`, `StatusEliminadas_SerializaEliminadas`. Actualizar `FakePuestosApiClient` para exponer QueryAsync.
- **Done**: 3 escenarios cubiertos.
- **Estimación**: ~50 líneas añadidas
- **Dependencias**: T-11
- **Commits**: Commit 1

### T-13: Refactor `PuestoIndexModel.LoadAsync` a `QueryAsync` server-side
- **Capa**: Web (Pages)
- **Archivos**: `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs` (modificar)
- **Acción**: `LoadAsync` delega a `puestosApiClient.QueryAsync(PuestoListQuery)` en lugar de `GetAllAsync()`. Eliminar filtro/orden en memoria. Setear `Items`, `TotalCount` desde `PagedResult`. Preservar `BuildToggleSegmentoRouteValues`, `BuildSortRouteValues`, `BuildDetailsRouteValues`, `BuildEditRouteValues`, `BuildPagedRouteValues`. Mantener PRG con `p/search/sort/status`. Mantener feedback 409 en `OnPostDeleteAsync`.
- **Done**: LoadAsync usa query server-side; PRG preserva contexto.
- **Estimación**: ~80 líneas modificadas
- **Dependencias**: T-11
- **Tests**: T-14
- **Commits**: Commit 2

### T-14: Tests `PuestoIndexModel.LoadAsync` + `OnPostDeleteAsync` con 409
- **Capa**: Tests
- **Archivos**: `tests/SGV.Tests/Web/Puesto/PuestoIndexPageTests.cs` (modificar)
- **Acción**: Agregar tests con `SgvWebApplicationFactory`: `OnGet_WithEliminadasStatus_RendersReactivarButtons`, `OnGet_WithSearch_QueriesApiClientWithSearch`, `OnGet_ToggleEliminadas_RendersAsLink`. Actualizar `FakePuestosApiClient` stub.
- **Done**: 3 escenarios cubiertos.
- **Estimación**: ~60 líneas añadidas
- **Dependencias**: T-13
- **Commits**: Commit 2

### T-15: Activar toggle Eliminadas + paginación en `Index.cshtml`
- **Capa**: Web (Pages)
- **Archivos**: `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml` (modificar)
- **Acción**: Reemplazar `<span class="btn ... disabled">Eliminadas</span>` por `<a href="@Url.Page(…, Model.BuildToggleSegmentoRouteValues("eliminadas"))">`. Agregar controles de paginación (espejo Cargos/Index.cshtml). Mantener banner de reactivación rápida. Mantener feedback 409.
- **Done**: Toggle activo, paginación renderizada, Crear oculto en Eliminadas.
- **Estimación**: ~50 líneas modificadas
- **Dependencias**: T-13
- **Tests**: T-14 (cubre comportamiento)
- **Commits**: Commit 3

### Plan de commits PR 2

| Commit | Tareas | Mensaje convencional | Líneas |
|--------|--------|---------------------|--------|
| 1 | T-11, T-12 | `feat(web): add PuestosApiClient.QueryAsync with pagination and tests` | ~100 |
| 2 | T-13, T-14 | `feat(web): wire puestos index to server-side query and add PageModel tests` | ~140 |
| 3 | T-15 | `feat(web): enable deleted toggle and pagination controls in Index.cshtml` | ~50 |

## Asunciones

1. **Sin migraciones**: Los filtros operan sobre `IsActive`/`IsDeleted`, no sobre columnas generadas. Los índices existentes cubren la query.
2. **Tests `[MySqlFact]`**: Se skipean limpios si no hay MySQL (146 tests skipped es comportamiento esperado). No hay dependencia nueva de infraestructura.
3. **Fixtures legacy**: `PuestoServicioComandos` conserva ctor legacy 4-parámetros que delega al primario con `NullOcupacionRepository`. `FakePuestoWriteRepository`/`PuestoWebTestFixture` no construyen el servicio directamente y no requieren cambios.
4. **`PuestoListQuery` legacy web**: Se reemplaza atómicamente con el type alias; no coexiste. Consumidores web existentes (`FakePuestosApiClient`) se actualizan en T-12/T-14.
5. **Sin normalización de page/pageSize**: El controller no valida `page<1`/`pageSize<1` (DEC-6, paridad con Cargos).
