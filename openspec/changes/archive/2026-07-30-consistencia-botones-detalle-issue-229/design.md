# Design: consistencia-botones-detalle-issue-229 (issue #229)

## Resumen

Unificar la barra de botones "Editar / Volver al listado" de las vistas `Details`
canónicas adoptando como patrón `Cargos/Details.cshtml` y `Personas/Details.cshtml`:
una **única** barra `<div class="row mt-3"><div class="col-12 d-flex gap-2">…`
fuera del `card`, renderizada en ambos estados (404 y recurso existente), con
Editar gated por `!IsNotFound` y Volver siempre presente. Se minimiza el área
tocada: 2 `.cshtml` (markup) + 1 `.cshtml.cs` (binding de query string).

## Contexto

- Proposal: `openspec/changes/consistencia-botones-detalle-issue-229/proposal.md`.
- Specs: `specs/web-detalle-consistencia-botones/spec.md` (REQ-DET-BTN-001..006)
  y `specs/web-ocupaciones-detalle/spec.md` (REQ-OCC-DET-PAGE-001).
- Patrón canónico verificado: `Cargos/Details.cshtml` (lin. 67-82) y
  `Personas/Details.cshtml` (lin. 71-102). Ambos mueven la barra **fuera** del
  `if/else` de card y la renderizan en ambos estados; Editar con `btn-warning` +
  `ti-pencil me-1`; Volver con `btn-outline-secondary` + `ti-arrow-left me-1`.
