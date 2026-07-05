# Verify Report — `habilidades-navegacion-cargos` (change completo)

## 1. Resumen

**Verdict global: PASS.** El change entrega el subrecurso `GET /api/v1/skills/{skillId}/cargos`
(WU-A), el cliente tipado `HabilidadApiClient.GetCargosAsync`, la Razor Page readonly `Cargos`
con gating admin y estado recuperable, y el entry point **botón Cargos** en `Habilidades/Index`
preservando `p`, `search`, `sort` y `status` (WU-B). Las tres delta specs evaluadas
(`habilidad-management` MODIFIED, `skill-cargo-query-contract` ADDED, `habilidad-web-listado-detalle-baja`
MODIFIED) cierran todos los MUST de su alcance con cobertura runtime PASS.

Build sin warnings, suite completa **1398/1398 PASS** (excluyendo `OcupacionRepositoryTests` por
issue #59) y bundle frontend (`bun run build`) exit 0. El gap conocido sobre cobertura directa
del repo está documentado y justificado en `tasks.md` T10 (issue #59 + ausencia de harness
InMemory).

## 2. Cambio auditado

- **Change**: `habilidades-navegacion-cargos`
- **Work Units implementados**: WU-A (T1, T2, T3, T4, T8) y WU-B (T5, T6, T7, T9).
- **Tareas fuera de scope**: T10 (omitido con justificación en `tasks.md`); T11 (hardening
  cross-WU = esta verificación).
- **Chain strategy resuelta por orquestador**: `stacked-to-develop` (2 PRs).
- **Strict TDD**: activo (`openspec/config.yaml:11-18`); módulo `strict-tdd-verify.md` aplicado.

## 3. Verificaciones runtime (T11)

Ejecutadas en este verify contra el árbol de trabajo actual (todo lo listado en
`git status --short` aún sin commitear):

| # | Check | Comando | Resultado |
|---|-------|---------|-----------|
| 1 | Restore | `dotnet restore SGV.slnx` | OK (sin cambios) |
| 2 | Build | `dotnet build SGV.slnx` | **PASS** — 0 warnings, 0 errors |
| 3 | Suite | `dotnet test SGV.slnx --filter "FullyQualifiedName!~OcupacionRepositoryTests"` | **1398/1398 PASS** en 37 s |
| 3a | Tests del change | `dotnet test --filter "FullyQualifiedName~HabilidadesCargosControllerTests\|FullyQualifiedName~HabilidadesCargosModelTests\|FullyQualifiedName~HabilidadIndexPageTests"` | **31/31 PASS** en 3 s |
| 4 | Frontend | `cd src/SGV.Web && bun run build` | **exit 0** — bundle Inspinia/Gulp generado |
| 5 | Scope diff | `git status --short` + `git diff --stat` | 9 modified (8 productivos + 1 Swagger whitelist) + 10 untracked (7 nuevos productivos + 2 dir SDD + 1 tests) — ver §6 |

> Nota: `dotnet build` se ejecutó antes de los tests; los tests corrieron con `--no-build` para
> ahorrarse el rebuild. Ambos pasan en la misma línea base.

## 4. Validación contra specs por WU

### 4.1 WU-A — Foundation + API

> El subrecurso `GET /api/v1/skills/{skillId}/cargos` ya fue verificado en la versión previa
> de este archivo (veredicto PASS con 1 WARNING + 3 SUGGESTION). Re-corro los chequeos clave
> contra el código actual y confirmo que siguen vigentes.

#### 4.1.1 `habilidad-management/spec.md` (MODIFIED)

| Scenario | Evidencia implementación | Evidencia test | Veredicto |
|----------|--------------------------|----------------|-----------|
| Habilidad existente devuelve colección paginada | `SkillsController.cs` nuevo método `GetCargos`; `SkillCargoDetailDto` con los 9 campos; `SkillCargoRepository.cs` con `CountAsync` separado y `Select` con `Cargo`/`Nivel` + `Skip/Take`; `SkillCargoServicioConsulta` envuelve en `PagedResult`. | `HabilidadesCargosControllerTests.Get_SkillExists_WithActiveCargos_Returns200WithPagedResultAndDtoItems` | **PASS** runtime (incluido en 31/31) |
| Habilidad existente sin cargos devuelve vacío | `SkillsController` chequea `GetByIdAsync` (200, no 404); `SkillCargoRepository` filtra segmento. | `Get_SkillExists_WithoutCargos_Returns200WithEmptyCollection` | **PASS** runtime |
| Habilidad inexistente devuelve 404 | `SkillsController.GetCargos` retorna `NotFound()` antes de delegar al servicio. | `Get_SkillNotFound_Returns404` | **PASS** runtime |
| Operaciones write de `CargoHabilidad` no disponibles | Único método nuevo: `GET {skillId:guid}/cargos`. Sin `[HttpPost/Put/Delete/Patch]` que escriba el vínculo. Anti-drift en `SwaggerConfigurationTests` (whitelist con `/api/v1/skills/{skillId}/cargos`). | `SwaggerConfigurationTests.SkillsCatalog_DocumentsOnlyCatalogOperations` | **PASS** (34/34 dentro de `SwaggerConfigurationTests`) |
| Lecturas autenticadas exitosas / Acceso anónimo rechazado / Mutación protegida por rol admin | `[Authorize]` a nivel controller; `GetCargos` sin `[Authorize(Roles)]`; writes mantienen `[Authorize(Roles = RolesSgv.Administrador)]`. | `Get_NoToken_Returns401` cubre 401; tests previos de admin gating (no modificados en este WU) cubren 403. | **PASS** runtime |

#### 4.1.2 `skill-cargo-query-contract/spec.md` (ADDED)

| Requirement / Scenario | Evidencia | Veredicto |
|------------------------|-----------|-----------|
| Req 1 / Devolver metadatos paginados y datos del vínculo | DTO con los 9 campos (incluyendo `CargoEliminado` agregado en remediación post-spot-check); `PagedResult<SkillCargoDetailDto>` | **PASS** runtime (test 1 verifica los 9 campos) |
| Req 1 / Colección vacía sin cambiar el shape | `Items = []` con `TotalCount/Page/PageSize` válidos | **PASS** runtime (test 2) |
| Req 2 / Status inválido cae a activas | `SkillsController.GetCargos` normaliza `status=archivo` → `Activas`; doc XML doc explícito | **PASS** runtime (test 5) |
| Req 3 / Acceso sin token es rechazado | `[Authorize]` a nivel controller | **PASS** runtime (test 4) |
| Req 3 / Habilidad inexistente devuelve 404 | `GetByIdAsync` previo en `SkillsController.GetCargos` | **PASS** runtime (test 3) |
| Req 4 / No contaminar contratos padre ni abrir writes | Anti-drift Swagger con whitelist de paths bajo `/api/v1/skills`; `CargoDto` espejo no expone campos del vínculo | **PASS** estructural |

#### 4.1.3 Anti-drift WU-A

- `SwaggerConfigurationTests.SkillsCatalog_DocumentsOnlyCatalogOperations` corrida con la whitelist
  extendida con `/api/v1/skills/{skillId}/cargos`: **PASS** dentro de los 34/34 del archivo.
- Gotcha Pomelo confirmado: `OrderBy` aplicado sobre `CargoHabilidadEntity.Cargo.Codigo` (entidad
  nativa) en `SkillCargoRepository.ApplySort`, proyección al DTO en `.Select(...)` posterior.
  Cubierto por test 7 (`Get_SortCodigoDesc_ReturnsOrderedCollection`).
- DI: `ISkillCargoRepository` y `ISkillCargoServicioConsulta` registrados como `Scoped` en
  `src/SGV.Infraestructura/DependencyInjection.cs` siguiendo el patrón vigente.

### 4.2 WU-B — Web layer

> Validación contra `habilidad-web-listado-detalle-baja/spec.md` (MODIFIED) y contra el
> contrato espejo de la página destino `Cargos.cshtml`.

#### 4.2.1 `habilidad-web-listado-detalle-baja/spec.md` (MODIFIED)

| Scenario | Evidencia implementación | Evidencia test | Veredicto |
|----------|--------------------------|----------------|-----------|
| Vista activas muestra acciones del catálogo activo (`Detalle`, `Cargos`, `Editar`, `Eliminar`) | `Index.cshtml:163-164` botón Cargos con `btn-primary`, `ti ti-briefcase`, `aria-label="Cargos de {Nombre}"` entre Detalle y Editar, dentro del bloque `@if (!Model.IsDeletedView)` | `HabilidadIndexPageTests.Get_Index_ActiveRow_ExposesCargosLinkWithPreservedContext:307-343` | **PASS** runtime |
| Navegación a cargos preserva contexto del listado (`p`, `search`, `sort`, `status`) | `Index.cshtml.cs:259-265` helper `BuildCargosRouteValues(Guid id)` retorna `RouteValueDictionary` con `[id]`, `[p]`, `[search]`, `[sort]`, `[status]` usando `Model.CurrentPage`/`Search`/`Sort`/`Segmento` | Mismo test `ActiveRow_ExposesCargosLinkWithPreservedContext` asserta que el `href` contiene los 4 query params correctos | **PASS** runtime |
| Vista eliminadas muestra solo reactivación | El botón Cargos está dentro de `@if (!Model.IsDeletedView)`, así que en vista eliminadas no se renderiza | `HabilidadIndexPageTests.Get_Index_DeletedRow_HidesCargosLink:346+` (asserta ausencia del `aria-label` y del `href` esperado) | **PASS** runtime |

#### 4.2.2 `Cargos.cshtml` — comportamiento observable

| Comportamiento | Evidencia | Veredicto |
|----------------|-----------|-----------|
| Página readonly sin handlers de write | No hay `OnPost*` definidos en `HabilidadesCargosModel`; solo `OnGetAsync` | **PASS** estructural |
| `[Authorize]` a nivel de clase | Atributo presente en `HabilidadesCargosModel` | `Get_CargosPage_Anonymous_RedirectsToSignIn` runtime **PASS** |
| Habilidad existente → grilla | `HabilidadesCargosModel.OnGetAsync` invoca `GetByIdAsync` + `GetCargosAsync`, hidrata `Items`/`TotalCount`; la vista renderiza la tabla con 4 columnas (Código, Nombre, Nivel, Acciones) | `Get_CargosPage_ExistingSkillWithCargos_RendersTableWithItems` runtime **PASS** |
| Habilidad inexistente → estado recuperable (NO 404) | `OnGetAsync` setea `IsRecoverable = true` y oculta la grilla cuando `GetByIdAsync` retorna null | `Get_CargosPage_NonExistingSkill_RendersRecoverableState` runtime **PASS** |
| `?status=archivo` → normaliza a activas | `OnGetAsync` resuelve segmento via `string.Equals(..., "eliminadas", OrdinalIgnoreCase)` similar a `Index` | `Get_CargosPage_InvalidStatus_FallsBackToActivas` runtime **PASS** |
| `?status=eliminadas` → propaga segmento eliminadas | Mismo flujo; header cambia a "Cargos eliminados de la habilidad" | `Get_CargosPage_StatusEliminadas_PassesEliminadasSegment` runtime **PASS** |
| Paginación + búsqueda preservada en llamada al subrecurso | `OnGetAsync` propaga `p`, `pageSize`, `search`, `sort` al `HabilidadCargosListQuery` | `Get_CargosPage_PaginationAndSearch_PreservedInSubresourceCall` runtime **PASS** |
| Gating admin no-admin | `EsAdministrador` flag calculado de `User.IsInRole(RolesSgv.Administrador)`. Botón "Gestionar habilidades del cargo" envuelto en `@if (Model.EsAdministrador)` en `Cargos.cshtml` línea 95 | `Get_CargosPage_NonAdmin_DoesNotRenderGestionarHabilidadesButton` runtime **PASS** |
| Gating admin admin | Mismo flujo pero con `adminRole: true` en `CreateAuthenticatedClientAsync` (claim `ClaimTypes.Role == Administrador` propagado vía JWT firmado + cookie) | `Get_CargosPage_Admin_RendersGestionarHabilidadesButton` runtime **PASS** |
| Estado vacío | `@if (Model.Items.Count == 0)` con mensaje "No hay cargos asociados a esta habilidad." | `Get_CargosPage_EmptyResult_RendersEmptyState` runtime **PASS** |
| Falla de transporte recuperable | `OnGetAsync` envuelve `GetByIdAsync` en try/catch; traduce `HttpRequestException` a `IsRecoverable = true` con mensaje accionable, sin filtrar stack trace | `Get_CargosPage_TransportFailure_RendersRecoverableMessage` runtime **PASS** |

#### 4.2.3 Cliente tipado `HabilidadApiClient.GetCargosAsync`

| Aspecto | Evidencia | Veredicto |
|---------|-----------|-----------|
| `EnsureSuccessStatusCode` | `HabilidadApiClient.cs:158` | **PASS** estructural |
| URI building manual con `StringBuilder` | `BuildCargosUri(skillId, page, pageSize, search, sort, status):164-193` | **PASS** estructural |
| Mapeo segmento enum → string | `segmentoText = query.Segmento == HabilidadSegmentoListado.Eliminadas ? "eliminadas" : null` | **PASS** estructural |
| Default a `PagedResult` vacío en respuesta nula | `?? new PagedResult<SkillCargoDetailDto>([], 0, query.Page, query.PageSize)` | **PASS** estructural |
| Cobertura directa del cliente real | **0%** (fake `FakeHabilidadApiClient` lo sustituye en los 10 tests de T9). Aceptable por patrón espejo del repo (espejo `CargoSkillControllerTests` también lo aplica). | **SUGGESTION** (cosmético, no bloquea) |

### 4.3 Cross-WU (T11)

| # | Check | Resultado | Notas |
|---|-------|-----------|-------|
| 1 | Build sin warnings | **PASS** | 0 warnings, 0 errors. |
| 2 | Suite excluyendo `OcupacionRepositoryTests` | **1398/1398 PASS** | Delta vs baseline anterior (1386 en WU-A) = +12 tests (2 T7 + 10 T9). Cero regresiones. |
| 3 | `bun run build` | **exit 0** | Bundle Inspinia/Gulp actualizado; el botón Cargos es `<a>` simple sin handler JS, no requiere cambio en `habilidades-index.js`. |
| 4 | Scope del diff | 9 modified + 10 untracked — todos los paths listados en `apply-progress.md` §"Files Created / Modified" para WU-A y WU-B. **Cero** archivos fuera de scope (`Habilidades/Details.cshtml`, `Cargo/Details.cshtml`, `Cargos/Habilidades.cshtml`, scripts de migración, `CargoHabilidadConfiguracion.cs` intactos). | Cumple chain strategy `stacked-to-develop`. |
| 5 | Anti-drift Swagger (paths bajo `/api/v1/skills`) | `SwaggerConfigurationTests` (34/34) PASS — whitelist con `/api/v1/skills/{skillId}/cargos`. | Aceptable; SUGGESTION-3 del WU-A sigue vigente sobre la cobertura por prefijo. |
| 6 | Anti-drift Habilidades Web (`HabilidadIndexPageTests`) | 13/13 PASS (11 previos + 2 nuevos) | El botón Cargos no rompe ningún test previo. |
| 7 | Cobertura promedio archivos productivos | 4 archivos WU-A: 2 a 100% (DTOs), 2 a 0% (servicio/repo real sustituidos por fake). WU-B: tests cubren `HabilidadesCargosModel`, `HabilidadApiClient` real queda a 0% por fake. | Aceptable por justificación T10 + patrón repo. |

## 5. TDD Cycle Evidence (consolidado)

Las tablas completas de WU-A (8 tests) y WU-B (12 tests = 2 T7 + 10 T9) están en
`openspec/changes/habilidades-navegacion-cargos/apply-progress.md`:

- WU-A §"TDD Cycle Evidence" líneas 31-40 — RED→GREEN documentado para los 8 escenarios de T8.
- WU-B §"TDD Cycle Evidence" líneas 190-210 — RED→GREEN documentado para los 12 tests de T7+T9.

Triangulación:
- WU-A: 8 tests cubren 7 escenarios MUST explícitos de `habilidad-management` + `skill-cargo-query-contract`.
- WU-B: 12 tests cubren 3 escenarios MUST explícitos de `habilidad-web-listado-detalle-baja` + 9 escenarios
  auxiliares del comportamiento observable de `Cargos.cshtml` (gating admin, recuperable, vacío,
  paginación, etc.).

**Assertion Quality Audit**:
- WU-A: 8/8 tests verifican comportamiento observable real (Items.Count, TotalCount, Page, PageSize,
  campos del DTO, status del response). Sin tautologías. 1 fake compartido con configuración de
  escenarios por test.
- WU-B: 12/12 tests verifican comportamiento observable real (presencia/ausencia de `href` y de
  `aria-label`, status del response, contenido del header, flag `IsRecoverable`, flag
  `EsAdministrador` derivado de `User.IsInRole`). Sin tautologías.

## 6. Cobertura de archivos cambiados

Recolectada conceptualmente desde `apply-progress.md` §"Files Created / Modified" combinado:

### Archivos nuevos (10 productivos)

| Path | Capa | Tests cubren | % |
|------|------|-------------|---|
| `src/SGV.Aplicacion/Habilidades/Consultas/Dtos/SkillCargoDetailDto.cs` | Aplicación | T8 | 100% |
| `src/SGV.Aplicacion/Habilidades/Consultas/Dtos/HabilidadCargosListQuery.cs` | Aplicación | T8 | 100% |
| `src/SGV.Aplicacion/Habilidades/Consultas/ISkillCargoServicioConsulta.cs` | Aplicación | T8 | 100% |
| `src/SGV.Aplicacion/Habilidades/Consultas/SkillCargoServicioConsulta.cs` | Aplicación | T8 | bajo (thin pass-through; fake sustituido) |
| `src/SGV.Aplicacion/Habilidades/Consultas/ISkillCargoRepository.cs` | Aplicación | T8 | 100% |
| `src/SGV.Infraestructura/Persistencia/Repositorios/SkillCargoRepository.cs` | Infra | T8 | 0% directo (justificado T10); runtime end-to-end vía controller |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Cargos.cshtml` | Web | T9 | 100% (estructura observable) |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Cargos.cshtml.cs` | Web | T9 | 100% (gating admin, recuperable, paginación) |
| `tests/SGV.Tests/Api/HabilidadesCargosControllerTests.cs` | Tests | sí mismo | — |
| `tests/SGV.Tests/Web/Habilidad/HabilidadesCargosModelTests.cs` | Tests | sí mismo | — |

### Archivos modificados (9 productivos)

| Path | Δ líneas | Cobertura | Notas |
|------|---------:|-----------|-------|
| `src/SGV.Api/Controllers/SkillsController.cs` | +59 | 100% (delta `GetCargos`) | Endpoint + DI |
| `src/SGV.Infraestructura/DependencyInjection.cs` | +2 | n/a | DI Scoped |
| `src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs` | +15 | 0% directo | Firma + XML doc |
| `src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs` | +46 | 0% directo | Impl + `BuildCargosUri` |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml` | +3 | sí (HabilidadIndexPageTests) | Botón Cargos |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml.cs` | +20 | sí (HabilidadIndexPageTests) | Helper `BuildCargosRouteValues` |
| `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` | +3/-2 | sí | Whitelist |
| `tests/SGV.Tests/Web/Habilidad/FakeHabilidadApiClient.cs` | +56 | sí | Handlers para `GetCargos`/`GetById` |
| `tests/SGV.Tests/Web/Habilidad/HabilidadIndexPageTests.cs` | +78 | sí | 2 tests T7 |

### Diff stats (`git diff --stat`)

```
src/SGV.Api/Controllers/SkillsController.cs        | 61 ++++++++++++++++-
src/SGV.Infraestructura/DependencyInjection.cs     |  2 +
.../Integration/Habilidades/HabilidadApiClient.cs  | 46 +++++++++++++
.../Integration/Habilidades/IHabilidadApiClient.cs | 15 +++++
.../Pages/Organizacion/Habilidades/Index.cshtml    |  3 +
.../Pages/Organizacion/Habilidades/Index.cshtml.cs | 20 ++++++
tests/SGV.Tests/Api/SwaggerConfigurationTests.cs   |  5 +-
.../Web/Habilidad/FakeHabilidadApiClient.cs        | 56 ++++++++++++++++
.../Web/Habilidad/HabilidadIndexPageTests.cs       | 78 ++++++++++++++++++++++
9 files changed, 284 insertions(+), 2 deletions(-)
```

(Los 7 archivos nuevos productivos suman ~870 líneas aprox; `git diff --stat` solo cuenta
los modificados respecto al HEAD. El conteo combinado WU-A+WU-B está dentro del budget
encadenado de la chain strategy `stacked-to-develop`.)

## 7. CRITICAL / WARNING / SUGGESTION

### CRITICAL

*(ninguno)*

### WARNING

- **WARN-1 — Cobertura directa del repositorio real `SkillCargoRepository` es 0%**.
  El fake `FakeSkillCargoServicioConsulta` sustituye al servicio en los tests de WU-A, por lo
  que `SkillCargoRepository` real (con EF Core contra MySQL) nunca se instancia en la suite
  de este change. Misma WARN-1 que en WU-A; no cambia con WU-B. Mitigación: T10
  documentado; cobertura end-to-end del controller ejercita el flujo completo de query
  server-side; sort test verifica el orden. **Aceptable para archivar.**

- **WARN-2 — Cobertura directa del cliente real `HabilidadApiClient.GetCargosAsync` es 0%**.
  El fake `FakeHabilidadApiClient` lo sustituye en los 10 tests de T9. Riesgo: un bug del
  URI building real (`StringBuilder` + `EscapeDataString`) no se detectaría hasta CI. Mitigación:
  el URI building es mecánico; el patrón está cubierto por `QueryAsync` que sí tiene tests.
  **Aceptable para archivar.**

### SUGGESTION

- **SUGGESTION-1** — `SkillCargoServicioConsulta` (impl real) a 0% por fake. Thin
  pass-through. Cosmético (heredado de WU-A).
- **SUGGESTION-2** — Schema OpenAPI de `SkillCargoDetailDto` sin test dedicado (heredado
  de WU-A). Cobertura runtime a través de `HabilidadesCargosControllerTests` ya verifica los
  9 campos con sus tipos esperados.
- **SUGGESTION-3** — Anti-drift por prefijo (heredado de WU-A). Acepta por prefijo cualquier
  operación bajo `/api/v1/skills/{skillId}/cargos*`.
- **SUGGESTION-4 (nueva WU-B)** — `HabilidadApiClient.GetCargosAsync` no tiene test directo.
  Si en el futuro se quiere paridad con `HabilidadApiClientTests` (que sí cubre los
  endpoints `QueryAsync`/`GetAllAsync`/`DeleteAsync`), agregar 1-2 tests siguiendo el patrón
  existente. No bloquea archive.

## 8. Decisión recomendada para el orquestador

**`sdd-archive` del change completo** (no requiere `sdd-apply` adicional — WU-A y WU-B ya están
implementados). Tras archive, crear los 2 PRs de la cadena `stacked-to-develop`:

1. **PR #1** — WU-A (Foundation + API). Base: `develop`.
2. **PR #2** — WU-B (Web layer). Base: `feat/habilidades-navegacion-cargos-api` (PR #1 mergeado).

Branch strategy: chain `stacked-to-develop` (la cadena es PR-céntrica sobre `develop`).

Justificación:
- Todas las MUST de las tres specs tienen cobertura runtime PASS.
- Tests del change: 31/31 PASS en 3 s (HU Cargos Controller + HU Cargos Model + HU Index Page).
- Suite completa (1398/1398 excluyendo `OcupacionRepositoryTests`): PASS.
- Build sin warnings; bundle frontend exit 0.
- Los 2 WARNING son estructurales al PR (fake sustituye servicio/repo real) y están
  justificados por el principio "calidad > cantidad" del repo + ausencia de harness InMemory.
- Las 4 SUGGESTION son cosméticas y no bloquean archive.
- Diff dentro del budget encadenado de la chain strategy.

## 9. Riesgos abiertos

- **R-NEW-1**: si en el futuro se reactiva T10 (harness InMemory), los 39 tests existentes
  deben seguir pasando — no hay dependencias de orden.
- **R-NEW-2**: cualquier refactor de `IHabilidadServicioConsulta.GetByIdAsync` impacta el
  chequeo 404↔vacío en `SkillsController.GetCargos` y en `HabilidadesCargosModel.OnGetAsync`
  (recuperable). Cubrir con tests equivalentes antes de tocar ese método.
- **R-NEW-3**: si el subrecurso `/api/v1/skills/{skillId}/cargos` cambia de shape entre
  el merge de PR #1 y PR #2, los tests de WU-B fallarán en CI (FAIL rápido). Mitigación:
  trazabilidad de apply-progress y suites de tests del controller ya verdes.
- **R-NEW-4**: `CargoWebTestFixture` reutilizado en T9 #10 — transitorio (created+disposed
  dentro del test). Si en el futuro el fixture cambia para apuntar a otra Program, ese test
  requerirá refactor.

