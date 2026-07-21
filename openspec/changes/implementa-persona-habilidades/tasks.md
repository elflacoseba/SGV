# Tasks: Implementa Persona-Habilidades

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 500–730 total (4 slices) |
| 400-line budget risk | Low (cada slice <400) |
| Chained PRs recommended | Yes — 4 PRs stacked-to-main |
| Suggested split | Slice 1 → Slice 2 → Slice 3a → Slice 3b |
| Delivery strategy | ask-always |
| Chain strategy | stacked-to-main |

```text
Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: Low
```

### Suggested Work Units (PR slices)

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Migrar wire-types PersonaSkill a Contracts + consolidar ErrorCategoria | PR 1 | `dotnet test --filter "FullyQualifiedName~PersonaSkillContracts"` | `dotnet build SGV.slnx` | Revertir archivos nuevos en Contracts; restaurar archivos borrados en Aplicación |
| 2 | Extender IPersonaApiClient y FakePersonaApiClient con skills | PR 2 | `dotnet test --filter "FullyQualifiedName~PersonaSkillClient"` | `dotnet build SGV.slnx` | Revertir cambios en `src/SGV.Web/Integration/Personas/` y tests |
| 3a | PersonaHabilidades PageModel GET + autorización + view + antiforgery | PR 3 | `dotnet test --filter "FullyQualifiedName~PersonaHabilidadesPage_Anon\|PersonaHabilidadesPage_Get"` | `dotnet build SGV.slnx` | Revertir `Pages/Personas/PersonaHabilidades.cshtml*` y tests auth/GET |
| 3b | Handlers POST + PRG + Details enlace + tests integración web + bun build | PR 4 | `dotnet test --filter "FullyQualifiedName~PersonaHabilidadesPage_Post"` | `dotnet test SGV.slnx && bun run build` en `src/SGV.Web` | Revertir handlers POST, Details enlace, tests integración |

---

## Slice 1 — Migrar wire-types PersonaSkill a SGV.Contracts + ErrorCategoria

*(Sin cambios respecto a la versión original — 7 tareas)*

### 1.1 — RED: test contratos PersonaSkill existen en Contracts namespace
- **Files**: `tests/SGV.Tests/Contracts/Personas/PersonaSkillContractsCompatibilityTests.cs` *(creado en commit `d34b0d0`)*
- **Comportamiento**: Verifica `PersonaSkillCommandResult`, `PersonaSkillError`, `PersonaSkillDeleteResult`, `AsignarPersonaSkillRequest` existen en `SGV.Contracts.Personas.Comandos`. (REQ-TAXO-01, REQ-TAXO-03)
- **Verify**: `dotnet test --filter "FullyQualifiedName~PersonaSkillContracts"` → **9 PASS**
- **Dependencias**: ninguna

### 1.2 — RED: test mapeo ErrorCategoria NotFound/Validation en ApiResults
- **Files**: `tests/SGV.Tests/Api/PersonaSkillErrorCategoriaMappingTests.cs` *(creado en commit `d34b0d0`)*
- **Comportamiento**: `NotFound` → `ErrorCategoria.NotFound` (404), `NivelHabilidadNoExiste` → `ErrorCategoria.Validation` (400). (REQ-TAXO-02, SCENARIO-01/02)
- **Verify**: `dotnet test --filter "FullyQualifiedName~PersonaSkillErrorCategoria"` → **6 PASS**
- **Dependencias**: ninguna

### 1.3 — RED: test deserialización JSON preserva wire shape
- **Files**: `tests/SGV.Tests/Web/Persona/PersonaSkillJsonCompatibilityTests.cs` *(creado en commit `d34b0d0`)*
- **Comportamiento**: Verifica nombres JSON `skillId`/`nivelId` (Dto) y `skill`/`nivel` (DetailDto) se preservan tras migrar. (REQ-TAXO-01, SCENARIO-01)
- **Verify**: `dotnet test --filter "FullyQualifiedName~PersonaSkillJson"` → **6 PASS**
- **Dependencias**: ninguna

