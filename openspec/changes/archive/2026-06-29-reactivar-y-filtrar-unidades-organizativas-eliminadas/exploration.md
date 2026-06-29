## Exploración: agregar reactivación y filtro de eliminadas en unidades organizativas

### Estado actual
- La API de unidades organizativas ya expone `PATCH /api/v1/unidades-organizativas/{id}/reactivar` y el servicio de comandos valida conflicto por `Codigo` y padre inactivo/eliminado.
- `GET /api/v1/unidades-organizativas/consulta` y `GET /api/v1/unidades-organizativas/arbol` devuelven solo unidades activas; el repositorio aplica `IsActive && !IsDeleted`.
- `UnidadOrganizativaDto` no incluye estado (`IsActive`/`IsDeleted`), así que el listado web no puede distinguir activas de eliminadas si mezcla ambos conjuntos.
- `SGV.Web` ya tiene reactivación en detalle/edición y un botón temporal en el listado tras una eliminación, pero no un filtro persistente para ver eliminadas ni una acción de reactivar por fila en ese contexto.
- El listado web actual solo maneja `list`/`tree`, búsqueda, paginación y ordenamiento; no transporta un filtro de estado.

### Áreas afectadas
- `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` — agregar query flag para incluir eliminadas o una vista explícita de eliminadas.
- `src/SGV.Aplicacion/Organizacion/Consultas/UnidadOrganizativaQuery.cs` — extender el contrato de consulta del listado.
- `src/SGV.Aplicacion/Organizacion/Consultas/UnidadOrganizativaServicioConsulta.cs` — aplicar el nuevo filtro/segmento de lectura.
- `src/SGV.Aplicacion/Organizacion/Consultas/IUnidadOrganizativaRepository.cs` y `src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs` — soportar consulta activa vs. incluida en eliminadas.
- `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/UnidadOrganizativaDto.cs` — solo si se decide un listado mixto con estado visible.
- `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaListItemViewModel.cs` y `UnidadOrganizativaApiClient.cs` — propagar el filtro al backend.
- `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml(.cs)` — UI del filtro, render de filas eliminadas y acción de reactivar.
- `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs`, `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioConsultaTests.cs`, `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs`, `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` — cubrir el nuevo contrato y el flujo de reactivación desde el listado.

### Enfoques
1. **Vista separada de eliminadas** — agregar un filtro/flag (`includeDeleted` o equivalente) que cambie el listado a “activas” o “eliminadas”; en la vista de eliminadas cada fila muestra `Reactivar`.
   - Pros: no requiere cambiar `UnidadOrganizativaDto`; encaja con el patrón actual de listado + acciones por fila; menor riesgo de romper contratos de lectura.
   - Contras: no permite mezclar activas y eliminadas en una misma grilla; hay que preservar el filtro en navegación, búsqueda y paginación.
   - Esfuerzo: Medio.

2. **Listado mixto con estado explícito** — devolver activas y eliminadas en la misma consulta, agregando estado visible al DTO para que la UI pinte badge/acción condicional.
   - Pros: una sola grilla muestra todo el universo; más flexible para auditoría visual.
   - Contras: cambia el contrato público de lectura; exige más cambios en tests, mapeo y presentación; más superficie para errores de UX.
   - Esfuerzo: Medio/Alto.

### Recomendación
La mejor opción es la **vista separada de eliminadas**. El repositorio ya sabe distinguir activos vs. eliminados; falta exponer ese corte al listado y reutilizar el endpoint de reactivación desde filas visibles. Es la forma más pequeña de cerrar la brecha funcional sin ensanchar el DTO ni el contrato de detalle.

### Riesgos
- Si el filtro no viaja en `ReturnToListUrl`, la experiencia de reactivación desde detalle/edición y desde el listado puede perder contexto.
- El cambio de contrato debe mantenerse coherente entre API, web y pruebas; si se elige vista separada, el árbol debería seguir activo-only.
- La reactivación puede seguir fallando por `Codigo` duplicado o padre inactivo/eliminado; la UI tiene que mostrar ese conflicto con claridad.

### Listo para propuesta
Sí. El alcance ya está claro para redactar la propuesta SDD: agregar filtro de eliminadas al listado y una acción de reactivar desde ese contexto.
