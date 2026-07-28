# Tasks: Módulo Web de Ocupaciones (Issue #208)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1.120 (4 PRs: 250 / 280 / 390 / 200) |
| 400-line budget risk | Bajo (ningún PR individual supera 400) |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (Contracts+API) → PR 2 (Cliente+Listado) → PR 3a (Formularios) → PR 3b (Navegación) |
| Delivery strategy | ask-on-risk |
| Chain strategy | stacked-to-main |

```
Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: Low
```

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Contracts wire-types + API extendida (segmento, filtros, `ErrorCategoria`) | PR 1 → develop | `dotnet test SGV.slnx --filter "Ocupacion"` | `GET /api/v1/ocupaciones?status=activas&personaId={guid}` | Revertir merge restaura `includeHistory` legacy; `OcupacionCommandResult` pierde `Categoria` |
| 2 | Cliente tipado + Index paginado + sidenav | PR 2 → develop | `dotnet test SGV.slnx --filter "Ocupacion"` | Navegar `/organizacion/ocupaciones`, alternar toggle | Revertir merge elimina `OcupacionApiClient`, `Index.*`, sidenav entry |
| 3a | Create/Edit/Details + _Form + transiciones ciclo de vida | PR 3a → develop | `dotnet test SGV.slnx --filter "Ocupacion"` | Crear/editar ocupación, finalizar, eliminar, reactivar | Revertir merge elimina pages, sin efecto en datos |
| 3b | PersonaOcupaciones + PuestoOcupaciones + enlaces cruzados | PR 3b → develop | `dotnet test SGV.slnx --filter "Ocupacion"` | Navegar desde Persona/Puesto Details | Revertir merge elimina pages y enlaces en Details |

## Convenciones

- **Numeración**: T-001 a T-024, global por slice.
- **TDD estricto**: RED (escribir test fallido) → GREEN (implementar) → REFACTOR (limpiar) por cada unidad.
- **Commit por unidad verificable**: producción + tests + docs juntos en cada commit.
- **Budget por PR**: ninguno supera 400 líneas. Si Slice 3a excede 380 antes del PR, subdividir en 3a-Form y 3a-Details.
- **Sin referencias de SGV.Web a capas internas**: verificar con `dotnet list reference` y `grep -r "SGV.Aplicacion" src/SGV.Web/`.

---

## Slice 1 — Contracts + API extendida (PR 1, ~250 LOC, 7 tasks)

### T-001 — Wire-types compartidos en `SGV.Contracts/Ocupaciones/`
- **Slice**: 1 | **Layer**: Contracts
- **Crear**: `OcupacionDto`, `OcupacionListQuery`, `OcupacionCommandResult`, `OcupacionError`, `OcupacionEstado`, `OcupacionSegmentoListado`, `OcupacionTipoAsignacion`, `OcupacionApiRoutes`, requests (Crear/Actualizar/Finalizar)
- **Criterios**: REQ-OCC-API-001 (DTO serializable, enums estables, contratos leaf)
- **Dependencias**: Ninguna
- **Archivos**: `src/SGV.Contracts/Ocupaciones/{Consultas/Dtos/,Comandos/,Enums/}/*.cs` (~8 archivos nuevos)
- **TDD**: RED → test unit de serialización JSON → GREEN → crear records
- **LOC**: media (~50)
- **Commits**: `feat(contracts): agregar wire-types de Ocupaciones en SGV.Contracts`

### T-002 — Migrar `OcupacionCommandResult` a `ErrorCategoria` con compat legacy
- **Slice**: 1 | **Layer**: Aplicación → Contracts
- **Mover** `OcupacionCommandResult` a `SGV.Contracts/Ocupaciones/Comandos/`. Agregar `Categoria: ErrorCategoria` a `OcupacionError`. Marcar `OcupacionErrorType` legacy como `[Obsolete]`.
- **Criterios**: REQ-OCC-API-004 (ErrorCategoria en Failure, preserva Code/Message/FieldErrors)
- **Dependencias**: T-001
- **Archivos**: mover `SGV.Aplicacion/Ocupaciones/Comandos/OcupacionCommandResult.cs` → `SGV.Contracts/Ocupaciones/Comandos/`. Actualizar `using` en servicio comandos y tests.
- **TDD**: RED → test `Failure_WithCategoria_ReturnsCategoria` → GREEN
- **LOC**: baja (~20)
- **Commits**: incluido en T-001

