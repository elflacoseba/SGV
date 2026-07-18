# Tasks: Card enriquecida de Persona en detalle de Usuarios

> Strict TDD (rojo → verde → run). Change puramente de presentación; no toca contrato HTTP ni flujo de acciones.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| PR estimado | ~150-200 líneas |
| 400-line budget risk | Bajo (entra holgado) |
| Chained PRs recommended | No |
| Delivery strategy | single-pr |
| Decision needed before apply | No |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Low

---

### T-01 [RED] Tests rojos: happy path enriquecido + ausencia de controles

- Agregar overload `BuildUsuario(string id, Guid personaId)` en `tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs` (espejo de `EditPageTests.cs` L569).
- **Test 1**: `Get_Details_WhenPersonaApiReturnsDto_RendersEnrichedCard` — `FakePersonaApiClient.WithPersonaList(dto)` con DTO completo (`Apellidos, Nombres, Legajo, TipoDocumento, NumeroDocumento, Email, Telefono, IsActive=true`); `BuildUsuario("u-1", personaId)`; asserta `data-usuario-persona-card`, `L-7777`, `DNI 30123456`, email, teléfono, badge `Activa`, `<a href="/personas/detalle/{pid}">` como título.
- **Test 4**: `Get_Details_NoControlesSeleccionPersona` — assert `DoesNotContain` para `data-usuario-persona-quitar`, `data-usuario-persona-buscar`, `usuario-persona-buscador-modal`.
- **Verificación**: `dotnet test --filter "FullyQualifiedName~DetailsPageTests"` → falla (HTML no tiene card aún). Los dos tests NO compilan por falta del overload inicial.

### T-02 [GREEN-IMPL] Helper `TryLoadPersonaVinculadaAsync` + props en DetailsModel

- `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml.cs`:
  - Agregar using `SGV.Contracts.Personas.Consultas.Dtos` y `SGV.Web.Integration.Personas`.
  - Insertar `IPersonaApiClient personaApiClient` en primary constructor.
  - Agregar `PersonaDto? PersonaVinculada { get; private set; }` y `string? PersonaDisplay { get; private set; }`.
  - Agregar `{static} FormatPersonaDisplay(string?, string?)` y `{private} TryLoadPersonaVinculadaAsync(Guid, CancellationToken)` como espejo de `Edit.cshtml.cs` L205-229 (mismo `Guid.Empty`, `TransportFailureClassifier`, `LogWarning`).
  - En `OnGetAsync` tras `GetByIdAsync` exitoso: setear `PersonaDisplay = FormatPersonaDisplay(...)` y llamar `TryLoadPersonaVinculadaAsync(Usuario.PersonaId, ...)`.
  - NO tocar `IsNotFound`, `BuildIndexUrl`/`BuildEditUrl`, ni handlers.

### T-03 [RUN] Validar T-01 + T-02 verdes (happy path)

- `dotnet test --filter "FullyQualifiedName~DetailsPageTests"` → verde.
- Fix iterativo si falla. NO avanzar hasta verde.

### T-04 [RED] Tests rojos: fallbacks 404 y transporte

- Agregar `GetByIdException` property a `tests/SGV.Tests/Web/Persona/FakePersonaApiClient.cs` y lanzarla en `GetByIdAsync` antes de la lógica actual. Patrón ya existe (`QueryException`, `CreateException`, etc.).
- **Test 2**: `Get_Details_WhenPersonaApiReturns404_FallsBackToPlainDisplay` — `FakePersonaApiClient` vacío (sin DTOS); assert fallback plano con `data-usuario-details-persona`, ausencia de `data-usuario-persona-card`, NO assert sobre `no está disponible`.
- **Test 3**: `Get_Details_WhenPersonaApiThrowsTransport_FallsBackWithoutIsNotFound` — `GetByIdException = new HttpRequestException("upstream")` en el fake; assert fallback plano + `DoesNotContain("no está disponible")`.
- **Verificación**: `dotnet test --filter "FullyQualifiedName~DetailsPageTests"` → falla (fallback no implementado).

### T-05 [GREEN-IMPL] Card read-only + fallback plano en Details.cshtml

- Reemplazar `Details.cshtml` L78-81:
  - **Enriquecida**: `<div class="card border mb-0" data-usuario-persona-card>` con `card-body`, `dl.row.mb-0`, `dt.col-sm-3`/`dd.col-sm-9` para Documento/Email/Teléfono/Estado (badge `Activa`/`Inactiva`). Título = `<a href="/personas/detalle/@Model.Usuario.PersonaId">` clickable. SIN `data-usuario-persona-quitar`, SIN `data-usuario-persona-buscar`, SIN modal. Agregar `@functions { FormatDocumento(PersonaDto?) }` espejo de `_Form.cshtml` L198-226.
  - **Fallback**: `<div class="card-body py-2" data-usuario-details-persona>` con `<a href="...">@Model.PersonaDisplay</a>`. Sin `data-usuario-persona-card`.
- NO modificar `_Form.cshtml`, `Index.cshtml`, `usuarios-index.js`, ni los forms/scripts de Details.

### T-06 [RUN] Validar suite completa + build

- `dotnet test --filter "FullyQualifiedName~DetailsPageTests"` → verde.
- `dotnet test --filter "FullyQualifiedName~Usuarios"` → sin regresiones.
- `dotnet build SGV.slnx` → verde, sin warnings nuevos.
- NO avanzar si falla.

### T-07 [COMMIT] Commit productivo

- Branch: `feat/detalles-usuario-persona-enriched-card` desde `develop`.
- Stage: `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml`, `Details.cshtml.cs`, `tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs`, `tests/SGV.Tests/Web/Persona/FakePersonaApiClient.cs`.
- Mensaje: `feat(web): persona enriched card on usuario detail`.
- NO stagear `openspec/`. NO agregar `Co-Authored-By`.

### T-08 [CHORE-SDD] Commit artefactos SDD

- `git add openspec/changes/2026-07-17-detalles-usuario-persona-enriched-card/`.
- Mensaje: `chore(sdd): add artifacts for 2026-07-17-detalles-usuario-persona-enriched-card`.
- NO incluir archivos productivos.

### T-09 [PR] Crear PR único

- `gh pr create` con base `develop`, título `feat(web): persona enriched card on usuario detail`.
- Body: referencia PR #168 como antecedente, lista los 4 escenarios del spec cubiertos, mención rollback trivial.
- NO incluir artefactos SDD ni decisiones operativas extra.