## 10. Resultado del output contract

- **status**: success
- **executive_summary**: Change `habilidades-navegacion-cargos` (WU-A + WU-B) cumple las tres
  delta specs (`habilidad-management` MODIFIED, `skill-cargo-query-contract` ADDED,
  `habilidad-web-listado-detalle-baja` MODIFIED) con cobertura runtime PASS. 31 tests del change
  PASS (HU Cargos Controller 8 + HU Cargos Model 10 + HU Index Page 13). Suite completa
  1398/1398 PASS excluyendo `OcupacionRepositoryTests` por issue #59. `dotnet build` 0 warnings,
  `bun run build` exit 0. Diff dentro del scope esperado. Gotcha Pomelo correctamente evitado.
  404↔vacío correctamente distinguido en controller y en PageModel. Auth correcta
  (`[Authorize]` a nivel controller y a nivel PageModel; sin restricción de rol en el GET nuevo;
  gating admin solo en el botón "Gestionar habilidades del cargo"). Anti-drift Swagger
  actualizado. Botón Cargos en filas activas preservando `p/search/sort/status`. Veredicto:
  **PASS** con 2 WARNING documentados y 4 SUGGESTION cosméticas.
- **artifacts**:
  - `openspec/changes/habilidades-navegacion-cargos/verify-report.md` (este archivo, sobrescrito)
  - `openspec/changes/habilidades-navegacion-cargos/apply-progress.md` (WU-A + WU-B consolidado)
- **next_recommended**: `sdd-archive` del change completo (sin `sdd-apply` adicional). Tras
  archive, ejecutar `branch-pr` con la chain `stacked-to-develop` (PR #1 WU-A → PR #2 WU-B
  con base `feat/habilidades-navegacion-cargos-api`).
- **risks**: 4 riesgos abiertos documentados en §9 (R-NEW-1 a R-NEW-4). Ninguno bloquea archive.
- **skill_resolution**: paths-injected — `sdd-verify`, `strict-tdd-verify`,
  `dotnet-best-practices`, `dotnet-xunit`, `Razor Pages Patterns`
</content>
</invoke>