### T-003 — Extender `IOcupacionServicioConsulta` con `OcupacionListQuery`
- **Slice**: 1 | **Layer**: Aplicación
- Reemplazar `ListAsync(bool includeHistory, int page, int pageSize, ct)` por `QueryAsync(OcupacionListQuery query, ct)`.
- **Criterios**: REQ-OCC-API-002, REQ-OCC-API-006
- **Dependencias**: T-001
- **Archivos**: modificar `SGV.Aplicacion/Ocupaciones/Consultas/IOcupacionServicioConsulta.cs`
- **TDD**: RED → test servicio con fake repository → GREEN
- **LOC**: baja (~15)
- **Commits**: `feat(api): cambiar includeHistory por status segmentado y filtros contextuales`

### T-004 — Extender `OcupacionRepository.QueryAsync` con filtros server-side
- **Slice**: 1 | **Layer**: Infraestructura
- Reemplazar `ListPagedAsync`/`ListHistoryPagedAsync` por `QueryAsync(OcupacionListQuery)`. `WHERE` segmento (Activas: `FechaFin==null && !IsDeleted`; Eliminadas: `FechaFin!=null || IsDeleted`), `WHERE` opcional `PersonaId`/`PuestoId`, `OrderByDescending(FechaInicio)`, `Count` antes de `Skip/Take`.
- **Criterios**: REQ-OCC-API-003 (filtros server-side combinados con AND), REQ-OCC-API-006 (paginación server-side)
- **Dependencias**: T-001
- **Archivos**: `SGV.Aplicacion/Ocupaciones/Consultas/IOcupacionRepository.cs`, `SGV.Infraestructura/Persistencia/Repositorios/OcupacionRepository.cs`
- **TDD**: RED → test unit con fake → GREEN → implementar QueryAsync
- **LOC**: media (~60)
- **Commits**: incluido en T-003

### T-005 — Cambiar `OcupacionesController.Get` a status/personaId/puestoId
- **Slice**: 1 | **Layer**: API
- `GetAll(includeHistory, page, pageSize, ct)` → `Get(status="activas", page=1, pageSize=20, personaId=null, puestoId=null, ct)`. `status` se parsea a `OcupacionSegmentoListado`. Retorna `PagedResult<OcupacionDto>`.
- **Criterios**: REQ-OCC-API-002 (activas por defecto, historial con `status=eliminadas`), REQ-OCC-API-005 (auth 401/403 preservada)
- **Dependencias**: T-003, T-004
- **Archivos**: `SGV.Api/Controllers/OcupacionesController.cs`
- **TDD**: incluido en T-006
- **LOC**: media (~40)
- **Commits**: incluido en T-003

### T-006 — Actualizar tests API existentes
- **Slice**: 1 | **Layer**: Tests
- `GetAll_IncludeHistory_ReturnsAllIncludingFinalized` → `Get_StatusEliminadas_ReturnsAllIncludingFinalized`. Agregar: `Get_Default_ReturnsActive`, `Get_PersonaId_Filters`, `Get_PuestoId_Filters`, `Get_PersonaIdPuestoId_ReturnsIntersection`, `Get_Anonymous_Returns401`, `Post/Finalize/Reactivate/Delete` auth 401/403.
- **Criterios**: escenarios REQ-OCC-API-002 a REQ-OCC-API-006
- **Dependencias**: T-005
- **Archivos**: `tests/SGV.Tests/Api/OcupacionesControllerTests.cs`
- **TDD**: RED (actualizar tests legacy+escribir nuevos) → GREEN (pasan con controller modificado)
- **LOC**: media (~60)
- **Commits**: `test(api): actualizar tests de OcupacionesController para nuevo contrato segmentado`

### T-007 — Tests `[MySqlFact]` de `OcupacionRepository.QueryAsync`
- **Slice**: 1 | **Layer**: Tests
- Tests de integración: segmento Activas/Eliminadas no se mezclan, filtros PersonaId/PuestoId, paginación con `TotalCount` filtrado.
- **Dependencias**: T-004
- **Archivos**: `tests/SGV.Tests/Persistencia/OcupacionRepositoryTests.cs` (nuevo)
- **TDD**: RED → tests `[MySqlFact]` → GREEN (pasan con repo QueryAsync)
- **LOC**: media (~50)
- **Commits**: `test(persistencia): agregar tests MySqlFact de OcupacionRepository.QueryAsync`

