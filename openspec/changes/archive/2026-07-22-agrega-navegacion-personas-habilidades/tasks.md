# Tasks: Agrega navegación Personas↔Habilidades

> Issue #187 — botón "Habilidades" en Personas/Index (punto 1) + botón "Personas" y página nueva en Habilidades (punto 2)
> strict_tdd: true | delivery: ask-always | review_budget_lines: 800

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 850–1080 (3 PRs) |
| 800-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR A (UI Personas, 50–80) → PR B (Backend, 400–500) → PR C (Frontend, 400–500) |
| Delivery strategy | ask-always |
| Chain strategy | stacked-to-main |

```text
Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
800-line budget risk: High
```

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Botón Habilidades en Personas/Index (Punto 1) | PR A | `dotnet test --filter "FullyQualifiedName~PersonasIndexPage_HabilidadesButton"` | `dotnet build SGV.slnx` | Revertir `Pages/Personas/Index.cshtml`(+cs) y tests |
| 2 | Backend subreverso: DTOs, repo, servicio, endpoint, firma cliente, fake | PR B | `dotnet test --filter "FullyQualifiedName~SkillPersona"` | `dotnet build SGV.slnx` | Revertir Contracts, Aplicacion, Infraestructura, Api, firma cliente y tests |
| 3 | Frontend subreverso: cliente impl, Razor Page, botón en Habilidades/Index | PR C | `dotnet test --filter "FullyQualifiedName~HabilidadesPersonasPage\|HabilidadesIndexPage_PersonasButton"` | `dotnet build SGV.slnx && bun run build` en `src/SGV.Web` | Revertir `Pages/Organizacion/Habilidades/Personas.cshtml*`, cambios en `Habilidades/Index`, cliente impl y tests web |

**Dependencias entre PRs**: PR A y PR B son independientes (mergean en paralelo a `main`). PR C depende de PR B (necesita endpoint y DTOs).

---

## PR A — UI Personas (Punto 1)

Objetivo: agregar botón "Habilidades" admin-only en `Personas/Index` con helper `BuildHabilidadesRouteValues`. Estimado: 50–80 líneas.

### Task A.1 — RED: tests de gating y helper
- **Files**: `tests/SGV.Tests/Web/Persona/PersonasIndexPageTests.cs` *(creado o extendido)*
- **Comportamiento**: (1) `BuildHabilidadesRouteValues` preserva `id`, `page`, `search`, `sort` y `status`. (2) Botón renderizado cuando fila activa + `EsAdministrador`. (3) Botón oculto cuando no-admin. (4) Botón oculto en vista eliminadas.
- **Spec**: REQ-PM-NEW, REQ-PM-NEW-ADMIN, REQ-PM-NEW-CONTEXT
- **Verify**: `dotnet test --filter "FullyQualifiedName~PersonasIndexPage_HabilidadesButton"` → **N PASS**
- **Dependencias**: ninguna

### Task A.2 — GREEN: helper + botón en Personas/Index
- **Files**: `src/SGV.Web/Pages/Personas/Index.cshtml` *(modificado)*, `src/SGV.Web/Pages/Personas/Index.cshtml.cs` *(modificado)*
- **Comportamiento**: Helper `BuildHabilidadesRouteValues(id)` con `RouteValueDictionary` preservando `page`, `search`, `sort`, `status`. Botón `ti ti-stars` `btn-primary btn-icon btn-sm rounded-circle` entre Detalle y Editar, gated con `Model.EsAdministrador && !item.IsDeletedView`.
- **Spec**: REQ-PM-NEW, REQ-PM-NEW-ADMIN, REQ-PM-NEW-POSITION, REQ-PM-NEW-CONTEXT
- **Verify**: `dotnet test --filter "FullyQualifiedName~PersonasIndexPage_HabilidadesButton"` → **4 PASS**
- **Dependencias**: A.1

### Task A.3 — VERIFY: build + tests focalizados
- **Files**: ninguno nuevo
- **Comportamiento**: `dotnet build SGV.slnx` sin errores, `dotnet test` focalizado pasa. Diferencias: solo `Personas/Index.cshtml*` y tests.
- **Verify**: `dotnet build SGV.slnx && dotnet test --filter "FullyQualifiedName~PersonasIndexPage_HabilidadesButton"`
- **Dependencias**: A.2

---

## PR B — Backend subreverso (Punto 2)

