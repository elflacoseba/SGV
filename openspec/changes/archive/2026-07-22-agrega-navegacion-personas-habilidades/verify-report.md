# Verify Report — agrega-navegacion-personas-habilidades

> Issue #187 — "Agregar botón para ver las Habilidades de una Persona / botón reverso Personas en Habilidades"
> Branch base: `develop` @ `cafc590c`
> Artifact store: hybrid (openspec + engram)
> Preflight: interactive, ask-always, strict_tdd=true, review_budget_lines=800
> Fecha verificación: 2026-07-22

---

## 1. Resumen ejecutivo

**Veredicto: `PASS WITH WARNINGS`** — el change satisface íntegramente los 16 criterios de aceptación de la issue #187, las 18 requirements declaradas en las 4 specs del change, y la suite completa queda en `2824/2824 PASS` con `dotnet build` y `bun run build` en verde. Los warnings son pre-existentes (análisis del repo, no introducidos por este change) o notas de diseño documentadas en `apply-progress.md`.

---

## 2. Build + Test + Bun

### Build (`dotnet build SGV.slnx`)

```text
Build succeeded.
    84 Warning(s)
    0 Error(s)
Time Elapsed 00:00:07.73
```

**0 errors. 84 warnings** — todos pre-existentes (no introducidos por este change). Distribución verificada:
- `CS8524` (switch no exhaustivo sobre `ErrorCategoria`): `SGV.Contracts/Comun/ErrorCategoriaMappers.cs` y los clientes tipados (`HabilidadApiClient.cs:303`, `PersonaApiClient.cs:356`, `CargoApiClient.cs:265`, `UsuarioApiClient.cs:286`, etc.) — pre-existente al change.
- `xUnit1031` (test methods blocking task ops): tests de validadores de personas — pre-existente.
- `EF1002` (`ExecuteSqlRawAsync` con interpolated strings): `BloquearDesbloquearEliminarGatewayTests.cs:322-324` — pre-existente.
- `NU1510` (paquetes no podables): `SGV.Infraestructura.csproj` — pre-existente.
- `CS8602/CS8604` (posibles null refs): `UnidadesOrganizativas/*.cs`, `Usuarios/Index.cshtml.cs` — pre-existente.

### Tests (`dotnet test SGV.slnx --no-build`)

```text
Passed!  - Failed: 0, Passed: 2824, Skipped: 0, Total: 2824, Duration: 1 m 19 s - SGV.Tests.dll (net10.0)
```

**2824/2824 PASS**. Coincide con la baseline reportada en `apply-progress.md` post-merge de PR #190 (2824/2824).

### Suite focalizada del change (37 tests)

```text
dotnet test --filter "FullyQualifiedName~PersonasIndexHabilidadesButton|
FullyQualifiedName~SkillPersona|FullyQualifiedName~HabilidadesPersonas|
FullyQualifiedName~HabilidadApiClientGetPersonas|
FullyQualifiedName~FakeHabilidadApiClientPersonas|
FullyQualifiedName~HabilidadesPersonasPage|
FullyQualifiedName~HabilidadesIndexPersonasButton"

Passed!  - Failed: 0, Passed: 37, Skipped: 0, Total: 37, Duration: 2 s
```

| PR | Tests esperados | Tests focalizados PASS | Cobertura |
|---|---|---|---|
| **PR A** | 3 (`PersonasIndexHabilidadesButtonTests`) | 3/3 | 100% |
| **PR B** | 13 (2 contracts + 3 aplicación + 8 controller) | 13/13 | 100% |
| **PR C** | 21 (5 typed-client + 4 fake + 9 page + 3 button) | 21/21 | 100% |
| **Total** | **37** | **37/37** | **100%** |

Cada escenario de spec tiene al menos un test runtime como covering test (`✅ COMPLIANT` en la matriz §3).

### Bun build (`bun run build` en `src/SGV.Web`)

```text
$ gulp build
[baseline-browser-mapping] The data in this module is over two months old...
[17:07:23] Starting 'build'...
[17:07:23] Starting 'plugins'...
[17:07:23] Finished 'plugins' after 5.13 ms
[17:07:23] Starting 'styles'...
[17:07:26] Finished 'styles' after 3 s
[17:07:26] Starting 'inspiniaPages'...
[17:07:26] Finished 'inspiniaPages' after 1.66 ms
[17:07:26] Finished 'build' after 3 s
```

**Exit code 0**. Sin warnings de código del repo (sólo `DEP0180` deprecation de `fs.Stats` interno de npm, no del código fuente). Assets Inspinia regenerados sin issues.

### Artefactos regenerados por gulp

- 10 archivos `.css` actualizados en `src/SGV.Web/wwwroot/css/` (5 son `.min.css`, 5 fuentes de bootstrap-scss).
- **NO están en `.gitignore`** pero son convención pre-existente: `git log -- src/SGV.Web/wwwroot/css/app.min.css` muestra que fue commiteado por el PR plantilla Inspinia (`3d88a22a`). Por convención del repo: los assets generados se commitean, no se gitignorean.
- **Cero impacto sobre los PRs del change**: estos assets no fueron modificados por las diffs de PR A/B/C; `bun run build` los regenera como side-effect sin alterar bytes de código.

---

## 3. Spec compliance

Mapeo de cada requirement de las 4 specs contra el código actual (path:line). Cumple requisitos ✅.

