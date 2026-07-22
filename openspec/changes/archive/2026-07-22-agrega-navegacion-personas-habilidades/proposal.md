# Proposal: agrega-navegacion-personas-habilidades

> Issue #187 — "Agregar botón para ver las Habilidades de una Persona (y botón reverso para ver las Personas de una Habilidad)"
> Artifact store: hybrid (openspec + engram)
> Preflight: interactive, ask-on-risk, strict_tdd, review_budget_lines 800

## Resumen ejecutivo

Este change cierra el gap simétrico del subrecurso `Persona↔Habilidad` en UI: agrega navegación **Persona → Habilidades** (página ya existente, hoy solo accesible desde `Details`) y **Habilidad → Personas** (página nueva que requiere backend + API + cliente tipado + Razor Page). El backend se mantiene intacto en dominio y persistencia; las decisiones D1-D5 están locked; los precedentes archivados (`2026-07-05-habilidades-navegacion-cargos`, `2026-07-06-cargos-navegacion-habilidades`) marcan el patrón a seguir. Total estimado **850–1190 líneas** → supera el review budget de 800 → **split obligatorio en 3 chained PRs stacked-to-main**.

## Trazabilidad con issue #187

Mapeo punto-por-punto de los 16 criterios de aceptación de la issue contra las secciones/unidades que los cubren.

| # | Criterio de aceptación (issue #187) | Cubierto en |
|---|---|---|
| 1 | `Personas/Index`: admin ve botón "Habilidades" en cada fila activa | proposal § Dec. producto D3; tasks PR A |
| 2 | `Personas/Index`: `href` incluye `id, page, search, sort, status` | proposal § Dec. técnicas (helper `BuildHabilidadesRouteValues`); tasks PR A |
| 3 | `Habilidades/Index`: cualquier autenticado ve botón "Personas" entre Cargos y Editar | proposal § Dec. producto D1, § Dec. técnicas; tasks PR C |
| 4 | `Habilidades/Personas`: grilla paginada con legajo, apellidos, nombres, email, nivel | proposal § Dec. técnicas (DTO y PageModel); tasks PR C |
| 5 | `Habilidades/Personas`: toggle `activas\|eliminadas` | proposal § Dec. técnicas (`status` query param); tasks PR C |
| 6 | `Habilidades/Personas`: cada fila linkea a `Pages/Personas/Details` | proposal § Dec. técnicas; tasks PR C |
| 7 | `GET /api/v1/skills/{skillId}/personas`: 200 paginado con `SkillPersonaDetailDto(PersonaDto, NivelHabilidadDto, PersonaId, NivelHabilidadId, HabilidadId)` | proposal § Dec. producto D5 (corregido vs. v1: incluye `HabilidadId`); tasks PR B |
| 8 | Endpoint acepta `page, pageSize, search, sort, status` (default `activas`) | proposal § Dec. producto D4; tasks PR B |
| 9 | `search` busca sobre legajo, nombres, apellidos | tasks PR B (repositorio) |
| 10 | `sort` acepta `legajo_asc\|desc`, `apellidos_asc\|desc`, `nombres_asc\|desc` | tasks PR B |
| 11 | `status` filtra por segmento de **persona** (no de habilidad) | proposal § Dec. producto D4; tasks PR B |
| 12 | `SkillPersonaDetailDto` vive en `SGV.Contracts.Habilidades.Consultas.Dtos` *(la issue dice `Organizacion/Consultas/Dtos` pero el precedent `SkillCargoDetailDto` está en `Habilidades/Consultas/Dtos/`; respetamos el precedent)* | proposal § Dec. técnicas |
| 13 | `IHabilidadApiClient.GetPersonasAsync(Guid skillId, HabilidadPersonasListQuery)` | proposal § Dec. técnicas; tasks PR B (firma), PR C (impl) |
| 14 | `FakeHabilidadApiClient` implementa con seed determinista | tasks PR B (interface, impl) |
| 15 | Tests cubren 7 escenarios (auth anon, con/sin resultados, paginación, búsqueda, toggle segmento, sort, OnGet recupera HabilidadDto padre o `IsRecoverable`) | proposal § Plan de pruebas |
| 16 | `bun run build` pasa | tasks PR C |

**NON-goals de la issue cubiertos**: (a) no se toca `Habilidades/Details.cshtml`; (b) no se agregan writes en `Habilidades/Personas`; (c) no se exponen `VerificadoAt`/`Fuente` (no existen en `PersonaHabilidad`); (d) no se modifica el PageModel `PersonaHabilidades`. Ver § Alcance.