---

## Slice 2 — Cliente Web + Listado (PR 2, ~280 LOC, 6 tasks)

### T-008 — Crear `IOcupacionApiClient` + `OcupacionApiClient`
- **Slice**: 2 | **Layer**: Web Integration
- Interface con `ListarAsync(OcupacionListQuery)`, `ObtenerPorIdAsync(Guid)`, `CrearAsync`, `ActualizarAsync`, `FinalizarAsync`, `EliminarAsync`, `ReactivarAsync`. Implementación con `ApiBearerTokenHandler`, `CommandResultMapper`, `ErrorCategoryMapper`, 10s timeout, `BuildQueryUri` con `StringBuilder` + `Uri.EscapeDataString`.
- **Criterios**: REQ-OCC-LST-001 (cliente tipado, DI, cancelación, fallas de transporte)
- **Dependencias**: T-001, T-005
- **Archivos**: `src/SGV.Web/Integration/Ocupaciones/{IOcupacionApiClient,OcupacionApiClient}.cs` (nuevos)
- **TDD**: RED → tests `OcupacionApiClientTests.BuildsUri`, `StatusEliminadas_Serializa`, `MapCategoriaToLegacyType_AllBranches` → GREEN
- **LOC**: media (~80)
- **Commits**: `feat(web): agregar IOcupacionApiClient y OcupacionApiClient con CommandResultMapper`

### T-009 — Crear helpers de ViewModel
- **Slice**: 2 | **Layer**: Web Integration
- `OcupacionListItemViewModel` (Id, PersonaId, PersonaNombre, PuestoId, PuestoNombre, Fechas, Tipo, Observaciones, Estado, EsVigente)
- **Dependencias**: T-001
- **Archivos**: `src/SGV.Web/Integration/Ocupaciones/OcupacionListItemViewModel.cs` (nuevo)
- **LOC**: baja (~20)
- **Commits**: incluido en T-010

### T-010 — Crear `Index.cshtml` + `Index.cshtml.cs`
- **Slice**: 2 | **Layer**: Web Pages
- `[Authorize]`. `OnGetAsync(p, search, sort, status, personaId?, puestoId?, ct)`. Tabla paginada server-side, toggle Activas/Eliminadas, acciones por fila gated por admin+estado. PRG con `PageFeedback` + `TempData`.
- **Criterios**: REQ-OCC-LST-002 (listado paginado), REQ-OCC-LST-003 (toggle), REQ-OCC-LST-004 (feedback uniforme), REQ-OCC-LST-006 (acciones por fila)
- **Dependencias**: T-008, T-009
- **Archivos**: `src/SGV.Web/Pages/Organizacion/Ocupaciones/{Index.cshtml,Index.cshtml.cs}` (nuevos)
- **TDD**: RED → tests render + paginación + toggle → GREEN
- **LOC**: media (~120)
- **Commits**: `feat(web): crear Index de Ocupaciones con paginación server-side y toggle segmentado`

### T-011 — Registrar `IOcupacionApiClient` en DI
- **Slice**: 2 | **Layer**: Web
- `AddHttpClient<IOcupacionApiClient, OcupacionApiClient>()` con `ApiBearerTokenHandler`.
- **Dependencias**: T-008
- **Archivos**: `src/SGV.Web/Program.cs` (modificar)
- **LOC**: baja (~10)
- **Commits**: incluido en T-008

### T-012 — Agregar entrada en `_Sidenav.cshtml`
- **Slice**: 2 | **Layer**: Web Pages
- Nuevo colapsable Ocupaciones (ícono `ti ti-history`) después del bloque `puestos`. Sub-ítems: Listado (todo autenticado), Nuevo (solo admin). Helper `ocupacionesGroupActive/ListadoActive/NuevaActive`.
- **Criterios**: REQ-OCC-LST-005 (sidenav con gates)
- **Dependencias**: T-010
- **Archivos**: `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` (modificar)
- **LOC**: baja (~30)
- **Commits**: `feat(web): agregar entrada Ocupaciones en sidenav con gates de admin`