Objetivo: wire-types, repositorio, servicio consulta, endpoint API, firma de cliente y fake. Estimado: 400–500 líneas.

### Task B.1 — RED: tests DTOs y query record
- **Files**: `tests/SGV.Tests/Contracts/Habilidades/SkillPersonaContractsCompatibilityTests.cs` *(creado)*
- **Comportamiento**: `SkillPersonaDetailDto` preserva nombres JSON `persona`, `nivel`, `personaId`, `habilidadId`, `nivelHabilidadId`. `HabilidadPersonasListQuery` tiene `Page`, `PageSize`, `Search`, `Sort`, `Segmento` (con `PersonaSegmentoListado`). `PersonaHabilidadesPageResult` tiene `Items`, `Page`, `PageSize`, `Total`, `Sort`, `Segmento`.
- **Spec**: REQ-SPQC-03, REQ-SPQC-04, REQ-SPQC-07
- **Verify**: `dotnet test --filter "FullyQualifiedName~SkillPersonaContracts"` → **N PASS**
- **Dependencias**: ninguna

### Task B.2 — GREEN: crear wire-types en SGV.Contracts
- **Files**: `src/SGV.Contracts/Habilidades/Consultas/Dtos/SkillPersonaDetailDto.cs` *(nuevo)*, `src/SGV.Contracts/Habilidades/Consultas/Dtos/HabilidadPersonasListQuery.cs` *(nuevo)*, `src/SGV.Contracts/Habilidades/Consultas/Dtos/PersonaHabilidadesPageResult.cs` *(nuevo)*
- **Comportamiento**: Records públicos en `SGV.Contracts.Habilidades.Consultas.Dtos`. `SkillPersonaDetailDto(PersonaDto Persona, NivelHabilidadDto Nivel)` con `PersonaId`, `NivelHabilidadId`, `HabilidadId` init-only. `HabilidadPersonasListQuery` con `PersonaSegmentoListado`. `PersonaHabilidadesPageResult` con metadatos de paginación.
- **Spec**: REQ-SPQC-03, REQ-SPQC-04, REQ-SPQC-07
- **Verify**: `dotnet build SGV.slnx` → **0 errors**
- **Dependencias**: B.1

### Task B.3 — RED: tests de repositorio EF Core
- **Files**: `tests/SGV.Tests/Api/HabilidadesPersonasControllerTests.cs` *(creado, escenarios de repositorio)*
- **Comportamiento**: `SkillPersonaRepository.ListDetailedBySkillIdAsync` con `WebApplicationFactory`: paginación (pageSize=5, page=2 retorna 5 de 12), search por legajo/apellido/nombres, sort por apellidos_asc/legajo_desc, filtro `Persona.IsActive` vs `Persona.IsDeleted`, **OrderBy pre-Select** (verifica orden correcto con Pomelo).
- **Spec**: REQ-SPQC-01, REQ-SPQC-02, REQ-SPQC-05
- **Verify**: `dotnet test --filter "FullyQualifiedName~SkillPersonaRepository"` → **N PASS**
- **Dependencias**: B.2

### Task B.4 — GREEN: implementar ISkillPersonaRepository + SkillPersonaRepository
- **Files**: `src/SGV.Aplicacion/Habilidades/Consultas/ISkillPersonaRepository.cs` *(nuevo)*, `src/SGV.Infraestructura/Persistencia/Repositorios/SkillPersonaRepository.cs` *(nuevo)*
- **Comportamiento**: Interfaz con `ListDetailedBySkillIdAsync(Guid, HabilidadPersonasListQuery, CancellationToken)` → `(IReadOnlyList<SkillPersonaDetailDto>, int)`. Implementación con `AsNoTracking()`, JOIN `PersonaHabilidad`+`Persona`+`NivelHabilidad`, filtro `Persona.IsActive/IsDeleted` según segmento, **`OrderBy` sobre `PersonaEntity.Apellidos`/`Legajo` ANTES del `Select`** (gotcha Pomelo), `Skip/Take`, `CountAsync` separado.
- **Spec**: REQ-SPQC-01, REQ-SPQC-02, REQ-SPQC-05
- **Verify**: `dotnet test --filter "FullyQualifiedName~SkillPersonaRepository"` → **N PASS**
- **Dependencias**: B.3