### 1.4 — GREEN: crear tipos PersonaSkill en SGV.Contracts.Personas
- **Files** (nuevos, commit `ce485d4`):
  - `src/SGV.Contracts/Personas/Comandos/PersonaSkillCommandResult.cs` — `PersonaSkillError` con `Categoria: ErrorCategoria`, `StatusCode: int?`
  - `src/SGV.Contracts/Personas/Comandos/PersonaSkillRequests.cs` — `AsignarPersonaSkillRequest` (Guid NivelId)
  - `src/SGV.Contracts/Personas/Comandos/PersonaSkillDeleteResult.cs` — nuevo, shape espejo `CargoSkillDeleteResult`
  - `src/SGV.Contracts/Personas/Consultas/Dtos/PersonaSkillDto.cs`
  - `src/SGV.Contracts/Personas/Consultas/Dtos/PersonaSkillDetailDto.cs` — `HabilidadDto Skill, NivelHabilidadDto Nivel` (rename detectado por git)
- **Comportamiento**: Records públicos que reemplazan los de Aplicacion. (REQ-TAXO-01, REQ-TAXO-03)
- **Verify**: `dotnet build SGV.slnx` → **0 errors**
- **Dependencias**: 1.1, 1.2, 1.3

### 1.5 — GREEN: migrar ApiResults a usar Contracts + ErrorCategoria
- **Files**: `src/SGV.Api/Infrastructure/Results/ApiResults.cs`, `src/SGV.Contracts/Comun/ErrorCategoriaMappers.cs` *(commits `ce485d4`)*
- **Comportamiento**: Reemplazado `using SGV.Aplicacion.Personas.Comandos` por `using SGV.Contracts.Personas.Comandos`. Añadidos mappers `ToCategoria(PersonaSkillErrorType)` y `ToTipoPersonaSkill(ErrorCategoria)` y eliminado el switch privado duplicado. (REQ-TAXO-02)
- **Verify**: `dotnet build src/SGV.Api && dotnet test --filter "FullyQualifiedName~PersonaSkillErrorCategoria"` → **6 PASS**
- **Dependencias**: 1.4

### 1.6 — GREEN: actualizar usings en Aplicación, Infraestructura y Controller
- **Files**: `src/SGV.Aplicacion/Personas/Comandos/PersonaSkillServicio.cs`, `IPersonaSkillServicio.cs`, `src/SGV.Aplicacion/Personas/Consultas/IPersonaSkillRepository.cs`, `src/SGV.Infraestructura/Persistencia/Repositorios/PersonaSkillRepository.cs`, `src/SGV.Api/Controllers/PersonasController.cs`, `tests/SGV.Tests/Api/PersonaSkillControllerTests.cs`, `tests/SGV.Tests/Api/PersonasControllerTests.cs`, `tests/SGV.Tests/Aplicacion/Personas/PersonaSkillServicioTests.cs` *(todos modificados en commit `ce485d4`)*
- **Comportamiento**: Actualizados `using` a `SGV.Contracts.Personas.*`. Sin cambio de lógica. (REQ-TAXO-01)
- **Verify**: `dotnet build SGV.slnx` → **0 errors**
- **Dependencias**: 1.4

### 1.7 — Eliminar fuentes duplicadas de Aplicación
- **Files** (eliminados en commit `ce485d4`): `src/SGV.Aplicacion/Personas/Comandos/PersonaSkillCommandResult.cs`, `PersonaSkillRequests.cs`, `Consultas/Dtos/PersonaSkillDto.cs`, `PersonaSkillDetailDto.cs`
- **Comportamiento**: Build compila sin duplicados; rename detectado preserva la historia del `PersonaSkillDetailDto.cs`. (REQ-TAXO-01)
- **Verify**: `dotnet build SGV.slnx && dotnet test SGV.slnx` → **2,705 tests PASS**
- **Dependencias**: 1.6

---

## Slice 2 — Extender IPersonaApiClient con consulta/upsert/baja

*(Sin cambios respecto a la versión original — 5 tareas)*

### 2.1 — RED: test fake registra invocaciones de skill methods
- **Files**: `tests/SGV.Tests/Web/Persona/PersonaSkillClientContractTests.cs`
- **Comportamiento**: `FakePersonaApiClient.GetSkillsAsync`/`UpsertSkillAsync`/`DeleteSkillAsync` incrementan contadores sin HTTP. (REQ-WEB-04, SCENARIO-01)
- **Verify**: `dotnet test --filter "FullyQualifiedName~PersonaSkillClientContract"`
- **Dependencias**: 1.7

