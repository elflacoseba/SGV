# Proposal: consistencia-botones-detalle-issue-229 (issue #229)

## Intent

Unificar la estructura HTML de la barra de botones "Editar / Volver al Listado" en las 6 vistas `Details.cshtml` de los módulos de organización y personas. Hoy hay inconsistencias en la ubicación de los botones (dentro vs. fuera del card), las clases CSS (btn-warning vs. btn-outline-warning, btn-light vs. btn-outline-secondary), los íconos (ti-pencil vs. ti-edit), y las URLs de retorno (hardcodeadas vs.Url.Page con parámetros de paginación/ordenamiento).

## Scope

### In Scope
- Normalizar la barra de botones en las 6 vistas `Details.cshtml` según el patrón canónico de `Cargos/Details.cshtml` y `Personas/Details.cshtml`
- **Unificar la barra en una sola estructura `<div class="row mt-3"><div class="col-12 d-flex gap-2">…</div></div>` presente tanto en 404 como en success**, fuera del `if/else` y fuera del `<div class="card">` (mismo patrón que las 4 vistas canónicas: Cargos L67-82, Personas L71-101, Habilidades L67-78, Puestos L83-103)
- Eliminar los botones `btn btn-primary` inline en las ramas 404 de `Ocupaciones/Details.cshtml` (L35-37) y `UnidadesOrganizativas/Details.cshtml` (L34-36); el botón Volver al listado unificado los reemplaza
- Mover los botones fuera del `card-footer` en `UnidadesOrganizativas/Details.cshtml`
- Corregir `btn-outline-warning` → `btn btn-warning` en `Ocupaciones/Details.cshtml`
- Corregir `ti ti-edit` → `ti ti-pencil me-1` en `UnidadesOrganizativas/Details.cshtml`
- Corregir `btn btn-light` → `btn btn-outline-secondary` + `ti ti-arrow-left me-1` en `UnidadesOrganizativas/Details.cshtml`
- Reemplazar URLs hardcodeadas por `Url.Page(…, new { p, search, sort })` en `Ocupaciones/Details.cshtml`

### Out of Scope
- Botones de acciones secundarias específicas de cada módulo (Habilidades, Ver ocupaciones, Finalizar, Eliminar, Reactivar, etc.)
- `Personas/Details.cshtml` como factor para extraer un partial reutilizable — eso es issue #219 (`reusable-persona-card`), no #229
- Cambios en la estructura del card-body, card-header o cualquier otra parte de las vistas
- PageModels de `Cargos`, `Habilidades`, `Puestos`, `Personas` y `UnidadesOrganizativas` (sus vistas ya son canónicas; sólo se modifican sus `.cshtml`)
- API, Contracts, capa de aplicación, base de datos y migraciones