### T-013 — Tests Web con `FakeOcupacionApiClient`
- **Slice**: 2 | **Layer**: Tests
- Crear `FakeOcupacionApiClient` con `Contadores` por método. Tests: `IndexPageTests` (carga inicial, toggle, paginación, sin resultados, error transporte, 401/403/404), `IOcupacionApiClientContractTests`.
- **Criterios**: escenarios REQ-OCC-LST-002 a LST-006
- **Dependencias**: T-010, T-011, T-012
- **Archivos**: `tests/SGV.Tests/Web/Ocupaciones/{FakeOcupacionApiClient.cs,OcupacionIndexPageTests.cs,IOcupacionApiClientContractTests.cs}` (nuevos). `SgvWebApplicationFactory.ConfigureTestServices` (modificar).
- **TDD**: RED → escribir tests → GREEN
- **LOC**: media (~70)
- **Commits**: `test(web): agregar FakeOcupacionApiClient y tests de IndexPage`

---

## Slice 3a — Formularios CRUD (PR 3a, ~390 LOC, 6 tasks)

### T-014 — Crear `OcupacionInputModel` y `OcupacionDetailsViewModel`
- **Slice**: 3a | **Layer**: Web Integration/Pages
- `OcupacionInputModel` con `[Required]`/`[StringLength]` para validación cliente. `OcupacionDetailsViewModel` con DTO + flags `EsVigente`, `EsAdministrador`.
- **Dependencias**: T-001
- **Archivos**: `src/SGV.Web/Integration/Ocupaciones/OcupacionInputModel.cs`, `src/SGV.Web/Pages/Organizacion/Ocupaciones/OcupacionDetailsViewModel.cs` (nuevos)
- **LOC**: baja (~25)
- **Commits**: incluido en T-016

### T-015 — Crear `_Form.cshtml` partial compartido
- **Slice**: 3a | **Layer**: Web Pages
- Partial con `asp-for` contra `OcupacionInputModel`. Selects de Persona (via `IPersonaApiClient.GetAllAsync`) y Puesto (via `IPuestosApiClient.GetAllAsync`). `asp-validation-for` por campo.
- **Dependencias**: T-014
- **Archivos**: `src/SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml` (nuevo)
- **LOC**: media (~50)
- **Commits**: incluido en T-016

### T-016 — Crear `Create.cshtml` + `Create.cshtml.cs`
- **Slice**: 3a | **Layer**: Web Pages
- `[Authorize(Roles=Administrador)]`. Carga catálogos Persona/Puesto. Pre-carga `PersonaId`/`PuestoId` desde query string. POST → `CrearAsync`. 409 `PersonaYPuestoOcupados`/`PuestoOcupado` → `ModelState` por campo. 400 → `FieldErrors`. PRG al Index.
- **Criterios**: REQ-OCC-FORM-001 (crear), REQ-OCC-FORM-004 (validación), REQ-OCC-FORM-005 (conflictos), REQ-OCC-FORM-006 (PRG)
- **Dependencias**: T-008, T-015, T-014
- **Archivos**: `src/SGV.Web/Pages/Organizacion/Ocupaciones/{Create.cshtml,Create.cshtml.cs}` (nuevos)
- **TDD**: RED → tests CreatePage → GREEN
- **LOC**: media (~80)
- **Commits**: `feat(web): crear formulario de alta de Ocupaciones con validación y conflictos 409`

### T-017 — Crear `Edit.cshtml` + `Edit.cshtml.cs`
- **Slice**: 3a | **Layer**: Web Pages
- Solo vigentes. Gate admin + `EsVigente`. Mismos campos que Create (PersonaId, PuestoId, FechaInicio, TipoAsignacion, Observaciones). 409 si Puesto cambió a ocupado.
- **Criterios**: REQ-OCC-FORM-002 (editar solo vigentes), REQ-OCC-FORM-004/005/006
- **Dependencias**: T-016, T-008
- **Archivos**: `src/SGV.Web/Pages/Organizacion/Ocupaciones/{Edit.cshtml,Edit.cshtml.cs}` (nuevos)
- **TDD**: RED → tests EditPage → GREEN
- **LOC**: media (~50)
- **Commits**: `feat(web): crear edición de Ocupaciones (solo vigentes)`