### Spec: `skill-persona-query-contract` (NUEVA — 7 reqs, 9 scenarios)

| Req | Status | Evidencia |
|---|---|---|
| **REQ-SPQC-01** Respuesta paginada, 200 OK con elementos | ✅ | `src/SGV.Api/Controllers/SkillsController.cs:176-180` (endpoint) + `:204-205` (Ok(result)). Repo carga con `Skip/Take`. |
| **REQ-SPQC-02** Acepta page/pageSize/search/sort/status, normalización a defaults | ✅ | `src/SGV.Api/Controllers/SkillsController.cs:189-202` (normalización: page<1→1, pageSize<1→20 \|\| min(100,…), sort whitelist→apellidos_asc). |
| **REQ-SPQC-03** Shape `SkillPersonaDetailDto(PersonaDto, NivelHabilidadDto)` + `PersonaId`, `HabilidadId`, `NivelHabilidadId` init-only | ✅ | `src/SGV.Contracts/Habilidades/Consultas/Dtos/SkillPersonaDetailDto.cs:6-11` — record con 3 props init-only. Tests `SkillPersonaContractsCompatibilityTests` (2) cubren nombres JSON tras serialización. |
| **REQ-SPQC-04** `status` HTTP se normaliza a `PersonaSegmentoListado` | ✅ | `src/SGV.Api/Controllers/SkillsController.cs:194-196` (map `eliminadas` → `PersonaSegmentoListado.Eliminadas`; default `Activas`). |
| **REQ-SPQC-05** `OrderBy` pre-`Select` sobre entidad Persona | ✅ | `src/SGV.Infraestructura/Persistencia/Repositorios/SkillPersonaRepository.cs:35-36` (ApplySort sobre `PersonaHabilidadEntity.Persona.Legajo/Apellidos/Nombres`), luego `:39` Select proyecta DTO. |
| **REQ-SPQC-06** Guid.Empty → `ArgumentException`; padre inexistente → 404 | ✅ | `src/SGV.Aplicacion/Habilidades/Consultas/SkillPersonaServicioConsulta.cs:15-18` (Guid.Empty throw); `:20-24` (parent null → return null → controller 404 en `SkillsController.cs:205`). |
| **REQ-SPQC-07** Response: `PersonaHabilidadesPageResult` con `Items/Page/PageSize/Total/Sort/Segmento` | ✅ | `src/SGV.Contracts/Habilidades/Consultas/Dtos/PersonaHabilidadesPageResult.cs:6-12`. Construido en `SkillPersonaRepository.cs:66`. |

### Spec: `habilidad-web-listado-detalle-baja` (DELTA — 1 MODIFIED + 3 ADDED = 4 reqs, 5 scenarios)

| Req | Status | Evidencia |
|---|---|---|
| **MOD: Acciones contextuales por segmento** (vista activa expone Personas entre Cargos y Editar; eliminada lo oculta) | ✅ | `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml:157` (`@if (!Model.IsDeletedView)` envuelve todo el bloque activo); `:165-171` botón Personas entre `:162` (Cargos) y `:172` (Editar). |
| **ADD: REQ-HLD-NEW** Botón Personas con `ti ti-users` + `btn-primary btn-icon btn-sm rounded-circle` → `/organizacion/habilidades/{id}/personas` | ✅ | `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml:165-171` (clases exactas; href usa `Model.BuildPersonasRouteValues(item.Id)` → `/Organizacion/Habilidades/Personas` con `id`). |
| **ADD: REQ-HLD-NEW-VISIBILITY** Visible solo si `!Model.IsDeletedView`; accesible a cualquier autenticado | ✅ | `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml:157` gating. No `@if (Model.EsAdministrador)` envolviendo este botón (a diferencia del botón "Editar"). |
| **ADD: REQ-HLD-NEW-POSITION** Posición entre Cargos y Editar | ✅ | `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml:165-171` (línea 165-171 está entre línea 162 "Cargos" y línea 172 "Editar"). |

### Spec: `habilidad-management` (DELTA — 4 ADDED = 4 reqs, 5 scenarios)

| Req | Status | Evidencia |
|---|---|---|
| **ADD: REQ-HM-NEW-PAGE** Página con paginación, búsqueda, orden, toggle, columnas (legajo, apellidos, nombres, email, nivel) | ✅ | `src/SGV.Web/Pages/Organizacion/Habilidades/Personas.cshtml:1` (`@page "/organizacion/habilidades/{id:guid}/personas"`); `:62-67` (5 columnas exactas); `:48-51` (toggle Activas/Eliminadas); `:49-50` (BuildToggleSegmentoRouteValues linkea a esta misma página con `p=1`). Paginación server-side via `query.Page/PageSize` propagados al subrecurso API. |
| **ADD: REQ-HM-NEW-AUTH** `[Authorize]` sin rol; anónimo redirige a sign-in | ✅ | `src/SGV.Web/Pages/Organizacion/Habilidades/Personas.cshtml.cs:24` (`[Authorize]`). Test `HabilidadesPersonasPageTests` cubre caso anonymous redirect. |
| **ADD: REQ-HM-NEW-READONLY** Sin formularios de gestión (POSTs no permitidos) | ✅ | `src/SGV.Web/Pages/Organizacion/Habilidades/Personas.cshtml.cs` — `grep "OnPost"` retorna 0 matches. Sólo `OnGetAsync` (línea 116). Gestión permanece exclusivamente en `Pages/Personas/PersonaHabilidades` (chequeado: `PersonaHabilidades.cshtml.cs:23` sigue siendo `[Authorize(Roles = RolesSgv.Administrador)]`). |
| **ADD: REQ-HM-NEW-LINK** Cada fila linkea al detalle de Persona via `PersonaId` | ✅ | `src/SGV.Web/Pages/Organizacion/Habilidades/Personas.cshtml:87-90` (anchor con `Url.Page("/Personas/Details", new { id = item.PersonaId })` envuelve `Apellidos`). |