### 2.2 — RED: test errores NotFound/Validation/Transport en cliente
- **Files**: `tests/SGV.Tests/Web/Persona/PersonaApiClientSkillErrorsTests.cs`
- **Comportamiento**: `FakePersonaApiClient` con errores → `Categoria` via `CommandResultMapper`/`DeleteResultMapper`. (REQ-WEB-05, SCENARIO-01/02, REQ-TAXO-02)
- **Verify**: `dotnet test --filter "FullyQualifiedName~PersonaApiClientSkillErrors"`
- **Dependencias**: 2.1

### 2.3 — GREEN: agregar métodos a IPersonaApiClient
- **Files**: `src/SGV.Web/Integration/Personas/IPersonaApiClient.cs`
- **Comportamiento**: `GetSkillsAsync`, `UpsertSkillAsync`, `DeleteSkillAsync`. (REQ-WEB-04, REQ-WEB-05)
- **Verify**: `dotnet build src/SGV.Web`
- **Dependencias**: 1.4

### 2.4 — GREEN: implementar métodos en PersonaApiClient
- **Files**: `src/SGV.Web/Integration/Personas/PersonaApiClient.cs`
- **Comportamiento**: `GET/PUT/DELETE /api/v1/personas/{personaId}/skills/{skillId}`. Delegar errores en `CommandResultMapper`/`DeleteResultMapper`. (Patrón CargoApiClient)
- **Verify**: `dotnet build src/SGV.Web`
- **Dependencias**: 2.3

### 2.5 — GREEN: extender FakePersonaApiClient con skill methods
- **Files**: `tests/SGV.Tests/Web/Persona/FakePersonaApiClient.cs`
- **Comportamiento**: Propiedades `GetSkillsResult`, `UpsertSkillResult`, `DeleteSkillResult` + contadores. Seed configurable. Sin HTTP. (REQ-WEB-04, SCENARIO-01)
- **Verify**: `dotnet test --filter "FullyQualifiedName~FakePersonaApiClient"`
- **Dependencias**: 2.3, 2.4

---

## Slice 3a — PersonaHabilidades PageModel GET + autorización + view + antiforgery

### 3a.1 — RED: test autorización admin-only
- **Files**: `tests/SGV.Tests/Web/Persona/PersonaHabilidadesPageTests.cs`
- **Comportamiento**: `GET /personas/{id}/habilidades` exige rol Administrador; anónimo redirect a sign-in; autenticado sin rol recibe 403. (REQ-WEB-01, SCENARIO-01)
- **Verify**: `dotnet test --filter "FullyQualifiedName~PersonaHabilidadesPage_Anon"`
- **Dependencias**: 2.5 (Fake con skills)

### 3a.2 — RED: test GET carga persona y grilla
- **Files**: `tests/SGV.Tests/Web/Persona/PersonaHabilidadesPageTests.cs`
- **Comportamiento**: GET carga nombre de persona + lista de skills desde fake. Persona inactiva redirige a estado recoverable (no 404). (REQ-WEB-02, REQ-WEB-03, SCENARIO-02/03)
- **Verify**: `dotnet test --filter "FullyQualifiedName~PersonaHabilidadesPage_Get"`
- **Dependencias**: 3a.1

### 3a.3 — GREEN: crear PersonaHabilidades.cshtml.cs (PageModel GET)
- **Files**: `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml.cs` (nuevo)
- **Comportamiento**: `[Authorize(Roles = RolesSgv.Administrador)]`, gate admin manual en `OnGetAsync`, carga `IPersonaApiClient.GetSkillsAsync`, persona inactiva → `IsRecoverable` + estado recuperable. Sin handlers POST. Sin Ponderacion/EsObligatoria. Antiforgery configurado (formulario con `@Html.AntiForgeryToken()`). (REQ-WEB-01, REQ-WEB-02, REQ-WEB-03)
- **Verify**: `dotnet build src/SGV.Web`
- **Dependencias**: 2.4 (ApiClient), 3a.2 (tests en rojo)