### T-018 — Crear `Details.cshtml` + `Details.cshtml.cs`
- **Slice**: 3a | **Layer**: Web Pages
- `[Authorize]`. `OnGetAsync(id, ct)`. Acciones `OnPostFinalizarAsync`, `OnPostEliminarAsync`, `OnPostReactivarAsync` (PRG). SweetAlert2 confirmación. Gate admin + estado. FechaFin >= FechaInicio (cliente+servidor).
- **Criterios**: REQ-OCC-FORM-003 (detalle + ciclo vida), REQ-OCC-FORM-007 (FechaFin válida), REQ-OCC-FORM-008 (reactivación con colisión)
- **Dependencias**: T-008, T-014
- **Archivos**: `src/SGV.Web/Pages/Organizacion/Ocupaciones/{Details.cshtml,Details.cshtml.cs}` (nuevos)
- **TDD**: RED → tests DetailsPage → GREEN
- **LOC**: media (~100)
- **Commits**: `feat(web): crear detalle de Ocupaciones con finalizar/eliminar/reactivar`

### T-019 — Tests Web de formularios CRUD
- **Slice**: 3a | **Layer**: Tests
- `OcupacionCreatePageTests`, `OcupacionEditPageTests`, `OcupacionDetailsPageTests`: render, PRG, errores por campo, 409 conflict, 404, gate admin, FechaFin válida.
- **Dependencias**: T-016, T-017, T-018
- **Archivos**: `tests/SGV.Tests/Web/Ocupaciones/{OcupacionCreatePageTests.cs,OcupacionEditPageTests.cs,OcupacionDetailsPageTests.cs}` (nuevos)
- **LOC**: media (~85)
- **Commits**: `test(web): agregar tests de Create/Edit/Details de Ocupaciones`

---

## Slice 3b — Navegación cruzada (PR 3b, ~200 LOC, 5 tasks)

### T-020 — Crear `PersonaOcupaciones.cshtml` + `PersonaOcupaciones.cshtml.cs`
- **Slice**: 3b | **Layer**: Web Pages
- `[Authorize]`, no requiere admin (paridad `PersonaHabilidades`). Filtro fijo `personaId` + `Segmento=Activas`. Sin toggle Eliminadas. Botón "Nueva ocupación" gated admin con `?personaId=`. Botón "Volver" a `/personas/detalles/{id}`.
- **Criterios**: REQ-OCC-NAV-001 (ocupaciones por persona), REQ-OCC-NAV-004 (sin toggle)
- **Dependencias**: T-008
- **Archivos**: `src/SGV.Web/Pages/Personas/PersonaOcupaciones.{cshtml,cshtml.cs}` (nuevos)
- **TDD**: RED → tests PersonaOcupacionesPage → GREEN
- **LOC**: media (~60)
- **Commits**: `feat(web): crear PersonaOcupaciones con filtro contextual y sin toggle`

### T-021 — Crear `PuestoOcupaciones.cshtml` + `PuestoOcupaciones.cshtml.cs`
- **Slice**: 3b | **Layer**: Web Pages
- Espejo de T-020: filtro fijo `puestoId`, `Segmento=Activas`. Sin toggle. Botón "Nueva" gated admin con `?puestoId=`. Botón "Volver" a `/organizacion/puestos/detalles/{id}`.
- **Criterios**: REQ-OCC-NAV-002 (ocupaciones por puesto), REQ-OCC-NAV-004
- **Dependencias**: T-008
- **Archivos**: `src/SGV.Web/Pages/Organizacion/Puestos/PuestoOcupaciones.{cshtml,cshtml.cs}` (nuevos)
- **TDD**: RED → tests PuestoOcupacionesPage → GREEN
- **LOC**: media (~60)
- **Commits**: `feat(web): crear PuestoOcupaciones con filtro contextual y sin toggle`

### T-022 — Agregar enlaces "Ver ocupaciones" en Details de Persona y Puesto
- **Slice**: 3b | **Layer**: Web Pages
- En `Personas/Details.cshtml` y `Puestos/Details.cshtml`: botón "Ver ocupaciones" cuando `IsActive`, con `asp-page` y `asp-route-id`.
- **Criterios**: REQ-OCC-NAV-003 (enlaces desde detalles, solo entidad activa)
- **Dependencias**: T-020, T-021
- **Archivos**: `src/SGV.Web/Pages/Personas/Details.cshtml` (modificar), `src/SGV.Web/Pages/Organizacion/Puestos/Details.cshtml` (modificar)
- **LOC**: baja (~20)
- **Commits**: `feat(web): agregar enlaces contextuales a Ocupaciones desde Persona/Puesto Details`

