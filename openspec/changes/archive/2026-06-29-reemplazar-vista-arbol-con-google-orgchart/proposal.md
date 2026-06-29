# Propuesta: Reemplazar vista árbol por Google OrgChart

## Intención

Reemplazar la vista de árbol jerárquico actual de unidades organizativas (rendereada con parcial recursivo Razor + Bootstrap cards) por un organigrama interactivo con Google OrgChart. Mejora la visualización al mostrar de forma clara relaciones jerárquicas padre-hijo con tooltips, resaltado al clicar y líneas conectoras.

## Alcance

### Dentro del alcance
- Reemplazar la renderización del árbol en `Index.cshtml` por un contenedor `div#orgchart`
- Agregar carga de Google Charts y lógica de dibujo del organigrama en `unidades-organizativas-index.js`
- Aplanar los datos jerárquicos del endpoint `GET /api/v1/unidades-organizativas/arbol` al formato tabla plana que espera OrgChart (Node ID, Parent ID, Tooltip)
- Mantener `_TreeNode.cshtml` sin eliminar (reutilizable en otros contextos)

### No objetivos
- No se modifican APIs, endpoints ni DTOs del backend
- No se altera la vista de listado/tabla, búsqueda, paginación, CRUD ni alternancia activas/eliminadas
- No se agregan nuevas funcionalidades de negocio
- No se cambia la navegación ni el layout del shell autenticado

## Capacidades

### Nuevas capacidades
Ninguna — este cambio no introduce nuevas capacidades.

### Capacidades modificadas
- `unidad-organizativa-web-listado`: se mejora la visualización del organigrama (vista árbol) dentro del listado existente. No cambian requisitos funcionales, solo la implementación de UI. No requiere delta spec.

## Enfoque

1. En `Index.cshtml`, reemplazar `@await Html.PartialAsync("_TreeNode", arbol)` por un `<div id="orgchart" style="width:100%; height:600px;"></div>`.
2. En `unidades-organizativas-index.js`, agregar función que carga Google Charts CDN, consume el endpoint `/arbol`, aplana la jerarquía a filas `[id, parentId, tooltip]` e invoca `google.visualization.OrgChart.draw()`.
3. El aplanamiento usa una cola/recorrido BFS: cada nodo produce una fila con su `Id`, el `Id` de su padre (o `null` para la raíz), y un tooltip con código + tipo.
4. Google Charts se carga desde `https://www.gstatic.com/charts/loader.js`.

## Áreas afectadas

| Archivo | Acción | Descripción |
|---------|--------|-------------|
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml` | Modificado | Reemplazar partial del árbol por contenedor del chart |
| `src/SGV.Web/wwwroot/js/pages/unidades-organizativas-index.js` | Modificado | Agregar carga de Google Charts + lógica de dibujo |

## Riesgos

| Riesgo | Probabilidad | Mitigación |
|--------|:------------:|------------|
| CDN de Google Charts no disponible | Baja | La tabla/listado sigue funcionando; el chart muestra mensaje de error |
| El aplanamiento de jerarquía falla con árboles profundos o cíclicos | Baja | Los datos vienen de una jerarquía controlada; validación en frontend |
| Layout responsivo del contenedor del chart | Media | Probar en viewports comunes; ajustar altura con JS si es necesario |

## Plan de rollback

Revertir los dos archivos modificados (`Index.cshtml` y `unidades-organizativas-index.js`), restaurando la llamada al partial `_TreeNode`. El partial original permanece intacto en disco.

## Dependencias

- Google Charts CDN: `https://www.gstatic.com/charts/loader.js`
- Paquete: `google.visualization.OrgChart`

## Criterios de éxito

- [ ] El organigrama se renderiza con todas las unidades organizativas activas visibles
- [ ] Las líneas conectoras padre-hijo son correctas para toda la jerarquía
- [ ] El tooltip hover muestra código y tipo de unidad
- [ ] Al hacer clic en un nodo, se resalta visualmente
- [ ] La vista de listado/tabla no se ve afectada por el cambio
- [ ] Si el CDN falla, el resto de la página funciona sin errores JS catastróficos
