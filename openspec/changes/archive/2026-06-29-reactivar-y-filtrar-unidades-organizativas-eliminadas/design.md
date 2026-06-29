# Design: Reactivar y filtrar unidades organizativas eliminadas

## Technical Approach

El cambio se resolverá extendiendo el flujo listado → API → aplicación → repositorio con un segmento explícito de estado para `activas` o `eliminadas`, manteniendo `arbol` y `GetByIdAsync` como lecturas de activas. En `SGV.Web`, `Index` seguirá siendo la página única del listado, pero agregará un selector binario dentro de la vista tabular, una acción de reactivación por fila solo para eliminadas y un redirect post-éxito de vuelta al listado activo.

## Architecture Decisions

| Tema | Alternativas consideradas | Decisión | Rationale |
|---|---|---|---|
| Contrato del filtro | `includeDeleted` ambiguo vs segmento explícito | Agregar un parámetro de estado binario (`activas`/`eliminadas`) en `UnidadOrganizativaQuery`, controller y cliente web | Evita cualquier interpretación de grilla mixta y refleja exactamente la spec. |
| UX del listado | Reutilizar el banner de “última eliminada” vs acciones por fila | Mostrar `Reactivar` por fila solo en la vista de eliminadas | El banner actual no cubre búsqueda/paginación sobre eliminadas; la acción por fila sí. |
| Navegación posterior a reactivar | Mantenerse en eliminadas vs volver a activas | Redirigir a `Index` en vista activa con mensaje de éxito | Reduce confusión: el registro deja de pertenecer al conjunto visible. |
| Manejo de conflicto | Error genérico vs mensaje accionable conservando contexto | Mantener la vista de eliminadas y mostrar `ProblemDetails` traducido a mensaje utilizable | Sigue el patrón actual de `UnidadOrganizativaCommandResult` y evita perder el contexto de reintento. |

## Data Flow

```text
GET Index?status=deleted
-> IndexModel crea UnidadOrganizativaListQuery
-> UnidadOrganizativaApiClient GET /consulta?...&status=eliminadas
-> Controller construye UnidadOrganizativaQuery
-> ServicioConsulta -> Repository.QueryAsync(status)
-> Web renderiza filas eliminadas con botón Reactivar

POST Index?handler=Reactivate
-> ReactivateAsync(id)
-> éxito: TempData + Redirect Index(status=activas)
-> conflicto/notfound: TempData + Redirect Index(status=eliminadas, mismo p/search/sort)
```

## File Changes

| File | Action | Description |
|---|---|---|
| `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/UnidadOrganizativaQuery.cs` | Modify | Incorporar el segmento de estado del listado con valor por defecto `activas`. |
| `src/SGV.Aplicacion/Organizacion/Consultas/IUnidadOrganizativaRepository.cs` | Modify | Extender `QueryAsync` para recibir el segmento de estado. |
| `src/SGV.Aplicacion/Organizacion/Consultas/UnidadOrganizativaServicioConsulta.cs` | Modify | Propagar el nuevo segmento sin alterar `ListAsync`, `GetByIdAsync` ni `GetTreeAsync`. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs` | Modify | Cambiar el predicado base de `QueryAsync` para devolver solo activas o solo eliminadas según el segmento. |
| `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` | Modify | Exponer y documentar el nuevo query param del endpoint `consulta`. |
| `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaListItemViewModel.cs` | Modify | Extender `UnidadOrganizativaListQuery` con el estado seleccionado. |
| `src/SGV.Web/Integration/Organizacion/IUnidadOrganizativaApiClient.cs` | Modify | Mantener el contrato de query con el nuevo filtro sin cambiar reactivación. |
| `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs` | Modify | Serializar el estado al request URI de `/consulta`. |
| `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaFormHelpers.cs` | Modify | Preservar el segmento de estado en URLs de retorno desde detalle/edición al listado. |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs` | Modify | Gestionar el estado seleccionado, alternar listados, condicionar acciones por fila y redirigir a activas tras reactivar. |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml` | Modify | Agregar toggle Activas/Eliminadas, vacío contextual y botón `Reactivar` por fila solo en eliminadas. |
| `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioConsultaTests.cs` | Modify | Cubrir default `activas`, consulta de `eliminadas` y no mezcla de segmentos. |
| `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` | Modify | Verificar filtrado MySQL por activas vs eliminadas en `QueryAsync`. |
| `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` | Modify | Enseñar el fake de consulta a responder según el nuevo segmento. |
| `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` | Modify | Probar query param/documentación efectiva del nuevo filtro. |
| `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` | Modify | Asegurar que Swagger documente el filtro de `consulta` sin tocar `arbol`. |
| `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Modify | Cubrir toggle, conservación de contexto, reactivación exitosa volviendo a activas y conflicto permaneciendo en eliminadas. |

## Interfaces / Contracts

```csharp
public enum UnidadOrganizativaSegmentoListado
{
    Activas = 0,
    Eliminadas = 1
}

public sealed record UnidadOrganizativaQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    Guid? TipoUnidadOrganizativaId = null,
    Guid? UnidadPadreId = null,
    DateOnly? VigenteEn = null,
    UnidadOrganizativaSegmentoListado Segmento = UnidadOrganizativaSegmentoListado.Activas);
```

El controller traducirá el query param HTTP a este enum y `SGV.Web` reutilizará el mismo valor semántico en `UnidadOrganizativaListQuery`. No se modifican `UnidadOrganizativaDto`, `PATCH /reactivar` ni `GET /arbol`.

## Testing Strategy

| Layer | What to Test | Approach |
|---|---|---|
| Aplicación | Segmento por defecto, segmento eliminadas, exclusión del conjunto opuesto | Extender `UnidadOrganizativaServicioConsultaTests` y su fake repository. |
| Persistencia | Predicados `IsActive/IsDeleted` aplicados correctamente al nuevo segmento | Agregar integración en `UnidadOrganizativaRepositoryTests` con datos activos y soft-deleted. |
| API/Swagger | `GET /consulta` acepta/documenta el filtro y mantiene mismo DTO | Extender `UnidadesOrganizativasControllerTests` y `SwaggerConfigurationTests`. |
| Web | Toggle de estado, filas eliminadas con `Reactivar`, redirect a activas en éxito, permanencia en eliminadas en falla | Extender `UnidadOrganizativaWebTests` y `FakeUnidadOrganizativaApiClient`. |

## Migration / Rollout

No migration required. El rollout es directo porque no cambia esquema ni reglas de negocio; solo agrega un contrato de lectura segmentada y la UX correspondiente en `SGV.Web`.

## Open Questions

- [ ] Ninguna bloqueante. El diseño asume que la vista árbol sigue siendo exclusiva de activas y que el filtro de eliminadas aplica solo al modo listado.
