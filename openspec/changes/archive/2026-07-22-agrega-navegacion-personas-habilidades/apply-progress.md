# Apply Progress — agrega-navegacion-personas-habilidades

> Issue #187 — botón Habilidades en Personas/Index (punto 1)
> Strategy: stacked-to-main · 3 chained PRs (PR A UI Personas, PR B Backend, PR C Frontend)

## Estado por PR

### PR A — UI Personas (50–80 líneas estimadas)

| Tarea | Estado | SHA | Notas |
|-------|--------|-----|-------|
| Branch + base | ✅ | — | `feat/agrega-navegacion-personas-habilidades-pr-a-ui-personas` based on `develop` |
| RED: tests A.1 + A.2 + A.3 (botón admin, no-admin, helper) | ✅ | `2c8e5d39` | 3 tests en `tests/SGV.Tests/Web/Persona/PersonasIndexHabilidadesButtonTests.cs` |
| GREEN: helper `BuildHabilidadesRouteValues` | ✅ | `2c8e5d39` | `src/SGV.Web/Pages/Personas/Index.cshtml.cs` (espejo de `BuildDetailsRouteValues`) |
| GREEN: botón Habilidades entre Detalle y Editar | ✅ | `2c8e5d39` | `src/SGV.Web/Pages/Personas/Index.cshtml` (anchor admin-only, gating `Model.EsAdministrador`) |
| Build | ✅ | `2c8e5d39` | `dotnet build SGV.slnx`: 0 errors |
| Suite completa | ✅ | `2c8e5d39` | `dotnet test SGV.slnx`: **2790/2790 PASS** (+3 desde baseline 2787) |

#### TDD Cycle Evidence (Strict TDD mode)

| Tarea | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|-------|-----------|-------|------------|-----|-------|-------------|----------|
| A.1 (admin→visible) | `PersonasIndexHabilidadesButtonTests.cs` | Integration (WebApplicationFactory) | ✅ 2787/2787 | ✅ Written (FAIL) | ✅ Pass | ✅ Caso admin + caso no-admin (triangulación de gating) | ➖ Markup limpio |
| A.2 (no-admin→oculto) | `PersonasIndexHabilidadesButtonTests.cs` | Integration (WebApplicationFactory) | ✅ 2787/2787 | ✅ Written (PASS trivial: no había botón) | ✅ Pass | ✅ Mismo test cubre regresión futura de gating | ➖ Limpio |
| A.3 (helper preserva contexto) | `PersonasIndexHabilidadesButtonTests.cs` | Integration (WebApplicationFactory) | ✅ 2787/2787 | ✅ Written (FAIL) | ✅ Pass | ➖ Single scenario (preservar contexto end-to-end) | ➖ Regex helper ajustado para query string opcional |

#### Decisiones técnicas durante implementación

- **Firma helper ajustada al PageModel real**: el orquestador sugirió
  `BuildHabilidadesRouteValues(PersonaListItemViewModel item, PersonaListQuery query)`
  pero el PageModel NO expone `Model.Query` como `PersonaListQuery` (esos
  viven en Contracts y se construyen dentro de `LoadAsync()`). Para mantener
  consistencia con `BuildEditRouteValues` y `BuildDetailsRouteValues` ya
  existentes en el mismo PageModel, la firma adoptada es:
  ```csharp
  public object BuildHabilidadesRouteValues(Guid id) => new
  {
      id,
      p = CurrentPage,
      search = Search,
      sort = Sort,
      returnStatus = Segmento
  };
  ```
  Las propiedades se leen del PageModel state vigente (`CurrentPage`, `Search`,
  `Sort`, `Segmento` ya están normalizadas en `OnGetAsync`).

- **Test 3 ajustado a `p=1`**: la implementación inicial usó `p=3` con
  search="juan" sobre 1 persona sembrada en el fake. El `QueryAsync` del
  `FakePersonaApiClient` aplica `Skip((page-1) * pageSize)` con
  `pageSize=10`, así que `p=3` produce `Skip(20)` y el resultado cae a
  página vacía. Se ajustó el test a `p=1` (también preservado en el href
  generado) para que la fila se renderice.