### Task B.5 — RED: tests de SkillPersonaServicioConsulta
- **Files**: `tests/SGV.Tests/Aplicacion/Habilidades/SkillPersonaServicioConsultaTests.cs` *(creado)*
- **Comportamiento**: Mock del repositorio. `Guid.Empty` → `ArgumentException`. Habilidad no encontrada (servicio valida con `IHabilidadServicioConsulta.GetByIdAsync`) → 404. Happy path delega y mapea a `PersonaHabilidadesPageResult`.
- **Spec**: REQ-SPQC-06
- **Verify**: `dotnet test --filter "FullyQualifiedName~SkillPersonaServicioConsulta"` → **N PASS**
- **Dependencias**: B.4

### Task B.6 — GREEN: implementar ISkillPersonaServicioConsulta + SkillPersonaServicioConsulta
- **Files**: `src/SGV.Aplicacion/Habilidades/Consultas/ISkillPersonaServicioConsulta.cs` *(nuevo)*, `src/SGV.Aplicacion/Habilidades/Consultas/SkillPersonaServicioConsulta.cs` *(nuevo)*
- **Comportamiento**: Interfaz con `ListarPersonasAsync(Guid skillId, HabilidadPersonasListQuery, CancellationToken)`. Implementación valida `Guid.Empty`, verifica existencia de habilidad padre via `ISkillServicioConsulta.GetByIdAsync`, delega al repositorio, construye `PersonaHabilidadesPageResult`.
- **Spec**: REQ-SPQC-06
- **Verify**: `dotnet test --filter "FullyQualifiedName~SkillPersonaServicioConsulta"` → **N PASS**
- **Dependencias**: B.5

### Task B.7 — RED: tests del endpoint API SkillsController.GetPersonas
- **Files**: `tests/SGV.Tests/Api/HabilidadesPersonasControllerTests.cs` *(extendido)*
- **Comportamiento**: 8 escenarios: (1) 200 con personas, (2) 200 sin personas, (3) 404 skillId inexistente, (4) 401 sin token, (5) status inválido cae a activas, (6) paginación, (7) sort por legajo_desc, (8) filtro eliminadas. Sin `[MySqlFact]`, con `WebApplicationFactory` + `AddBearerToken()`.
- **Spec**: REQ-SPQC-01, REQ-SPQC-02, REQ-SPQC-05, REQ-SPQC-06, REQ-SPQC-07
- **Verify**: `dotnet test --filter "FullyQualifiedName~HabilidadesPersonasController"` → **8 PASS**
- **Dependencias**: B.6

### Task B.8 — GREEN: endpoint GetPersonas + DI wiring
- **Files**: `src/SGV.Api/Controllers/SkillsController.cs` *(modificado)*, `src/SGV.Infraestructura/DependencyInjection.cs` *(modificado)*, `src/SGV.Aplicacion/DependencyInjection.cs` *(modificado)*
- **Comportamiento**: Endpoint `[HttpGet("{skillId:guid}/personas")]` con normalización de `page`/`pageSize`/`status`/`sort`. Inyectar `ISkillPersonaServicioConsulta`. Registrar repo como `Scoped` en Infra DI y servicio en Aplicacion DI. Hereda `[Authorize]` del controller.
- **Spec**: REQ-SPQC-01 a 07 completos
- **Verify**: `dotnet test --filter "FullyQualifiedName~HabilidadesPersonasController"` → **8 PASS**
- **Dependencias**: B.7

### Task B.9 — RED: tests de firma IHabilidadApiClient.GetPersonasAsync
- **Files**: `tests/SGV.Tests/Web/Habilidad/HabilidadApiClientContractTests.cs` *(creado)*
- **Comportamiento**: Firma `Task<PagedResult<SkillPersonaDetailDto>> GetPersonasAsync(Guid, HabilidadPersonasListQuery, CancellationToken)` declarada. Fake registra invocaciones. Serialización de query params correcta.
- **Spec**: REQ-SPQC-04, REQ-SPQC-07
- **Verify**: `dotnet test --filter "FullyQualifiedName~HabilidadApiClientContract"` → **N PASS**
- **Dependencias**: B.2

### Task B.10 — GREEN: extender IHabilidadApiClient + FakeHabilidadApiClient
- **Files**: `src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs` *(modificado)*, `tests/SGV.Tests/Web/Habilidad/FakeHabilidadApiClient.cs` *(modificado)*
- **Comportamiento**: Firma `GetPersonasAsync` en interfaz. `FakeHabilidadApiClient` con seed determinista de `SkillPersonaDetailDto` y contadores de invocación.
- **Spec**: REQ-SPQC-04, REQ-SPQC-07
- **Verify**: `dotnet test --filter "FullyQualifiedName~HabilidadApiClientContract"` → **N PASS**
- **Dependencias**: B.9