## Contexto y motivación

- La issue #187 pide el botón Persona→Habilidades; el explore confirmó que el reverso (Habilidad→Personas) también falta y debe entrar en el mismo slice.
- Estado hoy: `Personas/Index` no expone `PersonaHabilidades` (sólo desde `Details.cshtml` admin-only). `Habilidades/Index` no expone "Personas".
- Por qué ahora: (a) `GET /api/v1/skills/{skillId}/cargos` archivado y validado como precedent; (b) `PersonaSkillRepository.ListDetailedByPersonaIdAsync` se aprovecha como espejo; (c) dejar el gap degrada descubribilidad del vínculo Persona↔Habilidad.

## Alcance

### Incluido
- Botón admin-only "Habilidades" en `Pages/Personas/Index.cshtml` (entre Detalle y Editar).
- Botón "Personas" en `Pages/Organizacion/Habilidades/Index.cshtml` (entre Cargos y Editar, filas activas).
- Página readonly `Pages/Organizacion/Habilidades/Personas.cshtml(+cs)` con GET handler y toggle `activas|eliminadas`.
- Endpoint readonly `GET /api/v1/skills/{skillId}/personas` paginado, búsqueda, sort, segmento de persona.
- Wire-types `SkillPersonaDetailDto` y `HabilidadPersonasListQuery` en `SGV.Contracts.Habilidades.Consultas.Dtos` (espejo del precedent `SkillCargoDetailDto`).
- Servicio `ISkillPersonaServicioConsulta` + impl, repositorio `ISkillPersonaRepository` + `SkillPersonaRepository`.
- `IHabilidadApiClient.GetPersonasAsync(...)` + `FakeHabilidadApiClient`.
- Tests: contratos, controller API, PageModel, Index personas/habilidades, cliente tipado, web integration.

### Excluido (Non-Goals)
- **N1** NO se modifica `Habilidades/Details.cshtml` (fuera de alcance explícito).
- **N2** NO se exponen writes Persona↔Habilidad desde la página nueva (gestión sigue en `Personas/PersonaHabilidades`).
- **N3** NO se exponen `VerificadoAt` ni `Fuente` de `PersonaHabilidad` (el dominio no los modela aún).
- **N4** NO se modifica el PageModel `PersonaHabilidades` (mantiene su guarda admin-only).
- NO se modifica `Personas/Details.cshtml` (ya tiene su botón).
- NO se migra `PersonaHabilidad` ni se cambian reglas de soft-delete.
- NO se relaja la regla `EsAdministrador` para los botones en `Personas/Index`.
- NO se mezclan segmentos `activas|eliminadas` en el endpoint nuevo.

## Decisiones de producto (D1-D5 — locked)

| ID | Decisión | Resolución | Tradeoff |
|---|---|---|---|
| **D1** | Auth página `Personas.cshtml` | `[Authorize]` sin rol (read-only autenticado) | Coherente con `Habilidades/Cargos`. Datos de persona ya accesibles por cualquier autenticado vía `Personas/Index`. |
| **D2** | Alcance funcional | Solo lectura + link a detalle de persona | Consistente con `Habilidades/Cargos`. Gestión ya existe en `Personas/PersonaHabilidades`. |
| **D3** | Botón en `Personas/Index` | `@if (Model.EsAdministrador)` admin-only | Mismo criterio que `Details.cshtml`. Evita affordance engañosa. |
| **D4** | Paginación/segmento endpoint | `page`, `pageSize`, `search`, `sort`, `status` (default `activas`). `status` es segmento de la **persona** (`PersonaSegmentoListado`), NO de la habilidad. Query param HTTP se llama `status` (no `segmento`). | Espejo exacto de `GetCargos`. |
| **D5** | Shape `SkillPersonaDetailDto` | `(PersonaDto Persona, NivelHabilidadDto Nivel)` + `PersonaId`, `NivelHabilidadId`, `HabilidadId` init-only (link fields). | Sin `VerificadoAt`/`Fuente` (no existen en `PersonaHabilidad`). Sin `PersonaEliminada` (la issue no lo pide; el precedent usa `CargoEliminado` porque el segmento es por cargo, pero aquí el segmento es por persona y eso se transporta en `PersonaDto.IsActive`). |

## Decisiones técnicas