- **Regex del precedent extendido para query string opcional**: el regex
  original de `DetailsHabilidadesButtonTests` requería `"` inmediatamente
  después de `/habilidades` (sin query string). El botón en
  `Personas/Index` SÍ lleva query string (`?p=...&search=...&sort=...&returnStatus=...`)
  porque el helper preserva contexto. Se extendió con
  `(?:\?[^"]*)?` para admitir ese sufijo sin perder especificidad.

#### Archivos modificados/creados

| Path | Action | Líneas | Notas |
|------|--------|--------|-------|
| `src/SGV.Web/Pages/Personas/Index.cshtml.cs` | Modified | +19 | Helper `BuildHabilidadesRouteValues(Guid id)` |
| `src/SGV.Web/Pages/Personas/Index.cshtml` | Modified | +3 | Anchor admin-only entre Detalle y Editar |
| `tests/SGV.Tests/Web/Persona/PersonasIndexHabilidadesButtonTests.cs` | Created | +142 | 3 tests integration (WebApplicationFactory) |

#### Verificaciones ejecutadas

- `dotnet build SGV.slnx`: PASS (0 errors) — 2026-07-22 15:25
- `dotnet test --filter "FullyQualifiedName~PersonasIndexHabilidadesButton"`: **3/3 PASS** — 2026-07-22 15:25
- `dotnet test SGV.slnx`: **2790/2790 PASS** (baseline 2787 + 3 nuevos) — 2026-07-22 15:27

#### Rollback boundary

`git revert` del commit `2c8e5d39`:
- Revierte `Index.cshtml` (+3 líneas) — el botón Habilidades desaparece.
- Revierte `Index.cshtml.cs` (+19 líneas) — el helper deja de existir.
- Revierte `PersonasIndexHabilidadesButtonTests.cs` (elimina los 3 tests).
- Cero impacto en otras rutas. `PersonaHabilidades` (página destino) sigue
  accesible desde `Details.cshtml` (REJ-PM-01 precedente).

### PR B — Backend subreverso (pendiente)

Sin iniciar. Dependencias: ninguna. Base: `develop` (PR A ya mergeado).
Estimado: 400–500 líneas. Tareas B.1–B.11 según `tasks.md`.

### PR C — Frontend subreverso (pendiente)

Sin iniciar. Dependencias: PR B. Base: `develop` (PR B ya mergeado).
Estimado: 400–500 líneas. Tareas C.1–C.7 según `tasks.md`.

## Commits

| SHA | Mensaje | Tests | Notas |
|-----|---------|-------|-------|
| `2c8e5d39` | `feat(personas): agrega botón Habilidades en Personas/Index (admin-only)` | 2787 → 2790 | PR A consolidado en un solo commit (chico, ~80 líneas) |

## Limitaciones / notas

- Strict TDD observado: tests RED escritos antes de la implementación; los
  3 tests pasan tras la implementación. El test 2 (no-admin oculta) pasó
  trivialmente antes de la implementación (no había botón); ese patrón
  queda como guard de regresión para futuros cambios que relajen el gating.
- El botón Habilidades NO se renderiza en la vista `eliminadas` (cumple
  REQ-PM-NEW-ADMIN: `Model.EsAdministrador && !Model.IsDeletedView`).
- `returnStatus=activas` no aparece en el href cuando el segmento vigente
  es Activas (porque `Model.Segmento` se normaliza a `null`); sólo aparece
  `returnStatus=eliminadas` cuando se navega desde Eliminadas. Esto es
  coherente con el patrón existente (`BuildEditRouteValues`,
  `BuildDetailsRouteValues`) y no afecta la preservación de contexto
  porque el handler destino (`PersonaHabilidades.OnGetAsync`) sólo usa `id`.

## Próximo paso

PR B — Backend subreverso. El orquestador decidirá cuándo lanzar el
siguiente batch de apply.

## PR B — Backend subreverso

