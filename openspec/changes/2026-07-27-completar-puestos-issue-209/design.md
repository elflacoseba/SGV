# Design: Completar módulo de Puestos — endpoint segmentado, paginación server-side y protección de baja por ocupaciones vigentes

> Change: `2026-07-27-completar-puestos-issue-209` · Issue: #209 · Modo: hybrid · Delivery: stacked-to-main

## Contexto

Espejo de `cargo-management` (`CargoListQuery`, `CargoRepository.QueryAsync`, `CargosController.GetConsulta`, `CargoApiClient.QueryAsync`) y de `CargoServicioComandos.DesactivarAsync` + `ICargoRepository.HasActivePuestosAsync`. Cierra tres brechas: listado en memoria, sin paginación server-side, baja sin guarda contra ocupaciones vigentes. Decisiones **Locked** de la propuesta se mantienen tal cual.

## Cambios por capa

**Contracts** — `src/SGV.Contracts/Organizacion/Consultas/Dtos/PuestoListQuery.cs`:

```csharp
public enum PuestoSegmentoListado { Activas = 0, Eliminadas = 1 }

public sealed record PuestoListQuery(
    int Page, int PageSize, string? Search, string? Sort,
    PuestoSegmentoListado Segmento = PuestoSegmentoListado.Activas);
```

**Aplicación:** `IPuestoRepository.QueryAsync(PuestoListQuery, ct) → Task<(IReadOnlyList<Puesto>, int)>`. `IPuestoServicioConsulta.QueryAsync(PuestoListQuery, ct) → Task<PagedResult<PuestoDto>>` (thin pass-through). `PuestoServicioComandos` agrega `IOcupacionRepository` al ctor primario (7-parámetros); ctor legacy 4-parámetros se conserva delegando al primario con un `IOcupacionRepository` null-object que devuelve `false`. `FakePuestoWriteRepository`/`PuestoWebTestFixture` reciben el servicio por DI y no se tocan. `DesactivarAsync` añade guarda pre-mutación: tras `GetByIdForUpdateAsync`, invoca `IOcupacionRepository.ExistsActiveByPuestoAsync(id, ct)`; si `true` → `PuestoCommandResult.Failure(new PuestoError(PuestoErrorType.Conflict, "PuestoConOcupacionesActivas", "El puesto tiene ocupaciones vigentes y no puede darse de baja.", null, ErrorCategoria.Conflict))`. **Crítico:** `PuestoError` es `(Type, Code, Message, int? StatusCode=null, ErrorCategoria Categoria=Unexpected)`; pasar `Categoria = ErrorCategoria.Conflict` explícito o `ApiResults.MapCategoria` mapea a 500.

**Infraestructura** — `PuestoRepository.QueryAsync` espejo de `CargoRepository.QueryAsync`:

```csharp
IQueryable<PuestoEntity> query = Context.Set<PuestoEntity>().AsNoTracking()
    .Where(p => segmento == PuestoSegmentoListado.Activas
        ? (p.IsActive && !p.IsDeleted) : (!p.IsActive && p.IsDeleted))
    .Include(p => p.UnidadOrganizativa).Include(p => p.Cargo);
// search LIKE Codigo/Nombre/(Descripcion nullable → guard); totalCount antes de Skip/Take
// ApplySort: codigo_asc/desc, nombre_asc/desc (default codigo_asc)
```

`QueryAsync` no reutiliza `Query` base (filtra `IsActive`); construye su propio `AsNoTracking()` con Includes.

**API** — `PuestosController.GetConsulta([FromQuery] int page = 1, int pageSize = 20, string? search = null, string? sort = null, string? status = null, ct)` espejo de `CargosController`: construye `PuestoListQuery`, llama a `_servicio.QueryAsync`, retorna `Ok(PagedResult<PuestoDto>)`. No normaliza `page<1`/`pageSize<1` (paridad con Cargos).

**Web:** `IPuestosApiClient.QueryAsync(PuestoListQuery, ct) → Task<PagedResult<PuestoDto>>`. `PuestosApiClient.BuildQueryUri(page, pageSize, search, sort, status)` con `StringBuilder` + `Uri.EscapeDataString`; `page=…&pageSize=…` obligatorios, resto opcional; `status="eliminadas"` cuando `Segmento == Eliminadas`. `PuestoListItemViewModel.cs`: type alias `using PuestoListQuery = SGV.Contracts.Organizacion.Consultas.Dtos.PuestoListQuery;` preserva el nombre para consumidores existentes. `Puestos/Index.cshtml.cs.LoadAsync` delega a `puestosApiClient.QueryAsync` (elimina filtro/orden en memoria) y conserva PRG con `p/search/sort/status`. `OnPostDeleteAsync` ya cubre feedback `Conflict` vía `result.Categoria` + `result.Message`. `Index.cshtml`: `<span disabled>` → `<a href="@Url.Page(…, Model.BuildToggleSegmentoRouteValues("eliminadas"))">`; Crear sigue bajo `!IsDeletedView && EsAdministrador`.

## Decisiones técnicas

| # | Decisión | Locked |
|---|----------|--------|
| DEC-1 | Type alias preserva `PuestoListQuery` legacy sin imports rotos | ✓ |
| DEC-2 | Ctor primario 7-parámetros + legacy 4 con null-IOcupacionRepository preserva fixtures | ✓ |
| DEC-3 | `PuestoError.Categoria = ErrorCategoria.Conflict` explícito (default sería Unexpected → 500) | ✓ |
| DEC-4 | `QueryAsync` propio AsNoTracking + Includes; no reusa `Query` base que filtra IsActive | ✓ |
| DEC-5 | Repo devuelve `(Items, Total)`; servicio construye `PagedResult<PuestoDto>` (paridad Cargos) | ✓ |
| DEC-6 | Controller no normaliza `page<1`/`pageSize<1` (paridad Cargos) | ✓ |
| DEC-7 | `BuildQueryUri` con `StringBuilder` (espejo `CargoApiClient`) | ✓ |