- **Endpoint**: `GET /api/v1/skills/{skillId}/personas` en `SkillsController.cs`. `[Authorize]` heredado del controller; sin override de rol. Normalización idéntica a `GetCargos` (líneas 159-164 del controller).
- **Cliente tipado**: `Task<PagedResult<SkillPersonaDetailDto>> GetPersonasAsync(Guid skillId, HabilidadPersonasListQuery query, CancellationToken)` en `IHabilidadApiClient` + impl en `HabilidadApiClient` (espejo de `GetCargosAsync`).
- **Repositorio**: `SkillPersonaRepository.ListDetailedBySkillIdAsync(skillId, query, ct)`. `AsNoTracking` + proyección a DTO. **`OrderBy` sobre `PersonaEntity.Apellidos`/`Legajo` ANTES del `Select`** (gotcha Pomelo: sort sobre records posicionales anidados no se traduce).
- **Query record**: `HabilidadPersonasListQuery(int Page, int PageSize, string? Search, string? Sort, PersonaSegmentoListado Segmento = PersonaSegmentoListado.Activas)`. Query param HTTP `status`.
- **DTO**: `SkillPersonaDetailDto(PersonaDto Persona, NivelHabilidadDto Nivel)` con `PersonaId`, `NivelHabilidadId`, `HabilidadId` init-only (link fields), ubicado en `src/SGV.Contracts/Habilidades/Consultas/Dtos/SkillPersonaDetailDto.cs`. **Nota**: la issue lista el path bajo `Organizacion/Consultas/Dtos/`, pero el precedent `SkillCargoDetailDto` vive en `Habilidades/Consultas/Dtos/` y la regla del repo es "el owner del subrecurso define la carpeta" — como el subrecurso es `skill→personas`, el owner es `Skill` y por tanto la carpeta es `Habilidades/`.
- **Botón UI `Habilidades/Index`**: `ti-users`, `btn-primary btn-icon btn-sm rounded-circle`, entre `Cargos` y `Editar`. Visible solo cuando `!Model.IsDeletedView`. Helper `BuildPersonasRouteValues` centraliza id/p/search/sort/status.
- **Botón `Personas/Index`**: `ti-stars` icono (espejo del existente en `Details.cshtml`), admin-only, entre `Detalle` y `Editar`. Helper `BuildHabilidadesRouteValues`. Doble gate UI/backend coherente porque `PersonaHabilidades` ya valida admin.
- **Ubicación de archivos**:
  - DTOs/Query → `src/SGV.Contracts/Habilidades/Consultas/Dtos/` (espejo de `SkillCargoDetailDto.cs`, `HabilidadCargosListQuery.cs`).
  - Servicios → `src/SGV.Aplicacion/Habilidades/Consultas/` (espejo de `ISkillCargoServicioConsulta.cs`).
  - Repositorio → `src/SGV.Infraestructura/Persistencia/Repositorios/SkillPersonaRepository.cs`.

## Approve / estrategia de entrega

Split obligatorio en **3 chained PRs stacked-to-main** (total supera budget 800). PR A es independiente; PR B y PR C tienen dependencia secuencial.

| PR | Contenido | Líneas est. | Base | Depende de |
|---|---|---|---|---|
| **PR A** — UI Personas | `Personas/Index.cshtml` + helper; `Pages/Personas/Index.cshtml.cs`; `tests/SGV.Tests/Web/Persona/IndexPageTests.cs` (2 escenarios: activo+admin lo expone, activo+no-admin lo oculta) | 50–80 | `main` | — |
| **PR B** — Backend subreverso | `SGV.Contracts/Habilidades/Consultas/Dtos/SkillPersonaDetailDto.cs`+`HabilidadPersonasListQuery.cs`; `ISkillPersonaServicioConsulta`+impl; `ISkillPersonaRepository`+impl; `SkillsController` (`GetPersonas`); `IHabilidadApiClient` (`GetPersonasAsync` firma); `tests/SGV.Tests/Api/HabilidadesPersonasControllerTests.cs` (8 escenarios) | 400–500 | `main` | — |
| **PR C** — Frontend subreverso | `Pages/Organizacion/Habilidades/Personas.cshtml(+cs)`; `Habilidades/Index.cshtml`+helper; `HabilidadApiClient` impl + `FakeHabilidadApiClient` con seed; `tests/SGV.Tests/Web/Habilidad/HabilidadesPersonasModelTests.cs`; extensión `HabilidadesIndexPageTests` | 400–500 | `main` | PR B mergeado |

**Estrategia merge**: stacked-to-main. PR A y PR B mergeables en paralelo; PR C requiere PR B. Conflictos improbables porque las superficies no se solapan (PR A: `Pages/Personas/`; PR B: `Contracts/Aplicacion/Infraestructura/Api/Integration/firma`; PR C: `Pages/Organizacion/Habilidades/`).