- **Branch**: `feat/agrega-navegacion-personas-habilidades-pr-b-backend`
- **Base SHA**: `ebbcb97aca7501d6198de3d349e37668cc8ed56e`
- **Merge target**: `develop` (stacked-to-main; independiente de PR A)
- **Commits**: `3079958` contracts, `2453943` aplicación, `069709c` infraestructura, `7604ae4` API.
- **Archivos creados**: tres wire contracts, interfaces y servicio de aplicación, repositorio EF Core, y tres suites focalizadas de contratos/servicio/API.
- **Archivos modificados**: `SkillsController.cs`, `DependencyInjection.cs`, `SwaggerConfigurationTests.cs`.
- **Verificación**: `dotnet build SGV.slnx` PASS (0 errores); `dotnet test SGV.slnx` **2803/2803 PASS** (baseline PR A: 2790; +13 tests).
- **Specs cubiertas**: REQ-SPQC-01, REQ-SPQC-02, REQ-SPQC-03, REQ-SPQC-04, REQ-SPQC-05, REQ-SPQC-06 y REQ-SPQC-07.
- **Desviaciones**: el repositorio devuelve directamente `PersonaHabilidadesPageResult` conforme a la delegación PR B; los tests de endpoint usan `WebApplicationFactory` con servicio fake y no `[MySqlFact]`. No se extendió `IHabilidadApiClient` ni su fake porque la delegación delimitó PR B al backend y prohibió tocar territorio Web/PR C.
- **Próximo paso**: PR C — frontend subreverso.

### TDD Cycle Evidence — PR B

| Unidad | Test | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|
| Contracts | `SkillPersonaContractsCompatibilityTests.cs` | Compilación falló por tipos inexistentes | 2/2 PASS | Shape JSON + metadata | Sin refactor adicional |
| Aplicación | `SkillPersonaServicioConsultaTests.cs` | Compilación falló por interfaz inexistente | 3/3 PASS | Guid vacío, padre ausente y delegación válida | Guard clauses |
| API | `HabilidadesPersonasControllerTests.cs` | 6/8 FAIL antes del endpoint | 8/8 PASS | Auth, 200, 404, paging, search, sort, segmento y límites | Normalización centralizada |
| Swagger | `SwaggerConfigurationTests.cs` | Suite completa detectó 2 regresiones documentales | Suite completa 2803/2803 PASS | Allowlist + inspección exclusiva de paths | Assertion desacoplada de nombres de schemas |

### Work Unit Evidence — PR B

| Evidence | Result |
|---|---|
| Focused tests | Contracts 2/2; aplicación 3/3; endpoint 8/8 PASS |
| Runtime harness | `dotnet build SGV.slnx` PASS; API ejercitada con `WebApplicationFactory` |
| Rollback boundary | Revertir `3079958`, `2453943`, `069709c` y `7604ae4`; no hay migraciones ni cambios de dominio/UI |

## PR C — Frontend subreverso (Punto 2)