- Sin conflictos con `reusable-persona-card` (issue #219 / PR #222): slice-3
  tocó la card de persona (lin. 49-67), no la barra de botones.

## Arquitectura y capas afectadas

Cambio confinado a `SGV.Web` (Razor Pages). **No** afecta Dominio, Aplicación,
Contracts, Infraestructura, API, ni persistencia. No hay migraciones ni nuevos
wire-types.

| Proyecto | Archivo | Acción |
|----------|---------|--------|
| `SGV.Web` | `Pages/Organizacion/UnidadesOrganizativas/Details.cshtml` | Modificar (markup) |
| `SGV.Web` | `Pages/Organizacion/Ocupaciones/Details.cshtml` | Modificar (markup) |
| `SGV.Web` | `Pages/Organizacion/Ocupaciones/Details.cshtml.cs` | Modificar (binding) |

## Cambios detallados

### 1. `UnidadesOrganizativas/Details.cshtml`

Hoy el botón existe en **dos** lugares no canónicos: inline en la rama 404
(lin. 34-36, `btn btn-primary` sin ícono) y dentro de `card-footer` en la rama
success (lin. 96-103, `btn btn-light` + `ti ti-edit`).

- **Rama 404**: eliminar el `<a class="btn btn-primary" …>Volver al listado</a>`
  inline (lin. 34-36). El form de **Reactivar** (lin. 22-33) queda intacto (acción
  secundaria, out of scope).
- **Rama success**: eliminar el `card-footer` completo (lin. 96-103) de modo
  que el card cierre tras `</dl></div></div>` (la copia canónica no tiene footer).
- **Barra unificada** tras el bloque `@if/@else if` (lin. 107), réplica exacta
  de Cargos:
```razor
<div class="row mt-3">
  <div class="col-12 d-flex gap-2">
    @if (!Model.IsNotFound)
    {
      <a class="btn btn-warning"
         href="@Url.Page("/Organizacion/UnidadesOrganizativas/Edit",
             new { id = Model.Unidad!.Id, returnPage = Model.ReturnPage,
                   returnSearch = Model.ReturnSearch, returnSort = Model.ReturnSort,
                   returnView = Model.ReturnView, returnStatus = Model.ReturnStatus })">
        <i class="ti ti-pencil me-1"></i>Editar
      </a>
    }
    <a class="btn btn-outline-secondary" href="@Model.ReturnToListUrl">
      <i class="ti ti-arrow-left me-1"></i>Volver al listado
    </a>
  </div>
</div>
```
`Model.ReturnToListUrl` ya preserva `p` vía `returnPage`
(`UnidadOrganizativaFormHelpers.BuildReturnToListUrl`, lin. 12) → cumple
REQ-DET-BTN-005 escenario UnidadesOrganizativas. PageModel **no** se toca.

### 2. `Ocupaciones/Details.cshtml`

- **Rama 404** (lin. 35-37): eliminar el `<a class="btn btn-primary"
  href="/organizacion/ocupaciones">…</a>` inline. La card 404 queda sólo con
  icono+mensaje (rÉplica de Cargos/Puestos 404).
- **Barra unificada**: mover el bloque actual (lin. 149-161) **fuera** del
  `else if (showDetails)` a una posición tras el cierre de todo el `@if/@else
  if` (después del ciclo de vida card, que permanece dentro del `else if`).
  Editar gated por `!Model.IsNotFound && Model.ViewModel!.EsVigente &&
  Model.EsAdministrador`. Corrections:
  - `btn btn-outline-warning` → `btn btn-warning`.
  - `href="/organizacion/ocupaciones/editar/@o.Id"` → `Url.Page(
    "/Organizacion/Ocupaciones/Edit",
    new { id = o.Id, p = Model.CurrentPage, search = Model.Search, sort = Model.Sort })`.
  - `href="/organizacion/ocupaciones"` → `Url.Page("/Organizacion/Ocupaciones/Index",
    new { p = Model.CurrentPage, search = Model.Search, sort = Model.Sort })`.
  - La clase `btn-outline-secondary` + `ti-arrow-left me-1` ya es correcta.

### 3. `Ocupaciones/Details.cshtml.cs` (excepción al "no tocar PageModels")

Réplica exacta del patron `Cargos/Details.cshtml.cs` (lin. 34/40/46/54-63):

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
    // ... cuerpo existente sin cambios ...
}
```

Se usa `[FromQuery(Name = "p")] int currentPage = 1` (no `int? p`) y se trimea
`search/sort` (empty → null) para URLs limpias — comportamiento superconjunto
de los escenarios de `REQ-OCC-DET-PAGE-001` (que validan `p=0→1`, ausente→1 y
`p=3&search=garcia&sort=FechaInicio`). **No** se alteran `OnPostFinalizarAsync`,
`OnPostEliminarAsync`, `OnPostReactivarAsync` ni `TryLoadPersonaVinculadaAsync`
(handlers POST y helper privado independientes del binding GET). La prop. `o`
(ocupación) se resuelve sólo en rama success → acceso via `o.Id` seguro.

## Decisiones técnicas

| # | Decisión | Alternativa rechazada | Rationale |
|---|----------|----------------------|-----------|
| D1 | Unificar la barra fuera del `if/else` en **ambas** vistas (404+success) | Dejar un botón inline por rama (status quo) | `REQ-DET-BTN-001/003` cubren explícitamente el estado 404; el canónico Cargos/Personas ya lo resuelve con una sola barra exterior. |
| D2 | Firma canónica `[FromQuery(Name="p")] int currentPage = 1` con trim | `int? p = null` + `Search = search` (propuesta literal) | Convención del repo: seguir el patrón existente. El trim normaliza `""→null` (URL sin `?search=`), superconjunto de los escenarios spec. |
| D3 | Mover la barra de Ocupaciones fuera del `else if` | Mantenerla dentro (lin. 149-161) | Si queda dentro, la rama 404 no hereda la barra y viola REQ-DET-BTN-003. Necesario para cobertura 404. |
| D4 | No extraer partial reutilizable de botones | Componente `_DetailsButtonBar.cshtml` | Out of scope (#219 trata reutilización). Issue #229 es kosmético puntual. |

## Compatibilidad

- Vistas canónicas (`Cargos/Habilidades/Puestos/Personas/Details.cshtml`) **sin
  cambios**: ya cumplen los REQs. `Puestos/Details.cshtml` y
  `Habilidades/Details.cshtml` no se leyeron en esta fase; se verificará paridad
  en tasks/verify.
- `OcupacionDetailsPageTests` (15 tests): ningún assertion valida clase CSS
  exacta de botones (validado en proposal §Risks). La estructura HTML del 404
  cambia (se quita el botón inline), pero los smoke tests de navegación checean
  enlaces, no markup interno del card 404.
- Handlers POST y `TryLoadPersonaVinculadaAsync` intactos.

## Validación

- `dotnet build SGV.slnx` — compilación limpia.
- `dotnet test SGV.slnx` — suite completa verde (smoke tests web cubren
  render de Details y enlaces de retorno).
- Smoke web manual opcional: navegar Details con `?p=2&search=foo&sort=Nombre`,
  verificar que Volver/Editar preservan query string.

## Riesgos residuales

- Expansión de diff de `Ocupaciones/Details.cshtml` más allá de "lin. 149-161"
  (también lin. 35-37 + reubicación del bloque). **Confirmar con orquestador**
  que la cobertura 404 requerida por REQ-DET-BTN-003 justifica el alcance extra.
- `Puestos/` y `Habilidades/` no inspeccionados: si su 404 tuviera botón inline
  no canónico, habría que extender scope. Verificar en tasks.

## Out of scope

- Botones de acciones secundarias (Habilidades, Ver ocupaciones, Finalizar,
  Eliminar, Reactivar). Partial reutilizable (#219). Modificar card-body/header.
- PageModels de Cargos/Habilidades/Puestos/Personas/UnidadesOrganizativas.

## Open Questions

- [ ] Confirmar expansión de scope de `Ocupaciones/Details.cshtml` a la rama 404
      (_REQ-DET-BTN-003_ cubre 404; propuesta enumeró sólo lin. 149-161).
- [ ] Verificar paridad 404 de `Puestos/` y `Habilidades/` antes de tareas.

## Threat Matrix

N/A — sin routing, shell, subprocess, VCS/PR automation ni clasificación de
ejecutables. Punto final Razor Page existente.