### Decisión de scope revisada (post-`reusable-persona-card` slice-3 merge)
- Slice-3 (PR #222) ya migró la card de persona en `Ocupaciones/Details.cshtml` pero **no tocó** la barra de botones Editar/Volver. Confirmado: el "conflicto" alertado por la exploración original ya está resuelto sin afectar esta issue.
- `Ocupaciones/Details.cshtml.cs` **NO** expone `CurrentPage`, `Search`, `Sort` (a diferencia de `Cargos/Details.cshtml.cs` líneas 34/40/46). Para que el botón "Volver al listado" de `Ocupaciones/Details.cshtml` preserve `p/search/sort`, es necesario **agregar esos parámetros al PageModel**, mismo patrón que `Cargos`. Esto implica una excepción al "no tocar PageModels".
- **Decisión aprobada por el usuario**: exponer `CurrentPage/Search/Sort` en `Ocupaciones/Details.cshtml.cs` mediante binding desde query string, igual que `Cargos/Details.cshtml.cs`. Implementación atómica, sin afectar otros PageModels.

## Capabilities

### New Capabilities
Ninguna. Este cambio es puramente kosmético/markup; no introduce capacidades nuevas.

### Modified Capabilities
Ninguna. Los requisitos existentes de las capabilities relacionadas con cada módulo no cambian — solo el markup visual de los botones converge al mismo patrón.

## Approach

Inspección visual directa de los 6 archivos `.cshtml`. Se aplican cambios mínimos Targeted a la barra de botones:

1. **`UnidadesOrganizativas/Details.cshtml`**:
   - Eliminar el botón `btn btn-primary` inline de la rama 404 (línea 34-36); el botón Volver unificado lo reemplaza.
   - Mover los botones del `card-footer` (líneas 96-103) a una `<div class="row mt-3"><div class="col-12 d-flex gap-2">` **fuera del `if/else`**, presente tanto en 404 como en success, igual que las 4 vistas canónicas.
   - Cambiar `ti ti-edit` → `ti ti-pencil me-1` en el botón Editar.
   - Cambiar `btn btn-light` → `btn btn-outline-secondary` y agregar `<i class="ti ti-arrow-left me-1"></i>` al botón Volver.
   - Gatear el botón Editar con `if (!Model.IsNotFound)` para que NO aparezca en 404.
   - El link de Editar ya usa `returnPage/returnSearch/returnSort` — preservar.
   - El link de Volver usa `Model.ReturnToListUrl` que ya preserva estado vía `UnidadOrganizativaFormHelpers.BuildReturnToListUrl`.

2. **`Ocupaciones/Details.cshtml`**:
   - Eliminar el botón `btn btn-primary` inline de la rama 404 (líneas 35-37); el botón Volver unificado lo reemplaza.
   - Mover la `<div class="row mt-3"><div class="col-12 d-flex gap-2">` de las líneas 149-161 a una posición **fuera del `if/else`**, presente tanto en 404 como en success, igual que las 4 vistas canónicas.
   - Cambiar `btn btn-outline-warning` → `btn btn-warning` en el botón Editar.
   - Reemplazar la URL hardcodeada `/organizacion/ocupaciones/editar/@o.Id` por `Url.Page("/Organizacion/Ocupaciones/Edit", new { id = o.Id, p = Model.CurrentPage, search = Model.Search, sort = Model.Sort })`.
   - Reemplazar la URL hardcodeada `/organizacion/ocupaciones` por `Url.Page("/Organizacion/Ocupaciones/Index", new { p = Model.CurrentPage, search = Model.Search, sort = Model.Sort })`.
   - Gatear el botón Editar con `if (!Model.IsNotFound && Model.ViewModel!.EsVigente && Model.EsAdministrador)` — el condicional existente ya cubre estos casos; mantener el wrapping `if`.

3. **`Ocupaciones/Details.cshtml.cs`** (excepción al "no tocar PageModels"):
   - Agregar `public int CurrentPage { get; private set; } = 1;`
   - Agregar `public string? Search { get; private set; }`
   - Agregar `public string? Sort { get; private set; }`
   - En `OnGetAsync(Guid id, …)`, recibir parámetros `[FromQuery(Name = "p")] int currentPage = 1`, `[FromQuery(Name = "search")] string? search = null`, `[FromQuery(Name = "sort")] string? sort = null` y popular las propiedades (`CurrentPage = Math.Max(1, currentPage); Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(); Sort = string.IsNullOrWhiteSpace(sort) ? null : sort.Trim();`), mismo patrón que `Cargos/Details.cshtml.cs` líneas 34/40/46/61.
   - NO modificar los handlers POST ni la lógica de `TryLoadPersonaVinculadaAsync`.

## Non-goals

- No se introduce ningún componente parcial nuevo para botones reutilizables.
- No se modifica ninguna otra vista besides las 2 desviadas (`UnidadesOrganizativas/Details.cshtml`, `Ocupaciones/Details.cshtml`); las 4 restantes ya son canónicas y permanecen sin cambios.
- No se toca `Personas/Details.cshtml` para extraer un partial — eso pertenece a la issue #219.
- No se modifican los handlers POST de `Ocupaciones/Details.cshtml.cs` (Finalizar, Eliminar, Reactivar). Solo se agrega el binding de `CurrentPage/Search/Sort` al `OnGetAsync`.
- No se modifica `OcupacionDetailsViewModel`, ni la inyección de `IPersonaApiClient`, ni la lógica de `TryLoadPersonaVinculadaAsync` del slice-3 del issue #219.

## Affected Areas

| Archivo | Impacto | Descripción |
|---------|---------|-------------|
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml` | Modificado | Corregir `btn-outline-warning` → `btn btn-warning`; reemplazar URLs hardcodeadas por `Url.Page` con parámetros de paginación |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml.cs` | Modificado | Exponer `CurrentPage/Search/Sort` desde query string (binding tipo `Cargos/Details.cshtml.cs`) para soportar preservación de estado en el botón Volver |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Details.cshtml` | Modificado | Extraer botones del `card-footer`; corregir ícono (`ti-edit` → `ti-pencil`) y clase CSS (`btn-light` → `btn-outline-secondary` con `ti-arrow-left`) |
| `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml` | Sin cambios | Canónico — verificar que permanece idéntico |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Details.cshtml` | Sin cambios | Canónico — verificar que permanece idéntico |
| `src/SGV.Web/Pages/Organizacion/Puestos/Details.cshtml` | Sin cambios | Canónico — verificar que permanece idéntico |
| `src/SGV.Web/Pages/Personas/Details.cshtml` | Sin cambios | Canónico — verificar que permanece idéntico |

## Risks

| Riesgo | Probabilidad | Mitigación |
|--------|--------------|------------|
| Regresión visual al mover los botones en `UnidadesOrganizativas/Details.cshtml` (dentro → fuera del card) | Low | Los tests existentes de navegación de detalle (web smoke tests) validan que la estructura de la página y los enlaces son correctos. |
| Regresión en los tests de `OcupacionDetailsPageTests` (15 tests) al cambiar markup de la barra de botones | Low | Los tests existentes validan comportamiento; el patrón de botones canónico ya está validado en `Cargos/DetailsPageTests`. La revisión de los assertions confirma que ningún test verifica la clase CSS exacta de los botones. |
| Romper binding JS de los handlers POST (Finalizar/Eliminar/Reactivar) por tocar accidentalmente el `<form>` adyacente | Low | El cambio en `Ocupaciones/Details.cshtml` se limita al bloque `<div class="row mt-3">` posterior al card de acciones de ciclo de vida (líneas 149-161). No se toca el bloque de formularios. |

## Rollback Plan

1. `git checkout` de los 3 archivos modificados (2 `.cshtml` + 1 `.cshtml.cs`) a su estado previo en `develop`.
2. `dotnet build SGV.slnx` confirma compilación.
3. `dotnet test SGV.slnx` confirma que los tests pasan.
4. No se requiere migración de base de datos ni cambios en `SGV.Contracts`.
5. El cambio en `Details.cshtml.cs` solo agrega binding de query string; removerlo no afecta la lógica de carga de la ocupación ni los handlers POST.

## Dependencies

- Ninguna dependencia externa nueva. Los cambios son markup + binding de query string en `Ocupaciones/Details.cshtml.cs` (mismo patrón que `Cargos/Details.cshtml.cs` líneas 34/40/46/61).
- El change `reusable-persona-card` (issue #219) ya mergeó su slice-3 a `develop` (PR #222). Slice-3 tocó la card de persona (líneas 49-67 de `Ocupaciones/Details.cshtml`) pero **no** la barra de botones. No hay conflicto de merge.

## Test Strategy

No se requieren tests nuevos. La issue declara explícitamente que los tests existentes de navegación de detalle deben seguir pasando. La suite de tests web (Smoke tests) ya cubre:
- Que las páginas de detalle renderizan sin errores.
- Que los enlaces de navegación de retorno funcionan (vía `ReturnToListUrl` o `Url.Page`).

La verificación consistirá en:
1. `dotnet build SGV.slnx` — compilación limpia.
2. `dotnet test SGV.slnx` — suite completa verde.
3. Validación visual post-apply (opcional, no requerida por la issue).

## Success Criteria

- [ ] `UnidadesOrganizativas/Details.cshtml`: botones fuera del card, Editar con `btn btn-warning ti ti-pencil me-1`, Volver con `btn btn-outline-secondary ti ti-arrow-left me-1`
- [ ] `Ocupaciones/Details.cshtml`: botón Editar con `btn btn-warning ti ti-pencil me-1` (no `btn-outline-warning`); URLs de navegación preservan `p`, `search`, `sort` mediante `Url.Page`
- [ ] `Ocupaciones/Details.cshtml.cs`: expone `CurrentPage/Search/Sort` desde query string, mismo patrón que `Cargos/Details.cshtml.cs`
- [ ] Las 4 vistas canónicas (`Cargos`, `Habilidades`, `Puestos`, `Personas`) permanecen sin cambios
- [ ] `dotnet build SGV.slnx` compila sin errores
- [ ] `dotnet test SGV.slnx` pasa sin regresiones

---

## Estado de coordinación con issue #219 (reusable-persona-card)

**Resuelto**: slice-3 del change `reusable-persona-card` ya mergeó a `develop` (PR #222, merge commit `10c9d766`). Slice-3 tocó la card de persona (líneas 49-67 de `Ocupaciones/Details.cshtml`) pero **NO** la barra de botones Editar/Volver (líneas 149-161). No hay conflicto de merge. Esta issue puede aplicarse sobre `develop` sin coordinación adicional.
