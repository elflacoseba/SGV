# Delta: Detalle web de Ocupaciones — Preservación de estado (web-ocupaciones-detalle)

## Purpose

Habilitar que `Ocupaciones/Details.cshtml` preserve los parámetros de
paginación/ordenamiento (`p`, `search`, `sort`) al regresar al listado y al ir a
Editar, replicando el patrón de `Cargos/Details.cshtml.cs`.

## ADDED Requirements

### Requirement: REQ-OCC-DET-PAGE-001 Binding de p/search/sort en OnGetAsync

`Ocupaciones/Details.cshtml.cs.OnGetAsync` MUST aceptar desde query string
`int? p`, `string? search` y `string? sort`, y poblar las propiedades públicas
`CurrentPage`, `Search` y `Sort` respectivamente. `CurrentPage` MUST
inicializarse a `1` cuando `p` es nulo o menor que 1, usando `Math.Max(1, p ?? 1)`.
Los handlers POST (`OnPostFinalizarAsync`, `OnPostEliminarAsync`,
`OnPostReactivarAsync`) y `TryLoadPersonaVinculadaAsync` MUST NOT verse
afectados por este binding.

#### Scenario: OnGetAsync con query params popula CurrentPage/Search/Sort

- GIVEN la página `Ocupaciones/Details`
- WHEN se invoca con `?p=3&search=garcia&sort=FechaInicio`
- THEN el PageModel MUST exponer `CurrentPage=3`, `Search="garcia"` y `Sort="FechaInicio"`.

#### Scenario: OnGetAsync sin query params usa defaults

- GIVEN la página `Ocupaciones/Details`
- WHEN se invoca sin query params de paginación
- THEN el PageModel MUST exponer `CurrentPage=1`, `Search=null` y `Sort=null`.

#### Scenario: p menor que 1 cae a 1

- GIVEN la página `Ocupaciones/Details`
- WHEN se invoca con `?p=0`
- THEN el PageModel MUST exponer `CurrentPage=1`.

#### Scenario: Handlers POST no se ven afectados por el binding

- GIVEN `Ocupaciones/Details.cshtml.cs` con el nuevo binding agregado a `OnGetAsync`
- WHEN se invoca `OnPostFinalizarAsync`, `OnPostEliminarAsync` o `OnPostReactivarAsync`
- THEN su comportamiento observable (validación, feedback, redirección PRG) MUST ser idéntico al anterior.

## MODIFIED Requirements

(ninguno — capability nueva; no existe `openspec/specs/web-ocupaciones-detalle/`.)

## REMOVED Requirements

(ninguno.)

## RENAMED Requirements

(ninguno.)