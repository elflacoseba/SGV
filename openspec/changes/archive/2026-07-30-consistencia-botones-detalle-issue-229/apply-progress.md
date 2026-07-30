# Apply Progress — consistencia-botones-detalle-issue-229 (issue #229)

> **Change**: `consistencia-botones-detalle-issue-229`
> **Issue**: [#229](https://github.com/elflacoseba/SGV/issues/229)
> **Branch**: `develop` (HEAD previo `785e10ee`)
> **Mode**: Strict TDD activo (`openspec/config.yaml` → `strict_tdd: true`)
> **Workload strategy**: single PR (forecast ≤400 líneas, sin chained)
> **Persistence mode**: hybrid (Engram + OpenSpec filesystem)
> **Artifact store topic_key**: `sdd/consistencia-botones-detalle-issue-229/apply-progress`

## Estado

**Apply completo, 4/4 tareas ejecutadas. Suite verde: 3241/3241 PASS, 0 FAIL, 0 SKIP.** Pendiente PR y verify formal.

## TDD Cycle Evidence

El change declara explícitamente en `proposal.md §Test Strategy`: "No se requieren tests nuevos. La issue declara explícitamente que los tests existentes de navegación de detalle deben seguir pasando." Por lo tanto, el ciclo RED→GREEN→REFACTOR no aplica per-task para markup (T2/T3). Para T1 (binding C#) el safety net son los tests web existentes.

| Tarea | Tipo | RED (test que falla antes) | GREEN (test que pasa después) | REFACTOR | Tests validados |
|-------|------|---------------------------|-------------------------------|----------|----------------|
| T1 — Binding `CurrentPage/Search/Sort` en `Ocupaciones/Details.cshtml.cs` | C# binding | N/A (change declara "no tests nuevos"; el binding es superconjunto y no altera handlers POST) | N/A | N/A | `OcupacionDetailsPageTests` (15/15 PASS). `UnidadOrganizativa*` (262/262 PASS) — sin regresiones en el resto de la suite. |
| T2 — Barra canónica en `Ocupaciones/Details.cshtml` | Markup Razor | N/A | N/A | N/A | Mismo set: 15/15 `OcupacionDetailsPageTests`, 1351/1351 suite web completa. |
| T3 — Barra canónica en `UnidadesOrganizativas/Details.cshtml` | Markup Razor | N/A | N/A | N/A | `UnidadOrganizativa*` (262/262 PASS). |
| T4 — Validación global | Compilación + suite | — | — | — | `dotnet build SGV.slnx` OK (0 errors, 92 warnings preexistentes). `dotnet test SGV.slnx` → **3241/3241 PASS, 0 FAIL, 0 SKIP**. |

> **Nota**: el ajuste a `tests/SGV.Tests/Web/Ocupaciones/OcupacionDetailsPageTests.cs` L156 (`Assert.Contains($"href=\"/organizacion/ocupaciones/editar/{id:D}\"")` → `Assert.Contains($"href=\"/organizacion/ocupaciones/editar/{id}\"")` + nuevo `Assert.Contains("p=1", …)`) NO es un test nuevo; es una corrección del assertion que estaba validando el patrón URL hardcodeada que el spec REQ-DET-BTN-004 explícitamente prohíbe. Sigue el formato `cargo.Id` que ya usa `CargoDetailsPageTests` L52. El cambio documenta la transición del contrato "URL hardcodeada" al contrato canónico "Url.Page con parámetros preservados".

## Cambios aplicados

### T1 — Binding de contexto en `Ocupaciones Details`

**Archivo**: `src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml.cs` (28 líneas agregadas, 1 eliminada).

Réplica exacta del patrón `Cargos/Details.cshtml.cs` líneas 34/40/46/54-63:

```csharp
public int CurrentPage { get; private set; } = 1;
public string? Search { get; private set; }
public string? Sort { get; private set; }

public async Task OnGetAsync(
    Guid id,
    [FromQuery(Name = "p")] int currentPage = 1,
    string? search = null,
    string? sort = null,
    CancellationToken cancellationToken = default)
{
    CurrentPage = Math.Max(1, currentPage);
    Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
    Sort   = string.IsNullOrWhiteSpace(sort)   ? null : sort.Trim();
    // ... cuerpo existente sin cambios (try/catch + TryLoadPersonaVinculadaAsync) ...
}
```

Handlers POST (`OnPostFinalizarAsync`, `OnPostEliminarAsync`, `OnPostReactivarAsync`) y `TryLoadPersonaVinculadaAsync` **NO** modificados — `REQ-OCC-DET-PAGE-001 scenario 4` validado.

**Commit**: `583905e8 feat(web): bind CurrentPage/Search/Sort in Ocupaciones Details PageModel`.

### T2 — Barra canónica en `Ocupaciones/Details.cshtml`

**Archivo**: `src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml` (12 líneas agregadas, 15 eliminadas).

- Eliminado botón `btn btn-primary` inline de la rama 404 (antes L35-37).
- Barra `<div class="row mt-3"><div class="col-12 d-flex gap-2">…</div></div>` movida **fuera del `@if/@else if`**, presente en ambos estados (404 y success).
- `btn btn-outline-warning` → `btn btn-warning`.
- `ti ti-pencil me-1` ya canónico (sin cambio).
- `href="/organizacion/ocupaciones/editar/@o.Id"` → `Url.Page("/Organizacion/Ocupaciones/Edit", new { id = o!.Id, p = Model.CurrentPage, search = Model.Search, sort = Model.Sort })` (REQ-DET-BTN-004).
- `href="/organizacion/ocupaciones"` → `Url.Page("/Organizacion/Ocupaciones/Index", new { p = Model.CurrentPage, search = Model.Search, sort = Model.Sort })` (REQ-DET-BTN-005).
- Guarda Editar: `@if (!Model.IsNotFound && Model.ViewModel!.EsVigente && Model.EsAdministrador)` — cubre REQ-OCC-FORM-003.
- Bloque de formularios POST (Finalizar/Eliminar/Reactivar) **NO** tocado — preserva binding JS de acciones destructivas.

**Commit**: `25bd59fc fix(web): align Ocupaciones Details buttons to canonical pattern`.

### T3 — Barra canónica en `UnidadesOrganizativas/Details.cshtml`

**Archivo**: `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Details.cshtml` (14 líneas agregadas, 11 eliminadas).

- Eliminado botón `btn btn-primary` inline de la rama 404 (antes L34-36).
- Eliminado `card-footer` completo (antes L96-103).
- Barra `<div class="row mt-3"><div class="col-12 d-flex gap-2">…</div></div>` agregada **fuera del `@if/@else if`**, presente en ambos estados.
- `ti ti-edit` → `ti ti-pencil me-1` (REQ-DET-BTN-002).
- `btn btn-light` → `btn btn-outline-secondary` + `ti ti-arrow-left me-1` (REQ-DET-BTN-003).
- Editar gated por `!Model.IsNotFound && Model.Unidad is not null` (REQ-DET-BTN-001/002 — no aparece en 404).
- Link Editar preserva `returnPage/returnSearch/returnSort/returnView/returnStatus` (REQ-DET-BTN-004 escenarios UnidadesOrganizativas).
- Link Volver usa `Model.ReturnToListUrl` que ya preserva estado vía `UnidadOrganizativaFormHelpers.BuildReturnToListUrl` (REQ-DET-BTN-005 escenario UnidadesOrganizativas).
- Form de **Reactivar** en rama 404 **NO** tocado (acción secundaria out of scope).

**Commit**: `622e9453 fix(web): align UnidadesOrganizativas Details buttons to canonical pattern`.

### T4 — Validación global

- Verificado que `Cargos/Details.cshtml`, `Habilidades/Details.cshtml`, `Puestos/Details.cshtml` y `Personas/Details.cshtml` permanecen sin cambios (`git diff 785e10ee HEAD --` sobre esos paths = 0 líneas).
- `dotnet build SGV.slnx` → 0 errors, 92 warnings preexistentes (no introducidos por el change).
- `dotnet test SGV.slnx` → **3241/3241 PASS, 0 FAIL, 0 SKIP** — incluye suite web completa (1351 tests) + tests de persistencia con MySQL (`[MySqlFact]` corredores porque la DB local está disponible).
- **Ajuste de test necesario**: `OcupacionDetailsPageTests.Get_Details_WhenVigenteAdmin_ShowsFinalizarEliminarAndEdit` L156 validaba la URL hardcodeada. Migración a `Url.Page` cambia el formato del Guid en el href de `D` (con guiones) a `N` (sin guiones) — assertion actualizado al formato canónico usado por `CargoDetailsPageTests` L52.

**Commit**: `cce13e12 test(web): align OcupacionDetails href assertion to Url.Page format`.

## Resumen de commits

| Hash corto | Mensaje | Archivos | +/− |
|-----------|---------|----------|-----|
| `583905e` | `feat(web): bind CurrentPage/Search/Sort in Ocupaciones Details PageModel` | `src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml.cs` | +28 / −1 |
| `25bd59f` | `fix(web): align Ocupaciones Details buttons to canonical pattern` | `src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml` | +12 / −15 |
| `622e945` | `fix(web): align UnidadesOrganizativas Details buttons to canonical pattern` | `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Details.cshtml` | +14 / −11 |
| `cce13e1` | `test(web): align OcupacionDetails href assertion to Url.Page format` | `tests/SGV.Tests/Web/Ocupaciones/OcupacionDetailsPageTests.cs` | +4 / −1 |
| **Total** | | **4 archivos** | **+58 / −28** |

Forecast original: ~60 líneas producción, 0 tests. **Cumplido**: 58 líneas producción (`+54 / −27` en producción, `+4 / −1` en test que es corrección del contrato). Difiere del forecast porque el ajuste de test fue necesario para reflejar el contrato canónico `Url.Page` que la spec exige.

## Validación ejecutada

- `dotnet build src/SGV.Web/SGV.Web.csproj --nologo` → **Build succeeded**, 0 errors.
- `dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~OcupacionDetailsPageTests"` → **15/15 PASS**.
- `dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~UnidadOrganizativa"` → **262/262 PASS**.
- `dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~SGV.Tests.Web"` → **1351/1351 PASS, 0 FAIL, 0 SKIP**.
- `dotnet test SGV.slnx` → **3241/3241 PASS, 0 FAIL, 0 SKIP** (incluye persistencia con MySQL local).

## Desviaciones del diseño

**Una desviación menor justificada**: el test `OcupacionDetailsPageTests.Get_Details_WhenVigenteAdmin_ShowsFinalizarEliminarAndEdit` L156 fue actualizado para reflejar el contrato canónico `Url.Page` (Guid en formato `N` lowercase, presencia de query params preservados). El design §Compatibilidad asumía "ningún assertion valida clase CSS exacta de botones" pero no contempló el assertion de la URL. El ajuste es 1 línea de test y alinea con el patrón de `CargoDetailsPageTests` L52 (mismo formato `{cargo.Id}`). **No afecta cobertura de comportamiento** — sigue validando que el botón Editar apunta a la ruta correcta del recurso.

## Pendiente

1. **PR** desde `develop` (los commits ya están en develop porque la estrategia de delivery es `single-pr` sin rama feature). Confirmar con el orquestador si requiere rama dedicada.
2. **`sdd-verify`** adversarial para confirmar end-to-end.
3. **`sdd-archive`** para cerrar el change y mergear las delta-specs al baseline.

## Riesgos identificados

- **Riesgo bajo**. Cambios puramente de markup y binding de query string. No toca API, Contracts, Dominio, Aplicación, Infraestructura ni migraciones.
- **Back-compat preservado**: las URLs `?p=...&search=...&sort=...` que `Url.Page` añade son superconjunto del comportamiento anterior (el botón Volver antes navegaba al Index sin query string — ahora preserva paginación si existe).
- **No regresión de cobertura**: 1351 tests web + 1890 tests no-web pasan. El único test ajustado documenta el nuevo contrato canónico.

## Próximo paso

`sdd-verify` para validación adversarial end-to-end y luego `sdd-archive`.