## Capacidades OpenSpec afectadas

### New Capabilities
- **`skill-persona-query-contract`** — Contrato GET-only y readonly de `GET /api/v1/skills/{skillId}/personas`. Espejo exacto de `skill-cargo-query-contract`. Declara shape de `SkillPersonaDetailDto(PersonaDto, NivelHabilidadDto, PersonaId, NivelHabilidadId, HabilidadId)`, query record, normalización de `status`, gating de auth, paginación, búsqueda y sort.

### Modified Capabilities
- **`habilidad-web-listado-detalle-baja`** — Agregar CTA `Personas` en la lista de acciones de la vista `activas`, ubicado entre `Cargos` y `Editar`, con icono `ti-users`. La vista `eliminadas` MUST NOT renderizarlo. Preservar fuera de alcance la edición del vínculo.
- **`habilidad-management`** — Declarar `GET /api/v1/skills/{skillId}/personas` en la lista de sub-recursos readonly existentes. Mantener fuera de alcance los writes del vínculo `PersonaHabilidad`. Sumar el nuevo endpoint a la lista de lecturas autenticadas sin restricción de rol.
- **`persona-skill-web-management`** — El `Requirement: Descubribilidad desde el detalle de Persona` se extiende a `Personas/Index`: el botón `Habilidades` debe renderizarse admin-only también en `Personas/Index`, no solo en `Details.cshtml`. Mantener el icono `ti ti-stars`.

## Plan de pruebas (7 capas)

| # | Capa | Tipo | Escenarios clave |
|---|---|---|---|
| 1 | **Contratos** | Unitario | `SkillPersonaDetailDto` preserva nombres JSON tras serialización (espejo de `PersonaSkillContractsCompatibilityTests`) |
| 2 | **API Controller** | Integración (WebApplicationFactory) | GET con/sin resultados, paginación, search por legajo/apellido, sort, toggle status, 404 cuando `skillId` no existe, auth: 401 anónimo |
| 3 | **PageModel `Habilidades/Personas`** | Unitario (mockeando `IHabilidadApiClient`) | OnGet carga habilidad padre + lista paginada, toggle activas/eliminadas, search, sort, habilidad no encontrada → `IsRecoverable`, `Guid.Empty` → recarga |
| 4 | **PageModel `Personas/Index`** | Unitario | `BuildHabilidadesRouteValues` preserva contexto de paginación/búsqueda, `EsAdministrador` gatea render del botón |
| 5 | **Cliente tipado** | Unitario (HttpMessageHandler fake) | `GetPersonasAsync` serializa query params correctamente, response 200 deserializa `SkillPersonaDetailDto[]`, response 404/500 → `IsRecoverable` |
| 6 | **`Habilidades/Index`** | Unitario | Botón "Personas" visible solo en vista activas, `href` incluye `id` de habilidad |
| 7 | **Web integration** | Smoke (SgvWebApplicationFactory) | `bun run build` pasa, página `Habilidades/Personas` carga sin errores JS |

## Riesgos y mitigaciones

| # | Riesgo | Likelihood | Impacto | Mitigación |
|---|---|---|---|---|
| 1 | Total supera review budget 800 | Alta | Medio (gate) | Split en 3 chained PRs stacked-to-main. PR A despeja rápido. |
| 2 | Gotcha Pomelo: `OrderBy` sobre DTO posicional anidado | Media | Medio | Aplicar `OrderBy` sobre `PersonaEntity.Apellidos`/`Legajo` ANTES del `Select`. Test de sort en controller verifica end-to-end. |
| 3 | Issue #59 latente (12 tests `[MySqlFact]` rojos) | Baja | Bajo | Tests nuevos usan `WebApplicationFactory` + seed, NO `[MySqlFact]`. |
| 4 | Confundir segmento persona vs habilidad en endpoint | Baja | Bajo | Query record usa `PersonaSegmentoListado` (no `HabilidadSegmentoListado`). |
| 5 | Drift documental si no se actualizan las specs | Media | Medio | Renombrar "Out of scope" en `habilidad-management` para no contradecir al nuevo endpoint. Acceptance criteria prohíbe degradar el readonly. |
| 6 | Botón "Personas" renderizado en fila eliminada | Baja | Bajo | Gating `!Model.IsDeletedView` como ya hacen los botones existentes. |
| 7 | Pérdida de contexto al volver (p/search/sort/status) | Media | Medio | Helper `BuildPersonasRouteValues` centraliza los params. Test web verifica el `href` completo. |
| 8 | Inconsistencia de color entre botones Cargos/Personas | Baja | Bajo | Default `btn-primary` para ambos. |
| 9 | PRs chained mal coordinados (merge base main vs develop) | Media | Bajo | Stacked-to-main estricto. PR A y PR B mergeables en paralelo. PR C requiere `main` con PR B. |
| 10 | Paths del DTO listados en la issue difieren de los del precedent | Baja | Bajo | Documentado en § Decisiones técnicas; respetamos el precedent `Habilidades/Consultas/Dtos/` y la regla "owner del subrecurso define la carpeta". |