### Task B.11 — VERIFY: build + suite focalizada
- **Files**: ninguno nuevo
- **Comportamiento**: `dotnet build SGV.slnx` sin errores. Suite completa de PR B verde.
- **Verify**: `dotnet build SGV.slnx && dotnet test --filter "FullyQualifiedName~SkillPersona\|HabilidadApiClientContract"`
- **Dependencias**: B.8, B.10

---

## PR C — Frontend subreverso (Punto 2)

Objetivo: implementar `HabilidadApiClient`, página `Habilidades/Personas`, botón en `Habilidades/Index`, tests web. Estimado: 400–500 líneas.

### Task C.1 — RED: tests de HabilidadApiClient.GetPersonasAsync
- **Files**: `tests/SGV.Tests/Web/Habilidad/HabilidadApiClientTests.cs` *(creado)*
- **Comportamiento**: HttpMessageHandler fake: serializa query params correctamente, response 200 deserializa `PagedResult<SkillPersonaDetailDto>`, response 404/500 → `IsRecoverable`. El `skillId` viaja en path, `page/pageSize/search/sort/status` en query.
- **Spec**: REQ-SPQC-04, REQ-SPQC-07, REQ-HM-NEW-PAGE
- **Verify**: `dotnet test --filter "FullyQualifiedName~HabilidadApiClientTests"` → **N PASS**
- **Dependencias**: B.10 (firma + fake)

### Task C.2 — GREEN: implementar GetPersonasAsync en HabilidadApiClient
- **Files**: `src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs` *(modificado)*
- **Comportamiento**: Construye URI `{BaseRoute}/{skillId}/personas` con query params vía `QueryHelpers`, invoca `HttpClient.GetAsync`, deserializa `PagedResult<SkillPersonaDetailDto>`. Maneja 401/404/5xx consistente con `GetCargosAsync`.
- **Spec**: REQ-SPQC-04, REQ-SPQC-07
- **Verify**: `dotnet test --filter "FullyQualifiedName~HabilidadApiClientTests"` → **N PASS**
- **Dependencias**: C.1

### Task C.3 — RED: tests de Habilidades/Personas PageModel
- **Files**: `tests/SGV.Tests/Web/Habilidad/HabilidadesPersonasModelTests.cs` *(creado)*
- **Comportamiento**: Mockeando `IHabilidadApiClient`: OnGet carga habilidad padre + lista paginada, toggle `activas`/`eliminadas`, search, sort, habilidad no encontrada → `IsRecoverable`, `Guid.Empty` → recarga (o redirect). Auth: anónimo redirige a sign-in.
- **Spec**: REQ-HM-NEW-PAGE, REQ-HM-NEW-AUTH, REQ-HM-NEW-READONLY, REQ-HM-NEW-LINK
- **Verify**: `dotnet test --filter "FullyQualifiedName~HabilidadesPersonasModel"` → **N PASS**
- **Dependencias**: C.2

### Task C.4 — GREEN: crear Habilidades/Personas.cshtml + PageModel
- **Files**: `src/SGV.Web/Pages/Organizacion/Habilidades/Personas.cshtml` *(nuevo)*, `src/SGV.Web/Pages/Organizacion/Habilidades/Personas.cshtml.cs` *(nuevo)*
- **Comportamiento**: PageModel con `[Authorize]` (sin rol). Props: `Id`, `Page`, `Search`, `Sort`, `Status`, `Items`, `HabilidadNombre`, `EsAdministrador`. OnGet: valida habilidad padre, invoca `IHabilidadApiClient.GetPersonasAsync`, mapea a ViewModel. Markup: grilla paginada (legajo, apellidos, nombres, email, nivel), toggle activas/eliminadas, búsqueda, sort, cada fila linkea a `Personas/Details/{id}`. Sin handlers POST. Estado vacío legible.
- **Spec**: REQ-HM-NEW-PAGE, REQ-HM-NEW-AUTH, REQ-HM-NEW-READONLY, REQ-HM-NEW-LINK
- **Verify**: `dotnet test --filter "FullyQualifiedName~HabilidadesPersonasModel"` → **N PASS**
- **Dependencias**: C.3