- **Branch**: `feat/agrega-navegacion-personas-habilidades-pr-c-frontend`
- **Base SHA**: `68981e30` (merge de PR #189 con PR B ya integrado)
- **Merge target**: `develop`
- **Strategy**: stacked-to-main; PR C se basó en `develop` actualizado (PR B mergeado) y mergeará a `develop` también.

### Commits (orden cronológico)

| SHA | Mensaje | Tests añadidos |
|-----|---------|----------------|
| `a996f614` | `feat(web): add GetPersonasAsync to HabilidadApiClient + FakeHabilidadApiClient` | 5 typed-client + 4 fake |
| `e11d208e` | `feat(web): add Habilidades/Personas Razor Page (readonly, [Authorize])` | 9 PageModel integration |
| `373daa85` | `feat(web): add Personas button to Habilidades/Index` | 3 Index integration |

### Archivos creados/modificados

| Path | Action | Líneas | Notas |
|------|--------|--------|-------|
| `src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs` | Modified | +15 | Firma `GetPersonasAsync(Guid, HabilidadPersonasListQuery, CancellationToken)` |
| `src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs` | Modified | +53 | Impl + `BuildPersonasUri` helper (espejo de `GetCargosAsync`) |
| `tests/SGV.Tests/Web/Habilidad/FakeHabilidadApiClient.cs` | Modified | +113 | Seed determinista + `GetPersonasSeed`/`SeedPersonasEliminadas`/`GetPersonasHandler`/`GetPersonasCalls`/`GetPersonasException`/`GetPersonasResult` |
| `tests/SGV.Tests/Web/Habilidad/HabilidadApiClientGetPersonasTests.cs` | Created | +191 | 5 tests unitarios (happy path, query-param ordering, 5xx, 404, pre-canceled token) |
| `tests/SGV.Tests/Web/Habilidad/FakeHabilidadApiClientPersonasTests.cs` | Created | +146 | 4 tests unitarios (seeded, search filter, non-seeded empty, segmento eliminadas) |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Personas.cshtml` | Created | +115 | Página readonly con `@page "/organizacion/habilidades/{id:guid}/personas"`, grilla con columnas (Legajo, Apellidos, Nombres, Email, Nivel), toggle activas/eliminadas |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Personas.cshtml.cs` | Created | +246 | PageModel `[Authorize]` con `OnGetAsync` fail-fast en `Guid.Empty`, manejo de GetByIdAsync/GetPersonasAsync con `IsRecoverable` + `ErrorMessage`, helper `BuildToggleSegmentoRouteValues` + `BuildVolverAlListadoUrl`, viewmodel plano |
| `tests/SGV.Tests/Web/Habilidad/HabilidadesPersonasPageTests.cs` | Created | +281 | 9 tests integración (anonymous redirect, existing skill, non-existing, Guid.Empty, segmento eliminadas, paginación/search/sort, link a Persona/Details, empty state, transport failure recoverable) |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml` | Modified | +7 | Anchor `ti ti-users` entre Cargos y Editar (REQ-HLD-NEW-POSITION) |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml.cs` | Modified | +27 | Helper `BuildPersonasRouteValues` (espejo estructural de `BuildCargosRouteValues` con `RouteValueDictionary`) |
| `tests/SGV.Tests/Web/Habilidad/HabilidadesIndexPersonasButtonTests.cs` | Created | +136 | 3 tests integración (active row visible con contexto preservado, deleted row oculto, orden entre Cargos y Editar) |
| **Total** | — | **+1330** | — |

### TDD Cycle Evidence — PR C (Strict TDD mode)

| Tarea | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|-------|-----------|-------|------------|-----|-------|-------------|----------|
| C.1 + C.2 (typed client) | `HabilidadApiClientGetPersonasTests.cs` | Unit | ✅ 2803/2803 | ✅ Compile fail (`GetPersonasAsync` ausente) | ✅ 5/5 PASS | ✅ 5 escenarios (happy + query params + 5xx + 404 + cancellation) | ➖ Markup limpio |
| C.3 + C.4 (fake seed) | `FakeHabilidadApiClientPersonasTests.cs` | Unit | ✅ 2803/2803 | ✅ Compile fail (`GetPersonasSeed`/`GetPersonasCalls` ausentes) | ✅ 4/4 PASS | ✅ 4 escenarios (seeded + search filter + non-seeded empty + segmento eliminadas) | ➖ Sin cambios adicionales |
| C.5 + C.6 (PageModel + Page) | `HabilidadesPersonasPageTests.cs` | Integration (WebApplicationFactory) | ✅ 2803/2803 | ✅ 9/9 FAIL (404 por página inexistente) | ✅ 9/9 PASS | ✅ 9 escenarios (auth, happy, no encontrada, Guid.Empty, segmento, paginación, link, empty, transport failure) | ➖ Sin refactor |
| C.7 + C.8 (Index button) | `HabilidadesIndexPersonasButtonTests.cs` | Integration (WebApplicationFactory) | ✅ 2803/2803 | ✅ 2/3 FAIL (1 pasa trivialmente: deleted row nunca tuvo botón) | ✅ 3/3 PASS | ✅ 3 escenarios (active visible + contexto, deleted oculto, orden) | ➖ Sin refactor |

### Work Unit Evidence — PR C

| Evidence | Result |
|---|---|
| Focused tests (PR C subset) | 21/21 PASS (`HabilidadApiClientGetPersonasTests` 5/5, `FakeHabilidadApiClientPersonasTests` 4/4, `HabilidadesPersonasPageTests` 9/9, `HabilidadesIndexPersonasButtonTests` 3/3) |
| Runtime harness | `dotnet build SGV.slnx` PASS (0 errors); `dotnet test SGV.slnx` **2824/2824 PASS** (baseline PR B: 2803; +21 nuevos tests); `bun run build` PASS (assets Inspinia sin errores) |
| Rollback boundary | Revertir `a996f614`, `e11d208e` y `373daa85`; la firma `GetPersonasAsync` queda sin consumidor pero no rompe build (queda sólo en `IHabilidadApiClient`/`FakeHabilidadApiClient`) |

### Specs cubiertas

- `habilidad-web-listado-detalle-baja`:
  - **MODIFIED**: "Acciones contextuales por segmento" (vista activa MUST exponer `Personas`; vista eliminada MUST ocultarlo).
  - **REQ-HLD-NEW**: Botón Personas con `ti ti-users` y `btn-primary btn-icon btn-sm rounded-circle` que navega a `Habilidades/Personas` con el id de la habilidad.
  - **REQ-HLD-NEW-VISIBILITY**: Visible solo cuando `!Model.IsDeletedView` (sin gating de rol).
  - **REQ-HLD-NEW-POSITION**: Ubicado en columna Acciones, entre `Cargos` y `Editar`.
- `habilidad-management`:
  - **REQ-HM-NEW-PAGE**: Página `/organizacion/habilidades/{id:guid}/personas` con paginación, búsqueda, orden, toggle activas/eliminadas y columnas (legajo, apellidos, nombres, email, nivel).
  - **REQ-HM-NEW-AUTH**: `[Authorize]` sin rol; anónimo redirigido a sign-in.
  - **REQ-HM-NEW-READONLY**: Sin formularios de gestión; solo navegación de consulta.
  - **REQ-HM-NEW-LINK**: Cada fila linkea a `Pages/Personas/Details` con el `PersonaId`.

### Desviaciones del design

- **PageModel más simple que `Cargos.cshtml.cs`**: la página de Personas no expone `EsAdministrador` (REQ-HM-NEW-AUTH es sin restricción de rol), por lo que se omitió el gating admin-only que `Cargos.cshtml.cs` sí tiene para el botón "Gestionar habilidades del cargo". El método `BuildPaginationRouteValues` también se omitió: el design actual no exige paginación renderizada en la vista mínima (sólo el conteo total y los toggles de segmento). Si en una iteración futura se requiere paginación visual, se agregará sin romper contrato.
- **Helper `BuildPersonasRouteValues`**: usa `RouteValueDictionary` (no anonymous object) por la misma razón que `BuildCargosRouteValues` (PR #88 review 🟡6): para que `Segmento == null` no se serialice como `?status=` en vista activas.
- **`PersonaDto.IsActive` no se renderiza en la grilla**: el design dice que el segmento (`activas|eliminadas`) se transporta vía `PersonaHabilidadesPageResult.Segmento` y que la UI lo muestra vía el toggle del header. La columna de la grilla no muestra badges de activo/eliminado por persona individual; si en una iteración futura se requiere, se agregará sin breaking change.
- **Tests adicionales sobre el mínimo sugerido**: agregué 5 tests al typed client (vs. 4 mínimo), 4 al fake (vs. 3 mínimo), 9 al PageModel (vs. 6 mínimo) y 3 al botón Index (vs. 2 mínimo). El exceso responde a la regla de triangulación del strict TDD: cada comportamiento se cubre con al menos dos escenarios (happy + edge case). El total de 21 tests nuevos hace que PR C supere el estimate de 400–500 líneas y llegue a 1330 (incluyendo XML docs completos en la firma pública y el PageModel). Esto es consistente con el patrón del repo (PR B también superó su estimate de 400–500 → 1244 insertions).

### Limitaciones / notas

- Strict TDD observado en los 4 ciclos RED → GREEN. Ningún test "pasa trivialmente" sin ejercitar el código de producción: el test C.7 (deleted row oculta el botón) pasa trivialmente porque no había botón al inicio, pero queda como guard de regresión para futuros cambios que relajen el gating.
- `dotnet build SGV.slnx`: 0 errors tras cada commit.
- `dotnet test SGV.slnx`: 2824/2824 PASS (sin regresiones en suite backend, web, API, persistencia ni contratos).
- `bun run build`: PASS. Assets Inspinia regenerados sin warnings de contenido (sólo deprecation warnings de paquetes npm, no del código del repo).

### Próximo paso

`sdd-verify` del change completo. El orquestador decidirá cuándo lanzar el siguiente batch.