### 3a.4 — GREEN: crear PersonaHabilidades.cshtml (View)
- **Files**: `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml` (nuevo)
- **Comportamiento**: Vista Razor con grilla de skills (inspiración `CargoHabilidades.cshtml` pero más simple: sin Ponderacion/Obligatoria), estado vacío, estado recuperable, acceso denegado, `TempData` feedback section, look Inspinia. Formulario "Asignar" preparado (solo `NivelHabilidadId` selector). Sin handlers POST aún. (REQ-WEB-02)
- **Verify**: `dotnet build src/SGV.Web`
- **Dependencias**: 3a.3

### 3a.5 — Verify slice 3a
- **Files**: todos los del slice
- **Comportamiento**: Tests auth + GET pasan. Build completo. (REQ-WEB-01..03)
- **Verify**: `dotnet build SGV.slnx && dotnet test --filter "FullyQualifiedName~PersonaHabilidadesPage_Anon|PersonaHabilidadesPage_Get"`
- **Dependencias**: 3a.4

---

## Slice 3b — Handlers POST + PRG + Details enlace + tests integración web + bun build

### 3b.1 — RED: test handlers POST upsert/delete con PRG
- **Files**: `tests/SGV.Tests/Web/Persona/PersonaHabilidadesPageTests.cs`
- **Comportamiento**: `OnPostAsignar` y `OnPostQuitar` invocan cliente, redirigen con PRG, `TempData` refleja éxito. Fallan antes de implementar handlers. (REQ-WEB-02, SCENARIO-02/03)
- **Verify**: `dotnet test --filter "FullyQualifiedName~PersonaHabilidadesPage_Post_Init"`
- **Dependencias**: 3a.5 (page model scaffold existente)

### 3b.2 — RED: test POST persona inactiva bloquea mutación
- **Files**: `tests/SGV.Tests/Web/Persona/PersonaHabilidadesPageTests.cs`
- **Comportamiento**: POST con persona inactiva no invoca cliente; redirige sin mutar. (REQ-WEB-05, SCENARIO-02/03)
- **Verify**: `dotnet test --filter "FullyQualifiedName~PersonaHabilidadesPage_Post_Inactive"`
- **Dependencias**: 3b.1

### 3b.3 — GREEN: agregar handlers POST al PageModel
- **Files**: `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml.cs`
- **Comportamiento**: `OnPostAsignarAsync` y `OnPostQuitarAsync` con PRG, `TempData` feedback (`StatusMessage`/`StatusKind` via `PageFeedback`), gateo admin. Sin Ponderacion/EsObligatoria. (REQ-WEB-02, REQ-WEB-05)
- **Verify**: `dotnet build src/SGV.Web`
- **Dependencias**: 3b.2 (tests en rojo)

### 3b.4 — GREEN: tests integración web con WebApplicationFactory
- **Files**: `tests/SGV.Tests/Web/Persona/PersonaHabilidadesIntegrationTests.cs` (nuevo)
- **Comportamiento**: Tests integración vía `PersonaWebTestFixture`/`WebIntegrationFixture`: handlers POST con antiforgery real, redirección PRG, gateo admin, persona inactiva bloquea, feedback `TempData` legible. (REQ-WEB-01..05, SCENARIO-01/02/03)
- **Verify**: `dotnet test --filter "FullyQualifiedName~PersonaHabilidadesIntegration"`
- **Dependencias**: 3b.3

### 3b.5 — GREEN: enlace Habilidades desde Details.cshtml
- **Files**: `src/SGV.Web/Pages/Personas/Details.cshtml`, `src/SGV.Web/Pages/Personas/Details.cshtml.cs` (si requiere exponer `EsAdministrador`)
- **Comportamiento**: Botón "Habilidades" con `ti-stars` en barra inferior de Details, solo para persona activa y admin. Enlace a `/Personas/PersonaHabilidades?id=...`. Oculto si `IsNotFound`. (REQ-WEB-06, REQ-PM-01, SCENARIO-01/02)
- **Verify**: `dotnet build src/SGV.Web`
- **Dependencias**: 3b.3

### 3b.6 — Verify final slice 3b
- **Files**: todos los del cambio
- **Comportamiento**: Suite completa pasa 3 veces consecutivas con `--no-build`. `bun run build` en `src/SGV.Web` pasa. (REQ-WEB-01..06)
- **Verify**: `dotnet build SGV.slnx && dotnet test SGV.slnx && bun run build` (en `src/SGV.Web`)
- **Dependencias**: 3b.4, 3b.5