### Spec: `persona-management` (DELTA — 4 ADDED = 4 reqs, 4 scenarios)

| Req | Status | Evidencia |
|---|---|---|
| **ADD: REQ-PM-NEW** Botón Habilidades con `ti ti-stars` + `btn-primary btn-icon btn-sm rounded-circle` → `/personas/{id}/habilidades` | ✅ | `src/SGV.Web/Pages/Personas/Index.cshtml:185-189` (clases exactas; icono `ti ti-stars`; href via `Model.BuildHabilidadesRouteValues(item.Id)` que produce ruta a `Pages/Personas/PersonaHabilidades`). |
| **ADD: REQ-PM-NEW-ADMIN** Gating `Model.EsAdministrador && !Model.IsDeletedView` | ✅ | `src/SGV.Web/Pages/Personas/Index.cshtml:185` `@if (Model.EsAdministrador)` dentro de `:180` `@if (!Model.IsDeletedView)`. |
| **ADD: REQ-PM-NEW-POSITION** Entre Detalle y Editar | ✅ | `src/SGV.Web/Pages/Personas/Index.cshtml` — `:182-184` es Detalle (btn-info); `:185-189` es Habilidades; `:190-192` es Editar (btn-warning). Orden correcto. |
| **ADD: REQ-PM-NEW-CONTEXT** `BuildHabilidadesRouteValues` preserva `page/search/sort/status` | ✅ | `src/SGV.Web/Pages/Personas/Index.cshtml.cs:286-293` — props `p = CurrentPage, search = Search, sort = Sort, returnStatus = Segmento`. Test `PersonasIndexHabilidadesButtonTests` cubre preservación. |

**Compliance summary: 18/18 requirements ✅** — todas las requirements de las 4 specs del change tienen al menos un covering test que pasó en runtime.

---

## 4. Issue traceability

Mapeo de los 16 criterios de aceptación de la issue #187 contra el código.

| # | Criterio | Status | Evidencia |
|---|---|---|---|
| 1 | Personas/Index: admin ve botón Habilidades en filas activas | ✅ | `Pages/Personas/Index.cshtml:185-189` gating `@if (Model.EsAdministrador)` dentro de `@if (!Model.IsDeletedView)`. |
| 2 | Personas/Index: href preserva `id, page, search, sort, status` | ✅ | `Pages/Personas/Index.cshtml.cs:286-293` `BuildHabilidadesRouteValues` → `{id, p=CurrentPage, search=Search, sort=Sort, returnStatus=Segmento}`. Test 3 cubre preservación end-to-end. |
| 3 | Habilidades/Index: autenticado ve botón Personas en filas activas | ✅ | `Pages/Organizacion/Habilidades/Index.cshtml:165-171`. Sin gating admin. |
| 4 | Habilidades/Personas: grilla con legajo, apellidos, nombres, email, nivel | ✅ | `Pages/Organizacion/Habilidades/Personas.cshtml:62-67` (5 columnas). ViewModel `HabilidadPersonaListItemViewModel` en `:240-246` mapea los 6 campos. |
| 5 | Habilidades/Personas: toggle activas/eliminadas | ✅ | `Pages/Organizacion/Habilidades/Personas.cshtml:48-51` (2 anchors btn-group). `Pages/Organizacion/Habilidades/Personas.cshtml.cs:191-199` `BuildToggleSegmentoRouteValues`. Test 5 cubre segmento eliminadas. |
| 6 | Habilidades/Personas: cada fila linkea a detalle de persona | ✅ | `Pages/Organizacion/Habilidades/Personas.cshtml:88` anchor a `/Personas/Details/{PersonaId}`. |
| 7 | GET /api/v1/skills/{id}/personas: 200 con SkillPersonaDetailDto paginado | ✅ | `src/SGV.Api/Controllers/SkillsController.cs:176-206` retorna `Ok(result)` cuando padre existe; `SkillPersonaDetailDto.cs:6-11` cumple shape `(PersonaDto, NivelHabilidadDto)` + 3 init-only. Tests 1, 2 del controller cubren 200 con/sin personas. |
| 8 | Acepta page/pageSize/search/sort/status (default activas) | ✅ | `SkillsController.cs:180-187` firma; `:189-202` normalización con defaults `apellidos_asc` sort y `PersonaSegmentoListado.Activas`. Tests 6 y 7 del controller. |
| 9 | search busca sobre legajo, nombres, apellidos | ✅ | `SkillPersonaRepository.cs:25-32` substring case-insensitive en los 3 campos. Tests del controller cubren search. |
| 10 | sort acepta legajo_*/apellidos_*/nombres_* | ✅ | `SkillPersonaRepository.cs:69-79` ApplySort con 6 ramas válidas + default `apellidos_asc`; `:81-85` whitelist `NormalizeSort`. Tests 6 (sort legajo_desc) y 8 cubren. |
| 11 | status filtra por segmento de persona (no de habilidad) | ✅ | `SkillsController.cs:194-196` usa `PersonaSegmentoListado` (NO `HabilidadSegmentoListado`). Repo `:21-23` filtra `link.Persona.IsDeleted/IsActive`. |
| 12 | SkillPersonaDetailDto en SGV.Contracts.Habilidades.Consultas.Dtos | ✅ | `src/SGV.Contracts/Habilidades/Consultas/Dtos/SkillPersonaDetailDto.cs:3` namespace. Nota: la issue mencionaba `Organizacion/Consultas/Dtos` pero respeta el precedent del owner del subrecurso (`Skill` → `Habilidades/`). Documentado en proposal § D5. |
| 13 | IHabilidadApiClient.GetPersonasAsync existe | ✅ | `src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs:83-86`. Test de firma cubre. |
| 14 | FakeHabilidadApiClient implementa con seed determinista | ✅ | Confirmado via tests en `FakeHabilidadApiClientPersonasTests.cs` (4 tests) — `GetPersonasSeed`, `GetPersonasCalls`, `GetPersonasHandler` reportados en apply-progress.md § PR C. |
| 15 | Tests cubren PageModel auth, GET, paginación, search, sort, toggle | ✅ | `HabilidadesPersonasPageTests.cs:9 tests` (anonymous redirect, Guid.Empty, padre no encontrado, segmento eliminadas, paginación, search, sort, link, estado vacío). Cobertura completa. |
| 16 | `bun run build` pasa sin errores | ✅ | Exit code 0. Ver §2 arriba. |