## Manejo de errores

| Origen | HTTP | UX Web |
|--------|------|--------|
| `DesactivarAsync` con ocupación vigente (`PuestoConOcupacionesActivas`) | 409 | `SetDanger` con mensaje específico |
| Puesto inexistente (`PuestoNoEncontrado`) | 404 | `NotFoundDeleteMessage` |
| Sin rol Administrador | 403 | `Forbid()` |
| Sin token | 401 | `IAuthSessionRedirector` |
| Transporte (`HttpRequestException`/`TaskCanceledException`) | 5xx | `TransportFailureClassifier` → `SetLoadErrorState()` |

## Compatibilidad

**Sin migraciones.** Filtro opera sobre `IsActive/IsDeleted`, no sobre la columna generada `UX_Puestos_Activos_UnidadOrganizativaId_Codigo` (archivada por `2026-07-11-fix-active-puesto-id-unique-type`). Índices existentes cubren Includes + WHERE; `OrderBy(Codigo)` evita filesort sobre activos.

## Tests por capa

- **Repositorio:** `[MySqlFact]` en `PuestoRepositoryTests.cs` — segmento Activas/Eliminadas, no-mezcla, paginación, sort, search LIKE.
- **Servicio consulta:** unit con fake `IPuestoRepository` — delega al repo y construye `PagedResult`.
- **Servicio comandos:** unit con `FakePuestoWriteRepository` + `FakeOcupacionRepository` — `ConOcupacionesVigentes_RetornaConflictSinGuardar`, `SinOcupaciones_Procede`, `PuestoInexistente_RetornaNoEncontrado`.
- **API:** integration `ApiWebApplicationFactory` — `GetConsulta_ConStatusEliminadas`, `SinStatusDevuelveActivas`, `ClienteAnonimoDevuelve401`, `GetAll_NoModificaShape`.
- **Web client:** unit `PuestosApiClientTests` + `FakePuestosApiClientTests` — `QueryAsync_BuildsUri`, `DefaultPageSize20`, `StatusEliminadas_SerializaEliminadas`.
- **Web index:** integration `SgvWebApplicationFactory` — `OnGet_WithEliminadasStatus_RendersReactivarButtons`, `WithSearch_QueriesApiClientWithSearch`, `ToggleEliminadas_RendersAsLink`, `OnPostDelete_Conflict409_MuestraMensajeEspecifico`.
- **Contratos:** unit `PuestoListQueryTests.cs` — `Default_SegmentoEsActivas`, `PuedeConstruirQueryParaEliminadas`.

## Plan de entrega (stacked-to-main)

**PR1 backend** (base `develop` → `main`, ≈250 líneas, 4 commits): (1) `feat(contracts): PuestoListQuery + PuestoSegmentoListado + record test`; (2) `feat(repo+consulta): PuestoRepository.QueryAsync + PuestoServicioConsulta.QueryAsync + [MySqlFact]`; (3) `feat(api+protection): PuestosController.GetConsulta + DesactivarAsync guard + ctor 7-parámetros + integration tests`; (4) `test(backend): PuestoListQueryTests + PuestoServicioComandosTests + PuestosControllerTests`.

**PR2 web** (base `develop` con PR1 mergeado, ≈280 líneas, 3 commits): (1) `feat(web-client): PuestosApiClient.QueryAsync + BuildQueryUri + type alias`; (2) `feat(web-index): Puestos/Index refactor LoadAsync + toggle Eliminadas activo`; (3) `test(web): FakePuestosApiClientTests + PuestoIndexPageTests + PuestosApiClientTests`.

Cada commit cierra con `dotnet build SGV.slnx` + `dotnet test SGV.slnx` verdes. Sin paralelismo entre slices.

## Riesgos residuales

- **R1:** Specs Puestos vigentes usan `Purpose/Requirements`; este change usa `REQ-PTO-XXX` + G/W/T (deliberado). → Archive documenta la delta; no migrar specs históricas.
- **R2:** Mapeo `Page` (record) ↔ `page` (HTTP). → DEC-7: `BuildQueryUri` con `page=N&pageSize=M`; controller mapea `[FromQuery]` → record.
- **R3:** Delta doble sobre `puesto-web-listado-detalle-baja` (spec) vs `puesto-management` (proposal). → Archive archiva DOS deltas: `puesto-management` (REQ-PTO-010) y `puesto-web-listado-detalle-baja` (REQ-PTO-020); `archive-report.md` reconcilia.
- **R4:** Ctor primario cambia firma 6 → 7. → DEC-2 mantiene ctor legacy 4; fixtures no construyen el servicio.
- **R5:** Mapping 409 depende de `Categoria = Conflict` explícito. → DEC-3; cubierto por `ApiResultsTests`.
- **R6:** Constraint UX activos (columna generada) vs nueva query. → Filtro opera sobre `IsActive/IsDeleted`, no la columna.
- **R7:** `QueryAsync` no usa `Query` base. → DEC-4; comentario en código referencia este design.
- **R8:** `[MySqlFact]` skipea sin MySQL (146 skipped). → Documentado en AGENTS.md; slice 2 sin dependencia nueva.

## Threat Matrix

N/A — el change no toca routing, shell commands, subprocesses, VCS/PR automation, executable-file classification ni process integration. Solo agrega endpoint HTTP, guarda de lectura y cliente tipado. Las superficies nuevas (query EF + JSON) ya están cubiertas por `web-apiclient-transport-contract` archivado.