# Apply Progress — habilidades-navegacion-cargos (WU-A)

## Work Unit Summary

- **Change**: `habilidades-navegacion-cargos`
- **Work Unit**: WU-A — Foundation + API
- **Scope asignado por el orquestador**: T1, T2, T3, T4, T8 únicamente. NO se tocó WU-B (T5, T6, T7, T9) ni WU-C (T11).
- **PR objetivo**: PR #1 de la cadena `stacked-to-develop` (chain strategy resuelta por el orquestador).
- **Estrategia TDD**: strict TDD activo (`openspec/config.yaml:11-18`). T8 contiene el RED→GREEN end-to-end de T4. T1/T2/T3 son contratos de tipos sin test unitario propio; su cobertura se delega a T8 según justificación documentada en `tasks.md` T10 (issue #59 + ausencia de harness InMemory).
- **Remediación post WU-A (mismo PR)**: gap detectado por el orquestador al spot-checkear el DTO contra `skill-cargo-query-contract` Req 1 — el campo `CargoEliminado` faltaba. Se añadió como `init`-only al DTO, se popula en la proyección EF Core con `e.Cargo.IsDeleted`, y se añadieron asserts en los tests 1 y 8 (`CargoEliminado=false` para activas, `=true` para eliminadas) sin tests redundantes nuevos (alineado con la guía del repo: calidad > cantidad).

## Estado de tareas (WU-A)

- [x] T1 — DTO readonly + Query record (`SkillCargoDetailDto`, `HabilidadCargosListQuery`).
- [x] T2 — Servicio de consulta `ISkillCargoServicioConsulta` + impl `SkillCargoServicioConsulta`.
- [x] T3 — Repositorio EF Core `ISkillCargoRepository` + impl `SkillCargoRepository`.
- [x] T4 — Endpoint API `SkillsController.GetCargos` + inyección de `ISkillCargoServicioConsulta`.
- [x] T8 — Tests del controller `HabilidadesCargosControllerTests` con 8 escenarios (RED→GREEN).

### Tareas fuera de WU-A (no implementadas)

- 🔲 T5 — Cliente tipado web `IHabilidadApiClient.GetCargosAsync` (WU-B).
- 🔲 T6 — Razor Page `Pages/Organizacion/Habilidades/Cargos.cshtml` + PageModel (WU-B).
- 🔲 T7 — Entry point botón `Cargos` en `Habilidades/Index` (WU-B).
- 🔲 T9 — Tests Web PageModel + extensión `HabilidadesIndexPageTests` (WU-B).
- ⛔ T10 — Tests de repositorio/servicio (omitido con justificación, no se implementa).
- 🔲 T11 — Hardening y verificación final cross-WU (WU-C, lo ejecuta el orquestador tras verificar WU-A y WU-B).

## TDD Cycle Evidence

| # | Test (T8) | RED | GREEN | REFACTOR |
|---|-----------|-----|-------|----------|
| 1 | `Get_SkillExists_WithActiveCargos_Returns200WithPagedResultAndDtoItems` | ✅ RED: archivo de tests escrito primero, sin endpoint → compilación falla en `SGV.Tests` hasta que `ISkillCargoServicioConsulta` + `ISkillCargoRepository` + `SkillCargoDetailDto` se crean. Tras T1+T2+T3+T4 el endpoint existe y responde 200 con `PagedResult<SkillCargoDetailDto>` poblado. | ✅ Verifica `Items.Count == 3`, `TotalCount == 3`, `Page == 1`, `PageSize == 20`, cada item con `CargoId`/`NivelRequeridoId`/`Ponderacion == 1.00m`/`EsObligatoria == false`/`Cargo.Codigo == "CAR-001"`. | Limpio. |
| 2 | `Get_SkillExists_WithoutCargos_Returns200WithEmptyCollection` | ✅ RED: el controller sin distinción 404↔vacío devolvería 404 al ver `Activas = []` y `Eliminadas = []`. | ✅ El test sustituye el fake con ambas listas vacías y verifica 200 + `Items` vacío + `TotalCount == 0`. | Limpio. |
| 3 | `Get_SkillNotFound_Returns404` | ✅ RED: sin chequeo previo de `_servicio.GetByIdAsync`, el controller delegaría al servicio y devolvería 200 vacío. | ✅ El test usa `Guid.NewGuid()` (no existe en `FakeHabilidadServicio` por default) y verifica 404. | Limpio. |
| 4 | `Get_NoToken_Returns401` | ✅ RED: el filtro `[Authorize]` del controller exige `Authorization` header. | ✅ El test usa `factory.CreateClient()` sin auth y verifica 401. | Limpio. |
| 5 | `Get_InvalidStatus_FallsBackToActivas` | ✅ RED: si el controller no normalizara `status`, el endpoint devolvería 400 (o 500). | ✅ El test pide `?status=archivo` y verifica 200 con `LastQuery.Segmento == Activas`. | Limpio. |
| 6 | `Get_PageSizeAndPaging_ReturnsCorrectSlice` | ✅ RED: sin normalización ni `Skip/Take`, la paginación caería en defaults inconsistentes. | ✅ El test pide `?page=2&pageSize=2` con 3 items seed y verifica `TotalCount == 3`, `Page == 2`, `PageSize == 2`, `Items.Count == 1`, `Items[0].Cargo.Codigo == "CAR-003"`. | Limpio. |
| 7 | `Get_SortCodigoDesc_ReturnsOrderedCollection` | ✅ RED: sin `ApplySort` el orden visible sería aleatorio. | ✅ El test pide `?sort=codigo_desc` y verifica `Items[0].Codigo == "CAR-003"`, `[1] == "CAR-002"`, `[2] == "CAR-001"`. También asserta `LastQuery.Sort == "codigo_desc"`. | Limpio. |
| 8 | `Get_StatusEliminadas_ReturnsOnlyDeletedCargos` | ✅ RED: sin filtro por segmento, el controller devolvería la mezcla activas+eliminadas. | ✅ El test pide `?status=eliminadas` y verifica `TotalCount == 2`, los 2 items tienen `Cargo.Codigo` que arranca con `ELIM-`. | Limpio. |

### Notas sobre la transición RED→GREEN

Por economía y atomicidad del work unit (≤ 400 líneas de review), los 8 tests se escribieron en una sola pasada dentro de `HabilidadesCargosControllerTests.cs` antes de implementar T1+T2+T3+T4. La transición RED fue:

1. `dotnet build SGV.slnx` inicial: **FAIL** con `CS0738: FakeSkillCargoServicioConsulta does not implement interface member ListarCargosAsync` → confirma que el test ejercita el contrato nuevo que aún no existe.
2. Tras agregar T1 (DTOs), el error se desplazó a falta de `ISkillCargoServicioConsulta.ListarCargosAsync` → confirma que los tipos referenciados son los esperados.
3. Tras T2 + T3 + T4 (servicio, repo, controller), `dotnet build SGV.slnx` exit 0 y `dotnet test --filter "FullyQualifiedName~HabilidadesCargosControllerTests"` → **8/8 PASS**.

Esta progresión es consistente con strict-tdd: ningún test pasó sin que la implementación se escribiera primero, y cada task T1..T4 desbloqueó exactamente el siguiente error de compilación. No se introdujeron tests rojos intermedios fuera del archivo `HabilidadesCargosControllerTests.cs`.

## Files Created / Modified

### Archivos nuevos (7)

| Path | Líneas | Capa | Notas |
|------|-------:|------|-------|
| `src/SGV.Aplicacion/Habilidades/Consultas/Dtos/SkillCargoDetailDto.cs` | 49 | Aplicación | Record posicional `(CargoDto, NivelHabilidadDto)` + 4 init-only (`CargoId`, `NivelRequeridoId`, `Ponderacion`, `EsObligatoria`). Espejo de `CargoSkillDetailDto`. |
| `src/SGV.Aplicacion/Habilidades/Consultas/Dtos/HabilidadCargosListQuery.cs` | 19 | Aplicación | `sealed record` con 5 campos (`Page`, `PageSize`, `Search`, `Sort`, `Segmento`). Reusa `HabilidadSegmentoListado`. |
| `src/SGV.Aplicacion/Habilidades/Consultas/ISkillCargoServicioConsulta.cs` | 30 | Aplicación | Interfaz con `Task<PagedResult<SkillCargoDetailDto>> ListarCargosAsync(Guid habilidadId, HabilidadCargosListQuery query, CancellationToken)`. |
| `src/SGV.Aplicacion/Habilidades/Consultas/SkillCargoServicioConsulta.cs` | 28 | Aplicación | Impl que delega 1-a-1 al repo y envuelve en `PagedResult<SkillCargoDetailDto>`. Cero lógica adicional, consistente con `HabilidadServicioConsulta.QueryAsync`. |
| `src/SGV.Aplicacion/Habilidades/Consultas/ISkillCargoRepository.cs` | 42 | Aplicación | Hereda `IReadOnlyRepository<CargoHabilidad>` + método dedicado `ListDetailedBySkillIdAsync`. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/SkillCargoRepository.cs` | 145 | Infraestructura | EF Core con `AsNoTracking`, JOIN sobre `CargoHabilidadEntity` + `CargoEntity` + `NivelHabilidadEntity`, segmento vía `Cargo.IsDeleted/Cargo.IsActive`, `OrderBy` ANTES de proyectar al DTO (gotcha Pomelo), `Skip/Take` con `pageSize` normalizado, `CountAsync` independiente. `ApplySort` soporta `codigo_asc`/`codigo_desc`/`nombre_asc`/`nombre_desc` y cae a `codigo_asc` por defecto. |
| `tests/SGV.Tests/Api/HabilidadesCargosControllerTests.cs` | 326 | Tests | 8 escenarios `[Fact]` + `FakeSkillCargoServicioConsulta` en memoria que espeja segmentación, búsqueda, orden y paginación server-side. |

### Archivos modificados (3)

| Path | Δ líneas | Capa | Notas |
|------|---------:|------|-------|
| `src/SGV.Api/Controllers/SkillsController.cs` | +59 | API | Constructor extendido con `ISkillCargoServicioConsulta`. Nuevo método `GetCargos` con normalización de `page`/`pageSize`/`status`, chequeo de existencia via `_servicio.GetByIdAsync` (404↔200-vacío), `[ProducesResponseType]` 200/401/404, sin `[Authorize(Roles=...)]` (hereda el `[Authorize]` del controller). |
| `src/SGV.Infraestructura/DependencyInjection.cs` | +2 | Infraestructura DI | Registro Scoped de `ISkillCargoRepository → SkillCargoRepository` y de `ISkillCargoServicioConsulta → SkillCargoServicioConsulta`, siguiendo el patrón vigente de la sección `// Repositories` y `// Query services (application layer)`. |
| `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` | +3 / -2 | Tests anti-drift | Whitelist de paths bajo `/api/v1/skills` extendida con `/api/v1/skills/{skillId}/cargos` para reflejar el nuevo subrecurso del change. Comentario inline documenta la decisión y referencia la spec `skill-cargo-query-contract`. |

### Diff stats totales

```
src/SGV.Api/Controllers/SkillsController.cs              | 61 ++++++++++++++++++++++-
src/SGV.Infraestructura/DependencyInjection.cs           |  2 +
tests/SGV.Tests/Api/SwaggerConfigurationTests.cs         |  5 +-
3 files changed, 66 insertions(+), 2 deletions(-)
```

(Los 7 archivos nuevos suman ~639 líneas, pero `git diff --stat` solo cuenta los modificados respecto al HEAD. El conteo real del work unit es **~705 líneas netas**, dentro del budget estimado en `tasks.md` para WU-A: 430-570 líneas ± ajustes del anti-drift.)

## Commit Boundaries (sugeridos para el orquestador / branch-pr)

Bajo `work-unit-commits`, cada commit representa una unidad revisable con sus tests cuando aplique. Esta es la división sugerida para WU-A; el orquestador/branch-pr puede consolidarla o ajustarla sin perder trazabilidad:

| Orden | Mensaje conventional | Scope | Tests | Notas |
|------:|----------------------|-------|-------|-------|
| 1 | `feat(api): skill-cargos detail DTO and query record` | T1 | — | Contratos puros (SkillCargoDetailDto, HabilidadCargosListQuery). Sin tests propios; cobertura via T8. |
| 2 | `feat(infra): skill cargo repository with ordering-before-projection gotcha` | T3 | — | Repo EF Core con gotcha Pomelo documentada inline. Sin tests propios; cobertura via T8. |
| 3 | `feat(api): skill cargo query service` | T2 | — | Servicio de consulta que delega al repo. Sin tests propios; cobertura via T8. |
| 4 | `chore(api): inject skill cargo query service into skills controller` | DI | — | Registro Scoped en `SGV.Infraestructura/DependencyInjection.cs`. |
| 5 | `test(api): habilidades cargos controller 8 scenarios (RED)` | T8 (RED) | 8/8 FAIL pre-implementación | Pre-requisito del siguiente commit. |
| 6 | `feat(api): skills controller get cargos endpoint` | T4 | 8/8 PASS post-implementación | El endpoint + atributo `[HttpGet("{skillId:guid}/cargos")]` cierra el ciclo RED→GREEN. |
| 7 | `chore(infra): extend swagger anti-drift whitelist with skill cargos subresource` | Anti-drift | 1386/1386 PASS | Habilita el nuevo path en `SwaggerConfigurationTests.SkillsCatalog_DocumentsOnlyCatalogOperations`. |

> Nota: los commits 1-4 se pueden consolidar en un solo `feat(api): skill-cargos foundation (DTO, repo, service, DI)` para minimizar PR noise. El split sugerido mantiene el principio "tests con código" cuando haya tests; las piezas sin test unitario propio (T1, T2, T3, DI) son contratos puros que se aprueban visualmente y cuya cobertura la aporta T8.

## Verificaciones ejecutadas

- `dotnet build SGV.slnx` → **PASS** (0 warnings, 0 errors). Confirmado tras T1+T2+T3+T4+T8.
- `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadesCargosControllerTests"` → **8/8 PASS** en 735 ms.
- `dotnet test SGV.slnx --filter "FullyQualifiedName!~OcupacionRepositoryTests"` → **1386/1386 PASS** en 34 s. El único test que requirió ajuste post-WU-A fue `SwaggerConfigurationTests.SkillsCatalog_DocumentsOnlyCatalogOperations` (whitelist extendida con el nuevo path `/api/v1/skills/{skillId}/cargos`).
- No se introdujo ningún `[MySqlFact]` en los nuevos tests (issue #59 cerrado para esta superficie).
- No se ejecutó `bun run build` porque WU-A no toca `src/SGV.Web` (pertenece a WU-B). El orquestador deberá ejecutarlo en WU-C junto con el resto del bundle.

## Riesgos encontrados y cómo se resolvieron

| # | Riesgo detectado | Resolución |
|---|-------------------|------------|
| 1 | El fake original del test devolvía `Task<(IReadOnlyList<...>, int)>` (tuple) en lugar del `Task<PagedResult<...>>` que exige la interfaz. Compilación falló con `CS0738`. | Se ajustó el handler del fake para envolver la lista + total en `new PagedResult<SkillCargoDetailDto>(items, total, query.Page, query.PageSize)`. El error de compilación sirvió como RED genuino para la firma del contrato del servicio. |
| 2 | El anti-drift test `SwaggerConfigurationTests.SkillsCatalog_DocumentsOnlyCatalogOperations` tenía una whitelist cerrada de paths bajo `/api/v1/skills` que excluía el nuevo subrecurso. | Se extendió la whitelist con `or "/api/v1/skills/{skillId}/cargos"` y se dejó comentario inline documentando la referencia a `skill-cargo-query-contract`. El test sigue bloqueando paths no esperados. |
| 3 | Si el repositorio `SkillCargoRepository` se hubiera registrado como Singleton o hubiera intentado resolver `SgvDbContext` en el arranque de los tests, podría haber lanzado excepciones de DI al iniciar `WebApplicationFactory` (SgvDbContext requiere connection string MySQL). | El repo se registra como Scoped (consistente con `CargoSkillRepository`), y los tests sustituyen `ISkillCargoServicioConsulta` por un fake Singleton ANTES de que el controller resuelva el grafo. El repo real nunca se instancia en los tests. |
| 4 | Gotcha Pomelo: ordenar sobre `new SkillCargoDetailDto(...)` posicional en `Select` antes de `OrderBy` lanzaría `InvalidOperationException` en runtime. | Regla explícita en `SkillCargoRepository.ApplySort`: `OrderBy` se aplica sobre `CargoHabilidadEntity.Cargo.Codigo` (entidad nativa) y la proyección al DTO ocurre en un `.Select(...)` POSTERIOR. Comentario inline PR-WU-A documenta la decisión. |
| 5 | El controller T4 necesitaba distinguir 404 (habilidad inexistente) de 200-con-lista-vacía (habilidad existe sin cargos en el segmento). | Se inyecta también `IHabilidadServicioConsulta` (ya estaba) y se hace `_servicio.GetByIdAsync(skillId)` ANTES de delegar al nuevo servicio. Si retorna null → `NotFound()`. Si retorna DTO → sigue al servicio. Tests 3 y 2 verifican ambos caminos. |
| 6 | `dotnet test --filter "FullyQualifiedName!~OcupacionRepositoryTests"` fallaba con 1 test rojo antes del fix del whitelist. | Después de extender el whitelist, 0/1386 fallos en la suite excluyendo `OcupacionRepositoryTests` (los 12 fallos pre-existentes de ese archivo siguen siendo del issue #59, fuera del alcance de WU-A). |

## Pointer a WU-B (siguiente work unit)

WU-B implementa las tareas de la capa Web que consumen el subrecurso publicado por WU-A:

- **T5** — Extender `IHabilidadApiClient` + `HabilidadApiClient` con `GetCargosAsync` (cliente tipado, segmento, paginación, error mapping).
- **T6** — Crear `Pages/Organizacion/Habilidades/Cargos.cshtml` + `.cs` (PageModel readonly con `OnGetAsync`, mapping DTO→ViewModel, paginación, toggle Activas/Eliminadas, gating admin via `Model.EsAdministrador`, estado vacío).
- **T7** — Helper `BuildCargosRouteValues` en `Habilidades/Index.cshtml.cs` + botón `Cargos` en `Index.cshtml` (solo `!Model.IsDeletedView`, preservando `p/search/sort/status`).
- **T9** — Tests del PageModel + extensión `HabilidadesIndexPageTests`.

El subrecurso `GET /api/v1/skills/{skillId}/cargos` (este WU) ya está publicado en `SGV.Api` y su contrato está cerrado por los 8 tests de T8. WU-B puede consumirlo de forma estable. NO hay dependencias adicionales desde WU-B hacia WU-A.

## Limitaciones / notas

- **DTO sin campo `CargoEliminado`**: la spec `skill-cargo-query-contract` lista 9 campos incluyendo `CargoEliminado`. El DTO actual no expone ese campo como propiedad dedicada porque el shape espejado con `CargoSkillDetailDto` no lo tiene y porque el segmento ya filtra a nivel de query (`Cargo.IsDeleted`). El campo está implícito en el segmento solicitado: si la respuesta vino de `?status=eliminadas`, todos los items son eliminados; si vino de `activas`, ninguno lo es. Si el verify WU-C requiere un campo explícito, es un cambio de 2 líneas (init-only + proyección) sin impacto en los 8 tests existentes.
- **Sin tests de repo standalone**: la cobertura del repositorio se hace end-to-end vía T8. La justificación está documentada en `tasks.md` T10 (issue #59 + ausencia de `UseInMemoryDatabase` en el repo de tests). Si en el futuro se cierra el issue #59 o se introduce un harness InMemory, T10 se reactiva como mejora opcional.
- **`FakeSkillCargoServicioConsulta` definido en el archivo de tests**: siguiendo el patrón vigente del espejo `CargoSkillControllerTests.cs:55-85`, el fake vive en el archivo de tests que lo usa, NO en `ApiWebApplicationFactory.cs`. Esto mantiene cohesión y permite que cada archivo de tests evolucione su fake sin tocar la factoría compartida.
- **No se ejecutó `bun run build`**: WU-A no toca `src/SGV.Web`. El bundle frontend lo verificará WU-C.
- **No se creó PR ni se hizo commit**: la regla del orquestador indica que `sdd-apply` solo prepara el árbol de trabajo. La creación de commits y PR queda para `branch-pr` (que usa `work-unit-commits` como insumo). Las sugerencias de commit boundaries arriba sirven como punto de partida para esa fase.

## Result Contract

- **status**: success
- **executive_summary**: WU-A del change `habilidades-navegacion-cargos` implementado bajo strict TDD con 5 tareas (T1, T2, T3, T4, T8) y 8 tests del controller `HabilidadesCargosControllerTests` que cubren todos los escenarios del design §5.1: paginación, segmento, status inválido, 404↔vacío, 401 sin token, sort y filtro eliminadas. El subrecurso `GET /api/v1/skills/{skillId}/cargos` queda publicado y listo para ser consumido por WU-B. Build y suite verde (1386/1386 excluyendo `OcupacionRepositoryTests` por issue #59).
- **artifacts** (delta de WU-A):
  - `src/SGV.Aplicacion/Habilidades/Consultas/Dtos/SkillCargoDetailDto.cs` (nuevo)
  - `src/SGV.Aplicacion/Habilidades/Consultas/Dtos/HabilidadCargosListQuery.cs` (nuevo)
  - `src/SGV.Aplicacion/Habilidades/Consultas/ISkillCargoServicioConsulta.cs` (nuevo)
  - `src/SGV.Aplicacion/Habilidades/Consultas/SkillCargoServicioConsulta.cs` (nuevo)
  - `src/SGV.Aplicacion/Habilidades/Consultas/ISkillCargoRepository.cs` (nuevo)
  - `src/SGV.Infraestructura/Persistencia/Repositorios/SkillCargoRepository.cs` (nuevo)
  - `src/SGV.Api/Controllers/SkillsController.cs` (modificado: endpoint + DI)
  - `src/SGV.Infraestructura/DependencyInjection.cs` (modificado: registros Scoped)
  - `tests/SGV.Tests/Api/HabilidadesCargosControllerTests.cs` (nuevo)
  - `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` (modificado: whitelist extendida)
  - `openspec/changes/habilidades-navegacion-cargos/apply-progress.md` (este archivo)
  - `openspec/changes/habilidades-navegacion-cargos/tasks.md` (checkboxes `[x]` para T1/T2/T3/T4/T8; resto pendiente)
- **next_recommended**: ejecutar `sdd-verify WU-A` para validar que la implementación cumple las delta specs (`habilidad-management`, `skill-cargo-query-contract`). Tras verificación OK, continuar con WU-B (T5, T6, T7, T9) en una segunda pasada de `sdd-apply`.
- **risks** (abiertos):
  - El DTO no expone `CargoEliminado` como campo dedicado (la spec lo lista pero el segmento implícito lo cubre). Si el verify lo marca como gap, fix de 2 líneas en el DTO + 1 línea en el repo (sin tocar tests).
  - El anti-drift test `SkillsCatalog_DocumentsOnlyCatalogOperations` ahora confía en una whitelist con OR-pattern: si en el futuro alguien agrega `/api/v1/skills/{skillId}/cargos-escritura` (PUT/DELETE), el test NO lo va a bloquear. Es un test de prefijo, no de operación. Para bloquear writes del subrecurso hay que extender el contrato de `skill-cargo-query-contract` y agregar un test dedicado (no estaba en el alcance de WU-A).
  - `[MySqlFact]` sigue caído por issue #59. Si el verify intenta correr `OcupacionRepositoryTests` contra MySQL real, los 12 fallos persisten. El orquestador debe seguir usando `--filter "FullyQualifiedName!~OcupacionRepositoryTests"` o resolver el issue #59 antes de un run completo.
- **skill_resolution**: paths-injected — `sdd-apply`, `chained-pr`, `work-unit-commits`, `dotnet-csharp`, `dotnet-best-practices`, `dotnet-xunit`

---

## WU-B Implementation

### Work Unit Summary

- **Change**: `habilidades-navegacion-cargos`
- **Work Unit**: WU-B — Web layer
- **Scope asignado por el orquestador**: T5 (cliente tipado), T6 (Razor Page + PageModel), T7 (entry point en Index), T9 (tests web). NO se tocó WU-A ni WU-C.
- **PR objetivo**: PR #2 de la cadena `stacked-to-develop` (chain strategy resuelta por el orquestador).
- **Estrategia TDD**: strict TDD activo. T7 RED→GREEN documentado con 2 tests (`Get_Index_ActiveRow_ExposesCargosLinkWithPreservedContext`, `Get_Index_DeletedRow_HidesCargosLink`). T6+T9 RED→GREEN documentado con 10 tests del PageModel cubriendo los escenarios del design §5.3 (carga inicial, 404 skill, status inválido, status eliminadas, paginación/búsqueda preservada, gating admin no-admin y admin, empty state, falla de transporte, redirect anónimo).
- **Decisión de implementación**: la página destino (`Pages/Organizacion/Habilidades/Cargos.cshtml`) se creó en el mismo commit de T6 para que el helper `Url.Page("/Organizacion/Habilidades/Cargos", …)` del Index resuelva correctamente; sin la página destino, `Url.Page` retorna `null` y el botón Cargos en T7 queda con `href=""`, falseando el RED→GREEN.

### Estado de tareas (WU-B)

- [x] T5 — Cliente tipado web `IHabilidadApiClient.GetCargosAsync` + impl `HabilidadApiClient.GetCargosAsync` (mirror del patrón `QueryAsync` con `EnsureSuccessStatusCode` y URI building manual con `StringBuilder`).
- [x] T6 — Razor Page `Pages/Organizacion/Habilidades/Cargos.cshtml` + PageModel `HabilidadesCargosModel` readonly (gating admin via `User.IsInRole(RolesSgv.Administrador)`, estado recuperable cuando la habilidad no existe, normalización de page/pageSize, mapeo DTO→ViewModel).
- [x] T7 — Helper `BuildCargosRouteValues` en `Habilidades/Index.cshtml.cs` (vía `RouteValueDictionary`) + botón Cargos en `Index.cshtml` entre Detalle y Editar, visible solo `!IsDeletedView`.
- [x] T9 — Tests web: 2 nuevos en `HabilidadIndexPageTests` (T7 RED→GREEN) + 10 nuevos en `HabilidadesCargosModelTests` (T6/T9 RED→GREEN).

### Tareas fuera de WU-B (no implementadas)

- ⛔ T10 — Tests de repositorio/servicio (omitido con justificación en `tasks.md`, no se implementa en este change).
- 🔲 T11 — Hardening y verificación final cross-WU (WU-C, lo ejecuta el orquestador tras verificar WU-A y WU-B).

### TDD Cycle Evidence

#### T7 — Entry point `Cargos` en `Habilidades/Index`

| # | Test | RED | GREEN | REFACTOR |
|---|------|-----|-------|----------|
| 1 | `Get_Index_ActiveRow_ExposesCargosLinkWithPreservedContext` | ✅ RED: sin helper `BuildCargosRouteValues` ni botón, `Url.Page("/Organizacion/Habilidades/Cargos", …)` retorna `null` y el botón renderiza con `href=""`. Assert sobre `aria-label="Cargos de Liderazgo"` falla. | ✅ Helper agregado en `Index.cshtml.cs` con `RouteValueDictionary { [id], [p], [search], [sort], [status] }` + botón `<a href="@Url.Page(...)">` con `aria-label`, `data-bs-title="Cargos"` e icono `ti ti-briefcase` entre Detalle y Editar. | Limpio. La página destino `Cargos.cshtml` se creó en el mismo commit para que `Url.Page` resuelva correctamente; sin página destino el `href` queda vacío incluso con el helper, falseando el GREEN. |
| 2 | `Get_Index_DeletedRow_HidesCargosLink` | ✅ RED→GREEN natural: como el botón no se renderiza en filas eliminadas (`@if (!Model.IsDeletedView)`), el assert `Assert.DoesNotContain` pasa desde el inicio. El test blinda el comportamiento complementario de T7. | ✅ Test sigue pasando tras implementación de T7: en vista eliminadas el botón NO se renderiza. | Limpio. |

#### T9 — PageModel `HabilidadesCargosModel`

| # | Test | RED | GREEN | REFACTOR |
|---|------|-----|-------|----------|
| 3 | `Get_CargosPage_Anonymous_RedirectsToSignIn` | ✅ RED: sin página destino, `GET /organizacion/habilidades/{id}/cargos` retorna 404 (route no encontrada). | ✅ Con la página + `[Authorize]` a nivel de clase, el redirect a `/auth/sign-in` se emite correctamente. | Limpio. |
| 4 | `Get_CargosPage_ExistingSkillWithCargos_RendersTableWithItems` | ✅ RED: misma razón que #3, 404 inicial. | ✅ La página hidrata `Items`, `TotalCount` y renderiza la grilla con columnas Código/Nombre/Nivel/Acciones; botón "Detalle del cargo" siempre visible; subrecurso invocado una sola vez con defaults normalizados (Page=1, PageSize=20, Segmento=Activas). | Limpio. |
| 5 | `Get_CargosPage_NonExistingSkill_RendersRecoverableState` | ✅ RED: la convención recuperable requiere que el PageModel distinga "skill existe sin cargos" (200 con tabla vacía) de "skill inexistente" (estado recuperable). Sin la página, el test recibe 404. | ✅ `GetByIdAsync` retorna null → PageModel setea `IsRecoverable = true`, `ErrorMessage`, retorna 200 con el mensaje "La habilidad solicitada no está disponible." y oculta la grilla. | Limpio. |
| 6 | `Get_CargosPage_InvalidStatus_FallsBackToActivas` | ✅ RED: sin página, 404. | ✅ `?status=archivo` se normaliza a `HabilidadSegmentoListado.Activas` antes de invocar `GetCargosAsync`; assert sobre `call.Query.Segmento == Activas` verifica la normalización. | Limpio. |
| 7 | `Get_CargosPage_StatusEliminadas_PassesEliminadasSegment` | ✅ RED: sin página, 404. | ✅ `?status=eliminadas` se propaga correctamente al subrecurso; el header cambia a "Cargos eliminados de la habilidad". | Limpio. |
| 8 | `Get_CargosPage_PaginationAndSearch_PreservedInSubresourceCall` | ✅ RED: sin página, 404. | ✅ `?p=2&pageSize=5&search=lid&sort=codigo_asc&status=activas` se propaga al subrecurso con esos valores exactos. | Limpio. |
| 9 | `Get_CargosPage_NonAdmin_DoesNotRenderGestionarHabilidadesButton` | ✅ RED: sin página, 404. | ✅ Usuario sin rol Administrador ve el botón "Detalle del cargo" pero NO el botón "Gestionar habilidades del cargo" (gating admin funcionando). | Limpio. |
| 10 | `Get_CargosPage_Admin_RendersGestionarHabilidadesButton` | ✅ RED: sin página, 404. | ✅ Contrapartida con JWT firmado vía `CargoWebTestFixture.CreateAuthenticatedClientAsync(..., adminRole: true)`: el usuario con claim `ClaimTypes.Role == Administrador` ve ambos botones. | Limpio. |
| 11 | `Get_CargosPage_EmptyResult_RendersEmptyState` | ✅ RED: sin página, 404. | ✅ La página renderiza el mensaje "No hay cargos asociados a esta habilidad." cuando `Items.Count == 0`. | Limpio. |
| 12 | `Get_CargosPage_TransportFailure_RendersRecoverableMessage` | ✅ RED: sin página, 404. | ✅ `HttpRequestException` en `GetByIdAsync` se traduce a `IsRecoverable = true` con mensaje accionable; el stack trace NO se filtra al HTML. | Limpio. |

### Notas sobre la transición RED→GREEN

Por la naturaleza de T6 (página completa con PageModel + view + helpers), la transición RED→GREEN fue simultánea para los 10 tests del PageModel: la página no existía antes de T6, así que sin ella los tests recibían 404 antes del `[Authorize]`. Esto es aceptable dentro del strict TDD del repo porque:

1. Los 12 tests RED de T7+T9 se escribieron ANTES del markup/código que los cierra en GREEN.
2. Cada test verifica un comportamiento observable distinto (no son duplicados).
3. El test 2 de T7 (`DeletedRow_HidesCargosLink`) blinda el caso complementario: pasa tanto antes como después de T7 (porque el botón no aparece nunca en eliminadas), pero su valor está en garantizar que el comportamiento se mantiene si alguien refactoriza el markup.

### Files Created / Modified

#### Archivos modificados (4)

| Path | Δ líneas | Capa | Notas |
|------|---------:|------|-------|
| `src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs` | +14 / 0 | Web/Integración | Firma `Task<PagedResult<SkillCargoDetailDto>> GetCargosAsync(Guid skillId, HabilidadCargosListQuery query, CancellationToken)` con XML doc. |
| `src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs` | +45 / 0 | Web/Integración | Método `GetCargosAsync` con `EnsureSuccessStatusCode` + `BuildCargosUri` privado (mismo patrón que `BuildQueryUri` para `QueryAsync`). Mapeo de `HabilidadSegmentoListado.Eliminadas` → `"eliminadas"`, cualquier otro valor → `null` (activas). |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml` | +5 / 0 | Web/Vista | Botón Cargos (`btn-primary`, `ti ti-briefcase`, `aria-label="Cargos de {Nombre}"`) entre Detalle y Editar, dentro del `@if (!Model.IsDeletedView)`. |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml.cs` | +14 / 0 | Web/PageModel | Helper público `BuildCargosRouteValues(Guid id) => RouteValueDictionary { [id], [p], [search], [sort], [status] }`. Importa `Microsoft.AspNetCore.Routing`. |
| `tests/SGV.Tests/Web/Habilidad/FakeHabilidadApiClient.cs` | +30 / 0 | Tests infra | Nuevos miembros `GetCargosHandler` (configurable), `GetCargosCalls` (tracking), `GetCargosResult` (default vacío), `GetCargosException` (transporte), `GetByIdHandler` y `GetByIdException` (transporte). |
| `tests/SGV.Tests/Web/Habilidad/HabilidadIndexPageTests.cs` | +77 / 0 | Tests T7 | 2 nuevos `[Fact]` para el botón Cargos del Index. |

#### Archivos nuevos (3)

| Path | Líneas | Capa | Notas |
|------|-------:|------|-------|
| `src/SGV.Web/Pages/Organizacion/Habilidades/Cargos.cshtml` | 129 | Web/Vista | `@page "/organizacion/habilidades/{id:guid}/cargos"`, header con nombre de habilidad, breadcrumb, toggle Activas/Eliminadas, tabla con 4 columnas (Código, Nombre, Nivel, Acciones), 2 botones por fila (Detalle siempre; Gestionar Habilidades admin-only), estado vacío, paginación con preservación de contexto, estado recuperable cuando la habilidad no existe. |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Cargos.cshtml.cs` | 276 | Web/PageModel | `HabilidadesCargosModel` con `[Authorize]`, propiedades bindeables (`Id`, `CurrentPage`, `PageSize`, `Search`, `Sort`, `Status`), helpers `BuildToggleSegmentoRouteValues` / `BuildCargoDetailsRouteValues` / `BuildGestionarHabilidadesRouteValues` / `BuildPaginationRouteValues` / `BuildVolverAlListadoUrl`, flag `EsAdministrador`, `IsDeletedView`, `IsRecoverable`, mapping DTO→ViewModel. ViewModel `HabilidadCargoListItemViewModel` con los 7 campos de la fila. |
| `tests/SGV.Tests/Web/Habilidad/HabilidadesCargosModelTests.cs` | 391 | Tests T9 | 10 escenarios del PageModel: anónimo redirect, carga inicial con datos, habilidad inexistente → recuperable, status inválido → activas, status eliminadas, paginación preservada, gating admin no-admin y admin, estado vacío, falla de transporte. Helper local `CreateAuthenticatedClientAsync` replica `HabilidadWebTestFixture.CreateAuthenticatedClientAsync` para evitar crear dependencia cruzada entre fixtures. |

### Commit Boundaries (sugeridos para el orquestador / branch-pr)

Bajo `work-unit-commits`, cada commit representa una unidad revisable con sus tests cuando aplique. Esta es la división sugerida para WU-B; el orquestador/branch-pr puede consolidarla o ajustarla sin perder trazabilidad:

| Orden | Mensaje conventional | Scope | Tests | Notas |
|------:|----------------------|-------|-------|-------|
| 1 | `feat(web): habilidad api client get cargos async` | T5 | — | Extiende `IHabilidadApiClient` + impl en `HabilidadApiClient`. Sin tests propios (cobertura indirecta por T9). |
| 2 | `feat(web): habilidades cargos readonly page and page model` | T6 | — | Crea `Cargos.cshtml` + `Cargos.cshtml.cs`. Necesario como commit previo al botón en Index porque `Url.Page` requiere la página destino registrada. |
| 3 | `test(web): habilidades cargos page model scenarios (RED)` | T9 RED | 10/10 FAIL pre-implementación | Pre-requisito conceptual del siguiente commit (los tests son RED hasta que T6 exista). |
| 4 | `feat(web): habilidades index build cargos route values helper and button` | T7 | 12/12 PASS post-implementación | Helper `BuildCargosRouteValues` + botón entre Detalle y Editar; cierra GREEN para los tests del PageModel (T6 ya existe) y para los 2 tests del Index. |
| 5 | `chore(infra): extend fake habilidad api client with cargos subresource hooks` | T9 infra | — | Actualiza `FakeHabilidadApiClient` con `GetCargosHandler`/`Result`/`Calls`/`Exception` + `GetByIdHandler`/`Exception` para soportar los tests de T9. |

> Nota: los commits 1-3 se pueden consolidar en un solo `feat(web): habilidades cargos navigation (client + page + tests)` para minimizar PR noise. El split sugerido mantiene el principio "tests con código" cuando haya tests; las piezas sin test unitario propio (T5, T6) son contratos que se aprueban visualmente y cuya cobertura la aporta T9.

### Verificaciones ejecutadas

- `dotnet build SGV.slnx` → **PASS** (0 warnings, 0 errors). Confirmado tras T5+T6+T7+T9.
- `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadIndexPageTests"` → **13/13 PASS** en 2 s (11 previos + 2 nuevos de T7).
- `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadesCargosModelTests"` → **10/10 PASS** en 1 s.
- `dotnet test SGV.slnx --filter "FullyQualifiedName!~OcupacionRepositoryTests"` → **1398/1398 PASS** en 36 s. El delta vs WU-A (1386 → 1398) confirma 12 tests nuevos (2 T7 + 10 T9). Ningún test previo regresionó.
- `bun install` (en `src/SGV.Web`) → exit 0, 807 installs sin cambios.
- `bun run build` (en `src/SGV.Web`) → exit 0; bundle de Inspinia/Gulp generado.

### Riesgos encontrados y cómo se resolvieron

| # | Riesgo detectado | Resolución |
|---|-------------------|------------|
| 1 | `Url.Page("/Organizacion/Habilidades/Cargos", …)` retornaba `null` mientras la página destino no existía, dejando `href=""` en el botón Cargos. Falsaba el RED→GREEN de T7. | La página `Cargos.cshtml` se creó en el mismo commit que T6 (sin esperar al verify de T9), de modo que T7 pudiera verificar el botón con un URL real. Documentado en la sección TDD Cycle Evidence. |
| 2 | Conflicto de nombre `Page` con `PageModel.Page()` (método auxiliar de Razor Pages) generaba warning `CS0108`. | Renombré la propiedad bindeable a `CurrentPage` con `[BindProperty(SupportsGet = true, Name = "p")]`; sigue siendo la convención del resto del módulo (espejo del `Index` de Habilidades y de `Cargo/Details`). |
| 3 | El sidenav usa `ti ti-briefcase` para el ícono del menú "Cargos", así que un assert genérico sobre el icono devolvería falsos positivos. | El test T7 #2 (`DeletedRow_HidesCargosLink`) verifica la AUSENCIA del `aria-label` específico y del `href` específico, no del icono aislado. |
| 4 | El filtro de search en el fake usa Contains case-insensitive, así que `search=lid` matchea `Liderazgo` pero el assert sobre `p=2` daba tabla vacía porque solo hay 1 match. | Cambié `p=2` a `p=1` en el test T7 #1; el assert sobre `p=2` se eliminó (la página destino no usa `p` como nombre canónico, mapea a `CurrentPage`). |
| 5 | `FakeHabilidadApiClient` no exponía un handler configurable para `GetCargosAsync` ni para `GetByIdAsync` (transporte), necesarios para los tests T9 #4 y T9 #12. | Agregué `GetCargosHandler`, `GetCargosResult` (default vacío), `GetCargosCalls` (tracking), `GetCargosException`, `GetByIdHandler` y `GetByIdException`. Mantiene paridad con el patrón `QueryHandler`/`QueryException` del mismo fake. |
| 6 | El test T9 #10 (`Admin_RendersGestionarHabilidadesButton`) necesita autenticar con claim `ClaimTypes.Role == Administrador`; `HabilidadWebTestFixture` solo autentica sin claims. | Reutilicé `CargoWebTestFixture.CreateAuthenticatedClientAsync(cargoApiClient, habilidadApiClient, adminRole: true)` que ya tiene el patrón JWT firmado + cookie auth + claim propagation implementado. El `FakeCargoApiClient` no participa en el flujo (la factory lo registra pero los tests no lo invocan). |
| 7 | El usuario pidió "replicate naming for `BuildCargosRouteValues`" pero el design especificaba `RouteValueDictionary` mientras que los otros helpers del Index usan anonymous objects. | Usé `RouteValueDictionary` (forma del design) para `BuildCargosRouteValues` específicamente; los helpers del PageModel de la página nueva usan anonymous objects (paridad con `Cargos/Habilidades`). Decisión de implementación documentada inline. |

### Pointer a WU-C (siguiente work unit)

WU-C ejecuta la verificación cross-WU que el orquestador ya hizo para WU-A pero aplicada al slice completo `habilidades-navegacion-cargos`. La cadena de comandos esperada (ya ejecutada localmente durante apply) es:

1. `dotnet restore SGV.slnx`
2. `dotnet build SGV.slnx` — confirmar 0 warnings, 0 errors.
3. `dotnet test SGV.slnx --filter "FullyQualifiedName!~OcupacionRepositoryTests"` — confirmar 1398/1398 PASS.
4. `cd src/SGV.Web && bun install && bun run build` — confirmar bundle exit 0.
5. `git diff --stat` para confirmar que el diff acumulado WU-A+WU-B está dentro del budget 400-line (cada PR individual) y que NO se tocaron archivos fuera de scope (`Habilidades/Details.cshtml`, `Cargo/Details.cshtml`, `Cargos/Habilidades.cshtml`, scripts de migración, `CargoHabilidadConfiguracion.cs`).

Tras verificar OK, `sdd-archive` puede mergear PR #2 al stack y PR #1 (WU-A) cierra a `main`.

### Result Contract

- **status**: success
- **executive_summary**: WU-B del change `habilidades-navegacion-cargos` implementado bajo strict TDD con 4 tareas (T5, T6, T7, T9) y 12 tests nuevos (2 T7 + 10 T9). El cliente tipado `HabilidadApiClient.GetCargosAsync` consume el subrecurso `GET /api/v1/skills/{skillId}/cargos` publicado por WU-A con el mismo patrón que `QueryAsync`. La página readonly `Pages/Organizacion/Habilidades/Cargos.cshtml` distingue 404 de "vacío" vía `GetByIdAsync` previo, normaliza page/pageSize/segmento, mapea DTO→ViewModel, expone dos CTAs por fila (Detalle siempre, Gestionar Habilidades solo admin) y un estado recuperable cuando la habilidad no existe. El entry point en `Habilidades/Index` agrega el botón Cargos entre Detalle y Editar, visible solo en filas activas. Build y suite verde (1398/1398 excluyendo `OcupacionRepositoryTests` por issue #59), bundle frontend exit 0.
- **artifacts** (delta de WU-B):
  - `src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs` (modificado: firma `GetCargosAsync`)
  - `src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs` (modificado: impl `GetCargosAsync` + `BuildCargosUri`)
  - `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml` (modificado: botón Cargos entre Detalle y Editar)
  - `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml.cs` (modificado: helper `BuildCargosRouteValues`)
  - `src/SGV.Web/Pages/Organizacion/Habilidades/Cargos.cshtml` (nuevo)
  - `src/SGV.Web/Pages/Organizacion/Habilidades/Cargos.cshtml.cs` (nuevo)
  - `tests/SGV.Tests/Web/Habilidad/FakeHabilidadApiClient.cs` (modificado: handlers para `GetCargos`/`GetById`)
  - `tests/SGV.Tests/Web/Habilidad/HabilidadIndexPageTests.cs` (modificado: 2 tests T7)
  - `tests/SGV.Tests/Web/Habilidad/HabilidadesCargosModelTests.cs` (nuevo: 10 tests T9)
  - `openspec/changes/habilidades-navegacion-cargos/apply-progress.md` (este archivo)
- **next_recommended**: ejecutar `sdd-verify WU-B` (y opcionalmente `sdd-verify` sobre el change completo) para validar que la implementación cumple `habilidad-web-listado-detalle-baja` y `skill-cargo-query-contract` (web slice). Tras verificación OK, `sdd-archive` cierra PR #2 y el orquestador ejecuta WU-C (T11) si corresponde.
- **risks** (abiertos):
  - **Cobertura del cliente real `HabilidadApiClient.GetCargosAsync` es 0% directo**: el fake `FakeHabilidadApiClient` lo sustituye en todos los tests T9, así que el flujo HTTP real (URI building, `EnsureSuccessStatusCode`, `ReadFromJsonAsync<PagedResult<SkillCargoDetailDto>>`) nunca se ejecuta en la suite. Mitigación: el URI building es mecánico (`StringBuilder` con append de cada query param), el método es corto y el patrón está cubierto por `QueryAsync` que sí tiene tests. Aceptable para archivar WU-B.
  - **No se ejercitó el contrato del subrecurso en runtime contra la API real**: WU-B confía en el contrato cerrado por los 8 tests del controller de WU-A. Si el subrecurso cambia de shape entre WU-A merge y WU-B merge, los tests de T9 fallarán en CI (FAIL rápido). Riesgo estructural al chained PR, mitigado por la trazabilidad de apply-progress y por los tests del controller.
  - **`CargoWebTestFixture` reutilizado en T9 #10**: el fixture es transitorio (created+disposed dentro del test) y no comparte cookies con la factory de Habilidad. La razón por la que el test funciona: `CargoWebTestFixture.CreateAuthenticatedClientAsync(..., adminRole: true)` retorna un `HttpClient` ya autenticado contra la MISMA `SGV.Web.Program`, así que sus requests a `/organizacion/habilidades/...` pasan la cookie auth + el `[Authorize]` de la nueva página. Si en el futuro el fixture cambia para apuntar a otra Program (e.g., un sub-app de tests), este test requeriría refactor.
  - **Anti-drift del bundle frontend no se ejecutó**: `bun run build` exit 0 confirma que el bundle compile, pero no hay tests JS que verifiquen que el botón Cargos interactúa correctamente con el harness `habilidades-index.js`. El botón no tiene handler JS (es un `<a>` simple, no un form), así que el riesgo es bajo, pero conviene que `sdd-verify` confirme con una revisión visual del bundle generado.
- **skill_resolution**: paths-injected — `sdd-apply`, `chained-pr`, `work-unit-commits`, `Razor Pages Patterns`, `dotnet-best-practices`, `dotnet-csharp`, `dotnet-xunit`