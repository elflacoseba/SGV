# Tasks: Reemplazar vista árbol por Google OrgChart

## Review Workload Forecast

| Campo | Valor |
|-------|-------|
| Líneas estimadas cambiadas | ~150–200 |
| Riesgo de presupuesto 400 líneas | Bajo |
| PR encadenados recomendados | No |
| Estrategia de delivery | single-pr |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: Low

## Fase 1: Contenedor en la vista

- [x] 1.1 En `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`, reemplazar el bloque `<ul class="list-unstyled mb-0">` (líneas 236–244) y su `foreach` con `@await Html.PartialAsync("_TreeNode")` por un `<div id="orgchart" style="width:100%; min-height:500px;"></div>`, manteniendo la estructra del `else` y el condicional de `Model.TreeItems.Count == 0`

## Fase 2: Lógica JavaScript del organigrama

- [x] 2.1 En `src/SGV.Web/wwwroot/js/pages/unidades-organizativas-index.js`, agregar función que carga Google Charts vía `google.charts.load('current', {packages:["orgchart"]})` con `setOnLoadCallback(drawChart)`, y registrar en `window.loadUnidadOrganizativaOrgChart`
- [x] 2.2 Implementar `drawChart()`: hacer `fetch('/api/v1/unidades-organizativas/arbol')`, aplanar el árbol con BFS a un `google.visualization.DataTable` con columnas `[{v:'', f:'Nombre<br/><small>Código · Tipo</small>'}, 'parent', 'tooltip']`, y dibujar con `google.visualization.OrgChart` en `#orgchart`
- [x] 2.3 Agregar manejo de error: si `fetch` falla o Google Charts no carga, inyectar mensaje "No se pudo cargar el organigrama" en `#orgchart` y hacer `console.warn` sin propagar excepción

## Fase 3: Integración y autoejecución

- [x] 3.1 En `unidades-organizativas-index.js`, dentro del bloque `if (window.document)`, invocar `window.loadUnidadOrganizativaOrgChart()` solo cuando exista `#orgchart` en el DOM (es decir, solo en vista árbol)
- [x] 3.2 Verificar que las funciones existentes (`wireUnidadOrganizativaDeleteConfirmation`, `wireUnidadOrganizativaReactivateConfirmation`) y el export CommonJS no se rompen con las nuevas funciones agregadas

## Fase 4: Verificación manual

- [x] 4.1 Compilar solución con `dotnet build SGV.slnx` sin errores
- [x] 4.2 Validar bundle frontend con `bun run build` desde `src/SGV.Web`
- [x] 4.3 Verificar que la vista tabla/listado sigue funcionando sin dependencia del CDN de Google Charts