---

## Work unit commits sugeridos

### Slice 1 (3 commits):
1. `test(slice1): add contract existence and JSON deserialization tests for PersonaSkill* in Contracts` (1.1-1.3)
2. `refactor(slice1): move PersonaSkill wire-types from Aplicacion to Contracts.Personas` (1.4-1.6)
3. `refactor(slice1): remove duplicate PersonaSkill sources from Aplicacion` (1.7)

### Slice 2 (2 commits):
1. `test(slice2): add fake contract tests and error mapping tests for PersonaSkill client methods` (2.1-2.2)
2. `feat(slice2): extend IPersonaApiClient and PersonaApiClient with PersonaSkill methods` (2.3-2.5)

### Slice 3a (2 commits):
1. `test(slice3a): add authorization and GET integration tests for PersonaHabilidades page` (3a.1-3a.2)
2. `feat(slice3a): create PersonaHabilidades Razor Page with GET handler and Inspinia view` (3a.3-3a.4)

### Slice 3b (3 commits):
1. `test(slice3b): add POST handler unit tests for PersonaHabilidades (upsert, delete, inactive gate)` (3b.1-3b.2)
2. `feat(slice3b): implement POST handlers on PersonaHabilidades page with PRG and TempData` (3b.3)
3. `feat(slice3b): add integration tests, Details navigation link, and final verify` (3b.4-3b.6)

---

## Validación de forecast por slice

| Slice | Archivos | Líneas estimadas | ¿Excede 400? |
|-------|----------|-----------------|--------------|
| 1 | Contracts (4 new), Aplicación (6 modify/delete), Api (1 modify), Tests (3 new) | 160–280 | No |
| 2 | Integration (2 modify), Fakes (1 modify), Tests (2 new) | 195–290 | No |
| 3a | Razor (2 new), Tests (1 new/modify: auth + GET) | 220–310 | No |
| 3b | PageModel (modify), Details (2 modify), Tests (2 new/modify: POST + integración) | 245–325 | No |
| **Total** | ~20 archivos | 820–1205 | — |

Todos los slices estiman <400 líneas. Sin EXCEEDS_BUDGET.

---

## Riesgos identificados

| Riesgo | Severidad | Mitigación |
|--------|-----------|------------|
| **3b.4 tests integración olvidan antiforgery** | MEDIUM | Incluir `ExtractAntiforgeryTokenAsync` en test; la página renderiza `@Html.AntiForgeryToken()` ya en 3a.4 |
| **Drift JSON (skillId/nivelId planos vs nested)** | MEDIUM | Test deserialización explícito en Slice 1 (1.3) |
| **Migración incompleta (referencias a Aplicación)** | HIGH | Build + test compilación como guard; tareas 1.5-1.7 validan atómicamente |
| **Persona inactiva bypass UI** | MEDIUM | PageModel gatea en GET (3a.3) y POST (3b.3); tests cubren ambos |
| **3b depende de 3a mergeado** | LOW | Stacked-to-main: PR3 mergea antes de crear rama PR4 |

---

## Orden de merges (stacked-to-main)

1. **PR 1** (Slice 1) → `main` → rama para PR 2
2. **PR 2** (Slice 2) → `main` → rama para PR 3
3. **PR 3** (Slice 3a) → `main` → rama para PR 4
4. **PR 4** (Slice 3b) → `main`

Sin rebase intermedio. Sin feature branch acumulativa.

---

## Out of scope (excluido de tasks)

- Cambios en dominio, persistencia, migraciones EF, endpoints API o catálogos GUID
- `VerificadoAt`, `Fuente` (diferidos)
- `Ponderacion`, `EsObligatoria`, `NivelRequeridoId` (exclusivos de Cargo)
- `PersonaSkillErrorType` público (eliminado)
- Coverage 100% o tests de DTOs/constructores triviales

---

## Decisiones respetadas (congeladas, ver mem #1284)

- ✅ VerificadoAt/Fuente → diferidos
- ✅ Acceso → solo rol Administrador (lectura y escritura)
- ✅ Persona inactiva → bloquear gestión (estado recoverable en GET y POST)
- ✅ Errores → adoptar ErrorCategoria, eliminar PersonaSkillErrorType público