### T-023 — Preservación de contexto de navegación (`ReturnNavigationContext`)
- **Slice**: 3b | **Layer**: Web Pages
- `ReturnUrl` transporta `?returnPersonaId=` / `?returnPuestoId=`. Botón Volver siempre al Details dueño. `ReturnNavigationContext` reusado para que listado origen recuerde su segmento.
- **Criterios**: REQ-OCC-NAV-005 (volver preserva origen), REQ-OCC-NAV-006 (alta contextual precargada)
- **Dependencias**: T-020, T-021, T-016
- **Archivos**: modificar `PersonaOcupaciones.cshtml.cs`, `PuestoOcupaciones.cshtml.cs`, `Create.cshtml.cs`
- **LOC**: baja (~30)
- **Commits**: incluido en T-020/T-021

### T-024 — Tests Web de navegación cruzada
- **Slice**: 3b | **Layer**: Tests
- `PersonaOcupacionesPageTests`, `PuestoOcupacionesPageTests`: render con datos, sin toggle, persona sin ocupaciones, persona inexistente (404), enlace desde Details, retorno preserva contexto, gate admin en "Nueva".
- **Dependencias**: T-020, T-021, T-022, T-023
- **Archivos**: `tests/SGV.Tests/Web/Ocupaciones/{PersonaOcupacionesPageTests.cs,PuestoOcupacionesPageTests.cs}` (nuevos)
- **LOC**: media (~50)
- **Commits**: `test(web): agregar tests de navegación cruzada de Ocupaciones`

---

## Plan de PRs (stacked-to-main sobre `develop`)

| PR | Branch | Base | Head | Título | LOC est. | Cierra |
|----|--------|------|------|--------|----------|--------|
| 1 | `feat/208-p1-contracts-api` | `develop` | branch | `feat(api): contracts de Ocupaciones + API segmentada y filtros` | ~250 | Issue #208 (parcial) |
| 2 | `feat/208-p2-cliente-listado` | `develop` | branch | `feat(web): cliente API tipado + listado paginado de Ocupaciones` | ~280 | Issue #208 (parcial) |
| 3a | `feat/208-p3a-formularios` | `develop` | branch | `feat(web): formularios CRUD de Ocupaciones` | ~390 | Issue #208 (parcial) |
| 3b | `feat/208-p3b-navegacion` | `develop` | branch | `feat(web): navegación cruzada Persona/Puesto-Ocupaciones` | ~200 | Issue #208 (completo) |

**Verificaciones por PR**: `dotnet build SGV.slnx` + `dotnet test SGV.slnx --filter "Ocupacion"` + sin warnings nuevos. `bun run build` no aplica (no se modifican assets frontend).

---

## Plan de commits

| Commit | Tasks | Mensaje (conventional) | LOC |
|--------|-------|------------------------|-----|
| **PR1-C1** | T-001, T-002 | `feat(contracts): agregar wire-types de Ocupaciones en SGV.Contracts` | ~70 |
| **PR1-C2** | T-003, T-004, T-005 | `feat(api): cambiar includeHistory por status segmentado y filtros contextuales` | ~115 |
| **PR1-C3** | T-006 | `test(api): actualizar tests de OcupacionesController para nuevo contrato` | ~60 |
| **PR1-C4** | T-007 | `test(persistencia): agregar tests MySqlFact de OcupacionRepository.QueryAsync` | ~50 |
| **PR2-C1** | T-008, T-011 | `feat(web): agregar IOcupacionApiClient y OcupacionApiClient` | ~90 |
| **PR2-C2** | T-009, T-010 | `feat(web): crear Index de Ocupaciones con paginación server-side` | ~140 |
| **PR2-C3** | T-012 | `feat(web): agregar entrada Ocupaciones en sidenav` | ~30 |
| **PR2-C4** | T-013 | `test(web): agregar FakeOcupacionApiClient y tests de IndexPage` | ~70 |
| **PR3a-C1** | T-014, T-015, T-016 | `feat(web): crear formulario de alta de Ocupaciones` | ~155 |
| **PR3a-C2** | T-017 | `feat(web): crear edición de Ocupaciones (solo vigentes)` | ~50 |
| **PR3a-C3** | T-018 | `feat(web): crear detalle de Ocupaciones con acciones de ciclo de vida` | ~100 |
| **PR3a-C4** | T-019 | `test(web): agregar tests de Create/Edit/Details de Ocupaciones` | ~85 |
| **PR3b-C1** | T-020, T-023 | `feat(web): crear PersonaOcupaciones con filtro contextual` | ~80 |
| **PR3b-C2** | T-021, T-023 | `feat(web): crear PuestoOcupaciones con filtro contextual` | ~80 |
| **PR3b-C3** | T-022 | `feat(web): agregar enlaces contextuales a Ocupaciones desde Details` | ~20 |
| **PR3b-C4** | T-024 | `test(web): agregar tests de navegación cruzada de Ocupaciones` | ~50 |

