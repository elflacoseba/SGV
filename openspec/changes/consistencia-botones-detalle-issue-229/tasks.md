# Tasks: consistencia-botones-detalle-issue-229 (issue #229)

## Resumen

Cambio acotado a 3 archivos de `SGV.Web`: binding de query string y markup Razor. Las cuatro vistas canónicas permanecen sin cambios. No se agregan tests: los smoke/integration tests web existentes cubren render, estados 404, autorización y enlaces de retorno.

## Forecast de líneas

| Área | Estimación |
|---|---:|
| Producción | ~60 líneas modificadas |
| Tests | 0 líneas |
| Total | ~60 líneas |
| Riesgo 400 líneas | Bajo |

## Estrategia de PR

Single PR, sin chained PRs: el diff estimado queda ampliamente debajo de 400 líneas, no introduce dependencias ni cambios de persistencia y cada unidad puede revertirse por archivo. Delivery strategy: `ask-on-risk`; no requiere decisión adicional antes de apply.

## Tareas

1. [x] **T1 — Binding de contexto en Ocupaciones Details**
   - Agregar `CurrentPage`, `Search` y `Sort` y poblarlos en `OnGetAsync` con `[FromQuery(Name = "p")]`, `Math.Max(1, ...)` y trim, siguiendo `Cargos/Details.cshtml.cs`; no tocar handlers POST ni `TryLoadPersonaVinculadaAsync`.
   - **Archivo:** `src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml.cs`. **Safety net:** `tests/SGV.Tests/Web/Ocupaciones/OcupacionDetailsPageTests.cs` (15 tests existentes; escenarios GET, 404 y acciones). **Commit:** `feat(web): bind CurrentPage/Search/Sort in Ocupaciones Details PageModel`. **Aceptación:** `p=3` conserva 3 y search/sort; ausente o `p=0` usa 1/null; suite focalizada verde.

2. [x] **T2 — Barra canónica de Ocupaciones**
   - Mover la barra fuera del `if/else`, eliminar el botón inline 404, usar `btn-warning` y generar Editar/Volver con `Url.Page` preservando `p/search/sort`; mantener intactas las acciones de ciclo de vida.
   - **Archivo:** `src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml`. **Safety net:** `OcupacionDetailsPageTests` valida detalle existente, 404 y acciones; no verifica clases CSS literales. **Commit:** `fix(web): align Ocupaciones Details buttons to canonical pattern`. **Aceptación:** barra `row mt-3` aparece en ambos estados, Editar sólo cuando corresponde y ambos links preservan contexto.

3. [x] **T3 — Barra canónica de Unidades Organizativas**
   - Eliminar botón inline 404 y `card-footer`; renderizar barra exterior con Editar gated por `!IsNotFound`, `ti-pencil`, `btn-outline-secondary` y `ti-arrow-left`, preservando `ReturnToListUrl` y parámetros de retorno.
   - **Archivo:** `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Details.cshtml`. **Safety net:** `tests/SGV.Tests/Web/UnidadOrganizativaCreateDetailsTests.cs` y tests parciales de `UnidadOrganizativaWebTests` cubren autenticación, existente, 404, eliminado y reactivación. **Commit:** `fix(web): align UnidadesOrganizativas Details buttons to canonical pattern`. **Aceptación:** botones fuera del card en success/404; `returnPage=2` llega al listado; acciones de reactivación intactas.

4. [x] **T4 — Validación global y revisión de alcance**
   - Verificar que `Cargos`, `Habilidades`, `Puestos` y `Personas` no cambiaron; ejecutar build, suite completa y smoke manual opcional con query string.
   - **Safety net:** `dotnet build SGV.slnx`; `dotnet test SGV.slnx`; navegación de Details con `?p=2&search=foo&sort=Nombre`. **Commit final:** conservar commits T1–T3; no generar tests nuevos salvo fallo real de cobertura.

## Riesgos y mitigaciones

- Regresión visual al sacar botones del card: comparar estructura con `Cargos/Details.cshtml` y validar success/404.
- Pérdida de contexto de listado: comprobar URLs renderizadas con `p/search/sort` y `ReturnToListUrl`.
- Alteración accidental de formularios POST: limitar cambios de Ocupaciones al binding GET y barra visual; ejecutar suite completa.

## Validación global

- `dotnet build SGV.slnx` sin errores.
- `dotnet test SGV.slnx` verde; tests MySQL pueden skippear si no hay conexión.
- Smoke web manual opcional para estados existente/404 y navegación de retorno.