## Plan de rollback

- **PR A**: revertir `Pages/Personas/Index.cshtml`(+cs) y tests. No afecta otras features. Limpio.
- **PR B**: revertir `SkillsController` (endpoint `GetPersonas`), `SkillPersonaRepository`, servicio, contrato DTO y firma del cliente. No hay migraciones ni cambios de dominio. Limpio.
- **PR C**: revertir `Pages/Organizacion/Habilidades/Personas.cshtml(+cs)`, `Habilidades/Index.cshtml`(+cs) y tests web. La firma `GetPersonasAsync` del cliente queda sin consumidor; tolerable y reversible junto con PR B.

## Criterios de éxito

- [ ] Un `Administrador` ve un botón **Habilidades** en cada fila activa de `Personas/Index` y navega a `PersonaHabilidades`.
- [ ] Un usuario autenticado ve un botón **Personas** en cada fila activa de `Habilidades/Index` y navega a `Habilidades/Personas`.
- [ ] La página `Habilidades/Personas` respeta `status=activas|eliminadas`, muestra paginación, búsqueda y orden sobre datos de persona.
- [ ] El endpoint `GET /api/v1/skills/{skillId}/personas` responde `200` paginado con `SkillPersonaDetailDto` (incluye `PersonaDto`, `NivelHabilidadDto`, `PersonaId`, `NivelHabilidadId`, `HabilidadId`), `404` si la habilidad no existe, `401` sin token.
- [ ] Los CTAs `Personas` y `Habilidades` quedan ocultos en las vistas `eliminadas` y para no-administradores según corresponda.
- [ ] Las specs quedan alineadas: 1 nueva (`skill-persona-query-contract`) + 3 modificadas (`habilidad-web-listado-detalle-baja`, `habilidad-management`, `persona-skill-web-management`).
- [ ] `dotnet test SGV.slnx` verde y `bun run build` verde.

## Próximos pasos

1. Ejecutar `sdd-propose` (este artifact) y persistir en Engram — HECHO.
2. Ejecutar `sdd-spec` para redactar 1 delta spec nueva + 3 deltas de modificación.
3. Ejecutar `sdd-design` para detallar la arquitectura por capa.
4. Ejecutar `sdd-tasks` para descomponer en tareas por PR (budget ≤2h por tarea).
5. Ejecutar `sdd-apply` por PR (A → B → C) con gate `ask-on-risk` activado.
6. Ejecutar `sdd-verify` y `sdd-archive` por PR.

## Referencias

- Issue #187 (enriquecida): https://github.com/elflacoseba/SGV/issues/187
- `openspec/changes/agrega-navegacion-personas-habilidades/exploration.md`
- `openspec/specs/skill-cargo-query-contract/spec.md` (precedent directo)
- `openspec/specs/habilidad-web-listado-detalle-baja/spec.md`
- `openspec/specs/habilidad-management/spec.md`
- `openspec/specs/persona-skill-web-management/spec.md`
- `openspec/changes/archive/2026-07-05-habilidades-navegacion-cargos/proposal.md` (precedent subreverso)
- `openspec/changes/archive/2026-07-06-cargos-navegacion-habilidades/proposal.md` (precedent UI)
- `docs/decisiones-implementacion.md` líneas 229-256 (autorización API + gating UI)
- `src/SGV.Api/Controllers/SkillsController.cs` líneas 134-169 (`GetCargos` espejo)
- `src/SGV.Contracts/Habilidades/Consultas/Dtos/SkillCargoDetailDto.cs` (DTO espejo)
- `src/SGV.Infraestructura/Persistencia/Repositorios/PersonaSkillRepository.cs` líneas 21-58 (`ListDetailedByPersonaIdAsync` espejo)
- `src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs` líneas 60-71 (`GetCargosAsync` espejo)