---

## Riesgos y contingencias

| # | Riesgo | Contingencia |
|---|--------|-------------|
| 1 | Slice 3a > 380 LOC antes de abrir PR | Subdividir: PR 3a-Form (Create+Edit, ~200 LOC) y PR 3a-Details (~190 LOC). NO reducir alcance. |
| 2 | `[MySqlFact]` se skipean sin MySQL local | Comportamiento esperado (146+ tests skipped es normal). sdd-apply verificar preflight. |
| 3 | `dotnet build` falla tras Slice 1 | NO avanzar a Slice 2 hasta resolver. El build debe pasar en cada PR. |
| 4 | Warning CS8524 en `MapCategoriaToLegacyType` | Verificar que el mapper en `CommandResultMapper.cs` cubre las 7 variantes de `ErrorCategoria`. |
| 5 | `OcupacionDto.Estado` serialización enum vs string | Default `JsonStringEnumConverter` en System.Text.Json. Verificar test de serialización en T-001. |

---

## Verificación por slice

| Slice | Build | Test Frontend | Comandos |
|-------|-------|---------------|----------|
| 1 | `dotnet build SGV.slnx` | `dotnet test SGV.slnx --filter "Ocupacion"` | Sin warnings nuevos |
| 2 | `dotnet build SGV.slnx` | `dotnet test SGV.slnx --filter "Ocupacion"` | Sin warnings; grep cero a `SGV.Aplicacion` en `src/SGV.Web/Integration/Ocupaciones/` |
| 3a | `dotnet build SGV.slnx` | `dotnet test SGV.slnx --filter "Ocupacion"` | Sin warnings nuevos |
| 3b | `dotnet build SGV.slnx` | `dotnet test SGV.slnx --filter "Ocupacion"` | Sin warnings nuevos |

`bun run build` no aplica a ningún slice (solo Razor Pages, sin assets frontend nuevos).

---

## Referencias

- `openspec/changes/2026-07-28-web-ocupaciones-issue-208/proposal.md`
- `openspec/changes/2026-07-28-web-ocupaciones-issue-208/design.md` (11 DEC, 249 líneas)
- `openspec/changes/2026-07-28-web-ocupaciones-issue-208/specs/web-ocupaciones-contrato-api/spec.md` (REQ-OCC-API-001..006)
- `openspec/changes/2026-07-28-web-ocupaciones-issue-208/specs/web-ocupaciones-listado/spec.md` (REQ-OCC-LST-001..006)
- `openspec/changes/2026-07-28-web-ocupaciones-issue-208/specs/web-ocupaciones-crear-editar/spec.md` (REQ-OCC-FORM-001..008)
- `openspec/changes/2026-07-28-web-ocupaciones-issue-208/specs/web-ocupaciones-navegacion-contextual/spec.md` (REQ-OCC-NAV-001..006)
- `openspec/changes/archive/2026-07-27-completar-puestos-issue-209/tasks.md` (espejo)
- Engram: `issue-208-explore-state` (#1462), `architecture/sdd-issue-208-proposal` (#1463), `sdd/2026-07-28-web-ocupaciones-issue-208/spec` (#1464), `sdd/2026-07-28-web-ocupaciones-issue-208/design` (#1465)
- Issue #208: https://github.com/elflacoseba/SGV/issues/208
- `docs/decisiones-implementacion.md` (mapa de bloques GUID + deuda #125 migrada en este change)
