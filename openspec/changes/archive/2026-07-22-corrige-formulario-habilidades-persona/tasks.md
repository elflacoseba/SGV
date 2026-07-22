# Tasks: Corrige el formulario de habilidades de una persona

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 110–175 |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | ask-always |
| Chain strategy | pending |

```text
Decision needed before apply: Yes
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Low
```

## T1 — ViewModel: `HabilidadesDisponibles` + `NivelOptions`
- [ ] 1.1 — Extender `PersonaHabilidadesViewModel` (L276-305) con dos propiedades `init` default `[]`: `IReadOnlyList<HabilidadListItemViewModel> HabilidadesDisponibles` y `IReadOnlyList<NivelHabilidadDto> NivelOptions`.
- [ ] 1.2 — Agregar overload `From(persona, skills, habilidades, niveles)` al ViewModel (la 1-arg existente no se toca).
- **Modifica**: `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml.cs` (ViewModel)
- **Verifica**: `dotnet build src/SGV.Web` → 0 errors
- **Cubre**: REQ-VM-01, Scenario "ViewModel expone colecciones pobladas"

## T2 — DI: Inyectar `IHabilidadApiClient` + helper `LoadCatalogsAsync`
- [ ] 2.1 — Agregar `IHabilidadApiClient` como 2do parámetro del constructor primario (después de `IPersonaApiClient`, antes de `ILogger`). Exponerlo como `internal IHabilidadApiClient HabilidadApiClient`.
- [ ] 2.2 — Agregar helper `internal async Task LoadCatalogsAsync(Guid id, CancellationToken ct)` que invoque `Task.WhenAll(GetAllAsync, GetNivelesHabilidadAsync)` con `TransportFailureClassifier.IsTransportFailure(ex)` y deje colecciones vacías si fallan.
- **Modifica**: `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml.cs`
- **Verifica**: `dotnet build src/SGV.Web` → 0 errors (el constructor cambia firma)
- **Cubre**: REQ-01, Scenario "GET invoca los tres clientes en paralelo"

## T3 — GET handler: poblar catálogos
- [ ] 3.1 — En `OnGetAsync`, después de cargar skills, llamar a `LoadCatalogsAsync(id, ct)` y mapear `GetAllAsync` → `HabilidadListItemViewModel(Id, Codigo, Nombre, Descripcion, Categoria)` via `Select`.
- [ ] 3.2 — En `ReloadAfterFailedAsignarAsync`, invocar `LoadCatalogsAsync` tras recargar persona/skills para que el re-render tras POST inválido también pinte los selects.
- **Modifica**: `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml.cs`
- **Verifica**: `dotnet test --filter "FullyQualifiedName~PersonaHabilidadesPage"` (tests existentes + nuevos)
- **Cubre**: REQ-01, REQ-04 ("POST inválido recarga catálogos")

## T4 — Vista Razor: iterar colecciones en `<select>`
- [ ] 4.1 — En `<select id="SkillId">`, agregar `foreach` sobre `Model.ViewModel.HabilidadesDisponibles` generando `<option value="@item.Id">@item.Nombre</option>` después del placeholder.
- [ ] 4.2 — En `<select id="NivelHabilidadId">`, igual con `Model.ViewModel.NivelOptions`.
- **Modifica**: `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml`
- **Verifica**: `dotnet build SGV.slnx` → 0 errors
- **Cubre**: REQ-02, Scenarios "Select de habilidad lista N+1 options" y "Select de nivel lista M+1 options"

## T5 — Extender test infrastructure
- [ ] 5.1 — Extender `WebIntegrationFixture.CreatePersonaLeaseAsync` con parámetro opcional `IHabilidadApiClient? habilidad = null` (default `new FakeHabilidadApiClient()`, espejo de `CreateCargoLeaseAsync`).
- [ ] 5.2 — En `PersonaHabilidadesPageTests.CreatePage()`, agregar `FakeHabilidadApiClient` como 2do parámetro (default vacío) y actualizar las 2 factories privadas (`CreatePage`, `CreatePostPage`) para que todas las llamadas existentes sigan compilando.
- **Modifica**: `tests/SGV.Tests/Web/Collections/WebIntegrationFixture.cs`, `tests/SGV.Tests/Web/Persona/PersonaHabilidadesPageTests.cs`
- **Verifica**: `dotnet test --filter "FullyQualifiedName~PersonaHabilidadesPage"` → 23 PASS existentes
- **Cubre**: infraestructura de test para REQ-01/02/05

## T6 — Tests unitarios: GET popula catálogos + degradación
- [ ] 6.1 — `OnGet_PopulatesCatalogsFromHabilidadApiClient`: GET con seeds de habilidad y nivel → `HabilidadesDisponibles.Count == N`, `NivelOptions.Count == M`, se invocaron `GetAllAsync` y `GetNivelesHabilidadAsync`.
- [ ] 6.2 — `OnGet_HabilidadApiClientTransportFailure_LeavesCatalogsEmpty`: GET con `HttpRequestException` en catálogo → colecciones vacías, `IsRecoverable=true`, grilla hidratada.
- [ ] 6.3 — `OnPostAsignar_ModelStateInvalid_AlsoReloadsCatalogs`: POST inválido → re-render con catálogos poblados.
- **Modifica**: `tests/SGV.Tests/Web/Persona/PersonaHabilidadesPageTests.cs`
- **Verifica**: `dotnet test --filter "FullyQualifiedName~PersonaHabilidadesPage"` → nuevos tests PASS
- **Cubre**: REQ-01 (Scenario falla transporte), REQ-04, REQ-05 (degradación)

## T7 — Verify final
- [ ] 7.1 — Build + suite completa: `dotnet build SGV.slnx` → 0 errors; `dotnet test SGV.slnx` → PASS.
- **Verifica**: `dotnet test SGV.slnx`
- **Cubre**: REQ-ALL