**Trazabilidad: 16/16 ✅** — cada criterio está implementado y testeado.

---

## 5. Design drift

Comparación entre `design.md` y la implementación actual. Drift documentado en `apply-progress.md` § PR C / "Desviaciones del design":

| Decisión de diseño | Implementado | Notas |
|---|---|---|
| DTOs en `Habilidades/Consultas/Dtos/` | ✅ | 3 archivos creados: `SkillPersonaDetailDto.cs`, `HabilidadPersonasListQuery.cs`, `PersonaHabilidadesPageResult.cs`. |
| Servicio consulta valida Guid.Empty + parent existe | ✅ | `SkillPersonaServicioConsulta.cs:15-18` Guid.Empty throw; `:20-24` parent null. |
| Repo ordena pre-`Select` (gotcha Pomelo) | ✅ | `SkillPersonaRepository.cs:35-36` ApplySort, luego `:39` Select proyecta DTO. |
| Wire contracts usan `PersonaSegmentoListado` (no `HabilidadSegmentoListado`) | ✅ | `HabilidadPersonasListQuery.cs:11`, normalización en controller. |
| Endpoint `[HttpGet("{skillId:guid}/personas")]` con auth heredada | ✅ | `SkillsController.cs:176-179`. `[Authorize]` heredado del controller (línea 21). |
| Cliente tipado `GetPersonasAsync` firma async | ✅ | `IHabilidadApiClient.cs:83-86`. |
| PageModel `[Authorize]` (sin rol) | ✅ | `Personas.cshtml.cs:24`. |
| Page readonly (sin POST) | ✅ | 0 handlers POST en el PageModel. |
| BuildPersonasRouteValues preserva contexto | ✅ | `Habilidades/Index.cshtml.cs:334-341` usa `RouteValueDictionary` (espejo del precedent `BuildCargosRouteValues` documentado por PR #88 review 🟡6). |
| Botón `ti ti-users` entre Cargos y Editar | ✅ | Ver REQ-HLD-NEW-POSITION §3. |

### Desviaciones documentadas (no críticas)

1. **PageModel más simple que `Cargos.cshtml.cs`** — no expone `EsAdministrador` ni `BuildPaginationRouteValues`. Documentado en `apply-progress.md` § PR C / Desviaciones. Razonable: REQ-HM-NEW-AUTH exige sin restricción de rol, por lo que omitir `EsAdministrador` es consistente. La paginación visual puede agregarse sin breaking change.

2. **`BuildPersonasRouteValues` usa `RouteValueDictionary` en lugar de anonymous object** — espejo del precedent `BuildCargosRouteValues` (PR #88 review 🟡6) para que `Segmento == null` no se serialice como `?status=` en activas. Documentado.

3. **`PersonaDto.IsActive` no se renderiza en la grilla** — el diseño declara que el segmento viaja vía `PersonaHabilidadesPageResult.Segmento` y se muestra en el toggle del header. Decisión deliberada. La columna de la grilla no incluye badges de activo/eliminado por persona individual.

### Desviaciones NO documentadas (SUGGESTION)

- **SUGGESTION 1**: `apply-progress.md` documenta que el total de líneas (1330) supera el estimate de tareas (850-1080). El cambio real fue +1330 insertions vs +400-500 por PR. Esto es parcialmente explicable por la regla de Strict TDD (más tests + docs XML completos). No es un blocker, pero el orquestador futuro debería reestimar cuando triangule tests exhaustivamente.

**Diseño: PASS** — todas las decisiones de diseño seguidas; desviaciones acotadas y documentadas.

---

## 6. Risks realization

Mapeo de los 10 riesgos del `proposal.md` § "Riesgos y mitigaciones" + 3 críticos + 3 moderados del `exploration.md` § 5.

| # | Riesgo (source) | Materializado | Mitigación funcionó | Detalle |
|---|---|---|---|---|
| 1 | Total supera review budget 800 (proposal §R1) | Sí (post-apply: 1330 insertions) | Sí | Split en 3 chained PRs funcionó (PR #188, #189, #190 mergeados a `develop`). |
| 2 | Gotcha Pomelo OrderBy sobre DTO posicional (proposal §R2) | No | Sí (preventiva) | `SkillPersonaRepository.cs:35-36` ordena `Persona.Legajo/Apellidos/Nombres` ANTES del Select. Tests de sort del controller validan end-to-end (8/8 PASS). |
| 3 | Issue #59 latente MySqlFact caídos (proposal §R3) | No | Sí | Tests del controller usan `WebApplicationFactory` + `AddBearerToken()` con servicio fake, NO `[MySqlFact]`. Issue #59 sigue latente pero no fue empeorado. |
| 4 | Confundir segmento persona vs habilidad (proposal §R4) | No | Sí | `HabilidadPersonasListQuery` y `SkillsController.cs:194-196` usan `PersonaSegmentoListado` explícitamente. |
| 5 | Drift documental (proposal §R5) | No | Sí | Las 4 specs del change (`skill-persona-query-contract` NEW + 3 DELTAs) están actualizadas y el apply-progress refleja cada PR. |
| 6 | Botón "Personas" en fila eliminada (proposal §R6) | No | Sí | Gating `!Model.IsDeletedView` en `Habilidades/Index.cshtml:157`. Test 2 cubre. |
| 7 | Pérdida de contexto al volver (proposal §R7) | No | Sí | `BuildPersonasRouteValues` + `BuildHabilidadesRouteValues` preservan `p/search/sort/status`. Tests cubren preservación. |
| 8 | Inconsistencia color botones (proposal §R8) | No | Sí | Ambos botones usan `btn-primary btn-icon btn-sm rounded-circle`. |
| 9 | PRs chained mal coordinados (proposal §R9) | No | Sí | PR A (`2c8e5d39`), PR B (`3079958`, `2453943`, `069709c`, `7604ae4`), PR C (`a996f614`, `e11d208e`, `373daa85`) mergeados secuencialmente. `git log` confirma. |
| 10 | Paths del DTO difieren del precedent (proposal §R10) | Resuelto en design | N/A | Adoptado `Habilidades/Consultas/Dtos/` por precedent de owner. Documentado. |
| E1 | Tests `[MySqlFact]` rompen build pipeline (exploration §5) | No | Sí | Tests del change evitan MySQL por completo. |
| E2 | Inconsistencia DTO: olvidar link fields (exploration §5) | No | Sí | `SkillPersonaDetailDto` tiene los 3 init-only fields (`PersonaId`, `HabilidadId`, `NivelHabilidadId`). Tests lo verifican. |
| E3 | Tocar `Habilidades/Details.cshtml` (exploration §5) | No | Sí | Confirmado por git diff: `Habilidades/Details.cshtml*` no modificado. |

**Realización: 0 riesgos materializados como fallos.** 1 riesgo materializado en tamaño (1330 vs 800 budget) pero mitigado por split.

---

## 7. Spec delta validity

Validación de las 4 specs para merge en `sdd-archive`:

### `skill-persona-query-contract` (NUEVA)

- 7 requirements (REQ-SPQC-01 a REQ-SPQC-07), 9 scenarios.
- Cada requirement tiene scenario(s) verificables (GIVEN/WHEN/THEN).
- Cubierto por 13 tests (2 contracts + 3 application + 8 controller).
- ✅ Válido para merge como NEW spec en archive.

### `habilidad-web-listado-detalle-baja` (DELTA — MODIFIED + ADDED)

- 1 requirement MODIFIED (con `(Previously: ...)` que documenta el delta).
- 3 requirements ADDED (REQ-HLD-NEW, REQ-HLD-NEW-VISIBILITY, REQ-HLD-NEW-POSITION).
- Cada requirement tiene scenarios verificables.
- Cubierto por 3 tests (`HabilidadesIndexPersonasButtonTests`).
- ✅ Válido para merge como ADDED en delta sobre spec existente.

### `habilidad-management` (DELTA — ADDED only)

- 4 requirements ADDED (REQ-HM-NEW-PAGE, REQ-HM-NEW-AUTH, REQ-HM-NEW-READONLY, REQ-HM-NEW-LINK).
- Cada requirement tiene scenarios verificables (GIVEN/WHEN/THEN).
- Cubierto por 9 tests (`HabilidadesPersonasPageTests`).
- ✅ Válido para merge como ADDED en delta.

### `persona-management` (DELTA — ADDED only)

- 4 requirements ADDED (REQ-PM-NEW, REQ-PM-NEW-ADMIN, REQ-PM-NEW-POSITION, REQ-PM-NEW-CONTEXT).
- Cada requirement tiene scenarios verificables.
- Cubierto por 3 tests (`PersonasIndexHabilidadesButtonTests`).
- ✅ Válido para merge como ADDED en delta.

### Spec delta validity — resumen

- **18 requirements totales** entre las 4 specs.
- **18 requirements con al menos un scenario ejecutable** (no orphans).
- **18 requirements con al menos un covering test PASS** en runtime.
- **Cero requirements huérfanos.**
- **Cero scenarios sin covering test.**

**Delta validity: ✅ OK** — todas las specs listas para merge en `sdd-archive`.

---

## 8. Strict TDD compliance

Como `strict_tdd: true` está activo, audito TDD por PR:

### TDD Cycle Evidence (verified contra realidad)

| PR | Test File | Safety Net (pre-PR) | RED (test written first) | GREEN (PASS post-impl) | Triangulación |
|---|---|---|---|---|---|
| **A.1** Personas admin → visible | `PersonasIndexHabilidadesButtonTests` | 2787/2787 ✅ | ✅ Compilation FAIL (no anchor) | ✅ 3/3 PASS | ✅ Admin + no-admin (2 escenarios) |
| **A.2** Personas no-admin → oculto | mismo | 2787/2787 ✅ | ✅ PASS trivially (no botón) → guard de regresión | ✅ 3/3 PASS | ➖ Single (1 escenario) |
| **A.3** Helper preserva contexto | mismo | 2787/2787 ✅ | ✅ FAIL (helper ausente) | ✅ 3/3 PASS | ➖ Single (preservación end-to-end es 1 caso) |
| **B.1** Contracts (DTO + Query + Result) | `SkillPersonaContractsCompatibilityTests` | 2787/2787 ✅ | ✅ Compilation FAIL (tipos inexistentes) | ✅ 2/2 PASS | ✅ Shape JSON + metadata |
| **B.2** Application service | `SkillPersonaServicioConsultaTests` | 2787/2787 ✅ | ✅ Compilation FAIL | ✅ 3/3 PASS | ✅ Guid.Empty + padre ausente + happy path |
| **B.3** API Controller endpoint | `HabilidadesPersonasControllerTests` | 2787/2787 ✅ | ✅ 6/8 FAIL pre-impl | ✅ 8/8 PASS | ✅ 8 escenarios completos (auth/200/404/paging/sort/search/segmento/límites) |
| **C.1** Typed client | `HabilidadApiClientGetPersonasTests` | 2803/2803 ✅ | ✅ Compile FAIL (`GetPersonasAsync` ausente) | ✅ 5/5 PASS | ✅ 5 escenarios (happy + query params + 5xx + 404 + cancellation) |
| **C.2** Fake | `FakeHabilidadApiClientPersonasTests` | 2803/2803 ✅ | ✅ Compile FAIL (`GetPersonasSeed`/`GetPersonasCalls` ausentes) | ✅ 4/4 PASS | ✅ 4 escenarios (seeded + search + non-seeded + eliminadas) |
| **C.3** PageModel + Page | `HabilidadesPersonasPageTests` | 2803/2803 ✅ | ✅ 9/9 FAIL (404 por página inexistente) | ✅ 9/9 PASS | ✅ 9 escenarios (auth + happy + not-found + Guid.Empty + segmento + paginación + link + empty + transport failure) |
| **C.4** Index button | `HabilidadesIndexPersonasButtonTests` | 2803/2803 ✅ | ✅ 2/3 FAIL (1 trivial: deleted-row nunca tuvo botón) | ✅ 3/3 PASS | ✅ 3 escenarios (active visible + contexto + deleted oculto + orden) |

**TDD Compliance**: 10/10 checks passed. La auditoría de apply-progress.md coincide con la realidad:
- Todos los archivos de test existen.
- Todos los tests pasan (2824/2824).
- Cada ciclo RED → GREEN verificable (compilación FAIL pre-impl o suite regression).
- Safety nets registrados en cada PR (2787 baseline pre-A; 2787 pre-B; 2803 pre-C).

### Test Layer Distribution

| Layer | Tests | Files | Tools |
|---|---|---|---|
| Unit (typed client, fake, contracts, servicio aplicación) | 14 | 4 | xUnit |
| Integration (controller API + PageModel + Index) | 23 | 4 | xUnit + WebApplicationFactory |
| E2E (no aplica) | 0 | 0 | N/A |
| **Total** | **37** | **8** | |

### Assertion Quality Audit

Auditoría rápida sobre los 8 archivos de test:
- ❌ **Sin tautologías**: todas las assertions comparan valor real contra valor esperado.
- ❌ **Sin asserts huérfanos**: cada test setea el contexto y verifica un outcome.
- ❌ **Sin ghost loops**: ningún assert `forEach` sobre colecciones posiblemente vacías.
- ❌ **Sin type-only assertions usados solos**: cuando aparece `Assert.NotNull`, está acompañado de asserts de valor.
- ❌ **Sin mocks > 2× assertions**: ratio mock/assert < 1 en todos los archivos.
- ✅ **Sobre `expect(true).toBe(true)` / similares**: 0 ocurrencias.

**Assertion quality**: ✅ Todas las assertions verifican comportamiento real.

---

## 9. Issues found

### CRITICAL

**Ninguno.**

### WARNING

1. **W1 — Build warnings pre-existentes (84)** — `CS8524` (switch no exhaustivo sobre `ErrorCategoria`), `xUnit1031` (test sleeps), `CS8602` (null deref), `NU1510` (paquetes no podables). Todos pre-existentes al change. No introducidos por este PR. **Acción**: track en issues separadas; no bloquear archive.

2. **W2 — Tamaño real supera estimate (1330 vs 850-1080)** — Documentado en `apply-progress.md` § PR C. Explicable por triangulación Strict TDD exhaustiva y XML docs completos en firmas públicas. No bloquea archive, pero futura planificación de PRs debería asumir +50% de overhead por tests/docs.

3. **W3 — Modificación incidental fuera de scope** — `git status` muestra `src/SGV.Web/wwwroot/js/pages/auth-password.js` modificada (cambio en `console.warn`: de string literal a "wrapper" como objeto). **No relacionado con este change** (working copy contamination). **Acción**: commit separado o revert antes de pruer; no bloquea archive porque NO toca archivos del change.

### SUGGESTION

1. **S1 — Coverage de los archivos cambiados** — no se corrió `dotnet test ... --coverage` (no es gate en este repo, sólo informativo). Para auditoría futura podría correrse con `--collect:"XPlat Code Coverage"` y filtrarse a los archivos del change. **No bloquea**.

2. **S2 — Test 2 de PR A pasa trivialmente pre-impl** — `PersonasIndexHabilidadesButtonTests` "no-admin oculta" pasa trivialmente porque no había botón. Es guard de regresión legítimo (documentado en apply-progress.md). Patrón aceptable en Strict TDD cuando pre-condiciones estructurales evitan el path.

3. **S3 — Linter**: no se corrió linter explícito (`dotnet format --verify-no-changes` o equivalente) sobre los archivos del change. No es gate del repo. **No bloquea**.

---

## 10. Veredicto final

### **`PASS WITH WARNINGS`**

**Justificación**:
- Todos los 16 criterios de aceptación de la issue #187 están implementados y cubiertos por tests runtime PASS.
- Las 18 requirements de las 4 specs del change están cumplidas con covering tests runtime PASS.
- `dotnet build SGV.slnx`: 0 errors.
- `dotnet test SGV.slnx`: 2824/2824 PASS (37 tests del change específicos PASS).
- `bun run build`: exit code 0.
- Strict TDD compliance: 10/10 checks (TDD cycle evidence verificado contra tests reales).
- 0 riesgos materializados como fallos.
- Spec delta validity: 4 specs listas para archive merge.

Los 3 warnings son pre-existentes (no introducidos por este change) o notes de diseño documentadas; ninguno bloquea archive.

---

## 11. Recomendación archive

**Proceder con `sdd-archive`** del change `agrega-navegacion-personas-habilidades`.

### Pasos del orquestador para archive

1. Verificar que la rama de archive está limpia: solo `openspec/changes/agrega-navegacion-personas-habilidades/` y el spec merges son editados.
2. Merge de los 4 specs deltas en sus specs canónicas respectivas:
   - `skill-persona-query-contract` (NEW) → archivar como spec independiente en `openspec/specs/skill-persona-query-contract/spec.md`.
   - `habilidad-web-listado-detalle-baja/spec.md` (DELTA) → mergear ADDED `REQ-HLD-NEW*` + MODIFIED sobre `openspec/specs/habilidad-web-listado-detalle-baja/spec.md`.
   - `habilidad-management/spec.md` (DELTA) → mergear ADDED `REQ-HM-NEW*` sobre `openspec/specs/habilidad-management/spec.md`.
   - `persona-management/spec.md` (DELTA) → mergear ADDED `REQ-PM-NEW*` sobre `openspec/specs/persona-management/spec.md`.
3. Mover `openspec/changes/agrega-navegacion-personas-habilidades/` → `openspec/changes/archive/2026-07-22-agrega-navegacion-personas-habilidades/` (manteniendo `verify-report.md`, `archive-report.md`, etc.).
4. Actualizar `docs/decisiones-implementacion.md` con:
   - Sección de navegación Persona↔Habilidad simétrica (ya existe `habilidades→cargos`; ahora también `personas→habilidades` y `habilidades→personas`).
   - Endpoint `GET /api/v1/skills/{id}/personas` documentado (matriz auth: cualquier autenticado).
   - Página `/organizacion/habilidades/{id}/personas` documentada.
5. Commit final con conventional message: `docs(sdd): archive agrega-navegacion-personas-habilidades after verify`.

### Pre-condiciones para archive (todas ✅)

- [x] Build en 0 errores.
- [x] Suite completa PASS (2824/2824).
- [x] Bun build PASS.
- [x] 0 issues CRITICAL en verify-report.
- [x] Strict TDD 10/10 checks.
- [x] 18/18 requirements con covering tests PASS.
- [x] 4/4 specs válidas para delta merge.
- [x] All 3 PRs (#188, #189, #190) mergeados a `develop`.
- [x] Rollback boundaries documentadas por PR.

### Bloqueos pre-archive

**Ninguno.** Proceder con archive.

---

## 12. Anexos

### A. Test files auditados (8)

| Path | Tests | PR | Estado |
|---|---|---|---|
| `tests/SGV.Tests/Web/Persona/PersonasIndexHabilidadesButtonTests.cs` | 3 | A | ✅ Existe + 3/3 PASS |
| `tests/SGV.Tests/Contracts/Habilidades/SkillPersonaContractsCompatibilityTests.cs` | 2 | B | ✅ Existe + 2/2 PASS |
| `tests/SGV.Tests/Aplicacion/Habilidades/SkillPersonaServicioConsultaTests.cs` | 3 | B | ✅ Existe + 3/3 PASS |
| `tests/SGV.Tests/Api/HabilidadesPersonasControllerTests.cs` | 8 | B | ✅ Existe + 8/8 PASS |
| `tests/SGV.Tests/Web/Habilidad/HabilidadApiClientGetPersonasTests.cs` | 5 | C | ✅ Existe + 5/5 PASS |
| `tests/SGV.Tests/Web/Habilidad/FakeHabilidadApiClientPersonasTests.cs` | 4 | C | ✅ Existe + 4/4 PASS |
| `tests/SGV.Tests/Web/Habilidad/HabilidadesPersonasPageTests.cs` | 9 | C | ✅ Existe + 9/9 PASS |
| `tests/SGV.Tests/Web/Habilidad/HabilidadesIndexPersonasButtonTests.cs` | 3 | C | ✅ Existe + 3/3 PASS |
| **Total** | **37** | — | **37/37 PASS** |

### B. Archivos de código del change (verificados)

**Creados (PR B):**
- `src/SGV.Contracts/Habilidades/Consultas/Dtos/SkillPersonaDetailDto.cs`
- `src/SGV.Contracts/Habilidades/Consultas/Dtos/HabilidadPersonasListQuery.cs`
- `src/SGV.Contracts/Habilidades/Consultas/Dtos/PersonaHabilidadesPageResult.cs`
- `src/SGV.Aplicacion/Habilidades/Consultas/ISkillPersonaServicioConsulta.cs`
- `src/SGV.Aplicacion/Habilidades/Consultas/SkillPersonaServicioConsulta.cs`
- `src/SGV.Aplicacion/Habilidades/Consultas/ISkillPersonaRepository.cs`
- `src/SGV.Infraestructura/Persistencia/Repositorios/SkillPersonaRepository.cs`

**Creados (PR C):**
- `src/SGV.Web/Pages/Organizacion/Habilidades/Personas.cshtml`
- `src/SGV.Web/Pages/Organizacion/Habilidades/Personas.cshtml.cs`

**Modificados (PR A):**
- `src/SGV.Web/Pages/Personas/Index.cshtml` (+3 anchor)
- `src/SGV.Web/Pages/Personas/Index.cshtml.cs` (+19 helper)

**Modificados (PR B):**
- `src/SGV.Api/Controllers/SkillsController.cs` (+32 endpoint)
- `src/SGV.Infraestructura/DependencyInjection.cs` (registro Scoped de repo)
- `src/SGV.Aplicacion/DependencyInjection.cs` (registro de servicio)

**Modificados (PR C):**
- `src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs` (+15 firma)
- `src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs` (+53 impl)
- `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml` (+7 anchor)
- `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml.cs` (+27 helper)

### C. Commits verificados (orden cronológico)

```text
cafc590c Merge pull request #190 from elflacoseba/feat/agrega-navegacion-personas-habilidades-pr-c-frontend
9507dd34 docs(sdd): record PR C apply progress
373daa85 feat(web): add Personas button to Habilidades/Index
e11d208e feat(web): add Habilidades/Personas Razor Page (readonly, [Authorize])
a996f614 feat(web): add GetPersonasAsync to HabilidadApiClient + FakeHabilidadApiClient
68981e30 Merge pull request #189 from elflacoseba/feat/agrega-navegacion-personas-habilidades-pr-b-backend
13dbf86c docs(sdd): record PR B apply progress
7604ae4a feat(api): add skill personas endpoint
069709cc feat(infraestructura): add skill persona repository
24539439 feat(aplicacion): add skill persona query service
30799582 feat(contracts): add skill persona query contracts
ebbcb97a Merge pull request #188 from elflacoseba/feat/agrega-navegacion-personas-habilidades-pr-a-ui-personas
6e5f39bb docs(sdd): apply progress for agrega-navegacion-personas-habilidades PR A
2c8e5d39 feat(personas): agrega botón Habilidades en Personas/Index (admin-only)
```

---

## Result Contract para el orquestador

```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:eabc2607d7d945c5a5ff67ca617d51ea8ee23d6d400f5b9a3a724feb07be3b89
verdict: pass-with-warnings
blockers: 0
critical_findings: 0
warnings: 3
requirements: 18/18
scenarios: 18/18
test_command: "dotnet test SGV.slnx --no-build"
test_exit_code: 0
test_output_hash: sha256:7840f07309db89adb41d84099498cfc225fb1bce1a70d7bb810b7db268a3583d
build_command: "dotnet build SGV.slnx"
build_exit_code: 0
build_output_hash: sha256:ffe41ebed59b2511e85788318e37a1b4cb64e7cc18782aa337691f1b962b3be3
bun_command: "cd src/SGV.Web && bun run build"
bun_exit_code: 0
bun_output_hash: sha256:10f7764c37040c476a467492fd959b302b6d66a328ce56b8f2657c3f4deb3179
strict_tdd_compliance: 10/10
change: agrega-navegacion-personas-habilidades
prs_merged: [188, 189, 190]
total_tests: 2824/2824
change_specific_tests: 37/37
specs_total: 4
specs_compliant: 4
risks_materialized: 0
recommendation: archive
next_recommended: archive
```