### Task C.5 — RED: tests del botón Personas en Habilidades/Index
- **Files**: `tests/SGV.Tests/Web/Habilidad/HabilidadesIndexPageTests.cs` *(extendido)*
- **Comportamiento**: (1) Botón "Personas" visible en vista activas entre Cargos y Editar. (2) Botón oculto en vista eliminadas. (3) `href` incluye `id` de habilidad. (4) `BuildPersonasRouteValues` preserva `page`, `search`, `sort`, `status`.
- **Spec**: REQ-HLD-NEW, REQ-HLD-NEW-VISIBILITY, REQ-HLD-NEW-POSITION
- **Verify**: `dotnet test --filter "FullyQualifiedName~HabilidadesIndexPage_PersonasButton"` → **N PASS**
- **Dependencias**: C.4

### Task C.6 — GREEN: helper + botón en Habilidades/Index
- **Files**: `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml` *(modificado)*, `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml.cs` *(modificado)*
- **Comportamiento**: Helper `BuildPersonasRouteValues(id)` centraliza `id`, `p`, `search`, `sort`, `status`. Botón `ti ti-users` `btn-primary btn-icon btn-sm rounded-circle` entre Cargos y Editar, gated con `!Model.IsDeletedView`.
- **Spec**: REQ-HLD-NEW, REQ-HLD-NEW-VISIBILITY, REQ-HLD-NEW-POSITION
- **Verify**: `dotnet test --filter "FullyQualifiedName~HabilidadesIndexPage_PersonasButton"` → **4 PASS**
- **Dependencias**: C.5

### Task C.7 — VERIFY: build + suite completa + bun build
- **Files**: ninguno nuevo
- **Comportamiento**: `dotnet build SGV.slnx` sin errores. Suite completa del PR C verde. `bun run build` en `src/SGV.Web` verde (assets Inspinia). Sin cambios fuera de alcance.
- **Verify**: `dotnet build SGV.slnx && dotnet test --filter "FullyQualifiedName~HabilidadesPersonasPage\|HabilidadApiClientTests\|HabilidadesIndexPage_PersonasButton" && cd src/SGV.Web && bun run build`
- **Dependencias**: C.4, C.6

---

## Resumen de tareas

| PR | Tareas | Líneas est. | Specs cubiertas |
|----|--------|------------|-----------------|
| PR A | A.1–A.3 (3 tasks) | 50–80 | REQ-PM-NEW, REQ-PM-NEW-ADMIN, REQ-PM-NEW-POSITION, REQ-PM-NEW-CONTEXT |
| PR B | B.1–B.11 (11 tasks) | 400–500 | REQ-SPQC-01..07 |
| PR C | C.1–C.7 (7 tasks) | 400–500 | REQ-HM-NEW-PAGE, REQ-HM-NEW-AUTH, REQ-HM-NEW-READONLY, REQ-HM-NEW-LINK, REQ-HLD-NEW, REQ-HLD-NEW-VISIBILITY, REQ-HLD-NEW-POSITION |
| **Total** | **21 tasks** | **850–1080** | — |

## Rollback boundaries

| PR | Revertir exactamente |
|----|---------------------|
| **PR A** | `git revert` de cambios en `Pages/Personas/Index.cshtml`, `Index.cshtml.cs` y `tests/Web/Persona/PersonasIndexPageTests.cs`. Sin impacto en otras rutas. |
| **PR B** | `git revert` de cambios en `SGV.Contracts/Habilidades/Consultas/Dtos/`, `SGV.Aplicacion/Habilidades/Consultas/`, `SGV.Infraestructura/Persistencia/Repositorios/SkillPersonaRepository.cs`, `SGV.Api/Controllers/SkillsController.cs`, `SGV.Infraestructura/DependencyInjection.cs`, `SGV.Aplicacion/DependencyInjection.cs`, `SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs`, `tests/SGV.Tests/` (contracts + api + aplicacion). Sin migraciones. |
| **PR C** | `git revert` de cambios en `Pages/Organizacion/Habilidades/Personas.cshtml*`, `Habilidades/Index.cshtml`(+cs), `HabilidadApiClient.cs`, `FakeHabilidadApiClient.cs`, `tests/SGV.Tests/Web/Habilidad/`. La firma `GetPersonasAsync` queda sin consumidor pero no rompe build. |
