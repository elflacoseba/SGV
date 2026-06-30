# Diseño: Implementar el módulo de Cargos en el Frontend

## Enfoque técnico

El slice se implementará en `SGV.Web` como un módulo Razor Pages autenticado bajo `Pages/Organizacion/Cargos`, replicando el seam probado de `UnidadesOrganizativas` pero ajustado al contrato real de `CargosController`: listado activo-only, detalle readonly y baja lógica. La carga inicial será SSR con `OnGetAsync`; la baja se resolverá con `OnPostDeleteAsync`, `TempData` para feedback y recarga GET para preservar antiforgery, navegación y pruebas web existentes.

## Decisiones de arquitectura

| Tema | Alternativas consideradas | Decisión | Rationale |
|---|---|---|---|
| Cliente HTTP | Llamar `HttpClient` directo desde PageModels | Crear `ICargoApiClient` + `CargoApiClient` en `Integration/Organizacion` | Mantiene PageModels delgados y permite fakear el módulo en `WebApplicationFactory`. |
| Forma de paginar | Pedir paginación server-side o endpoint `/consulta` | Paginar, buscar y ordenar en memoria sobre `GET /api/v1/cargos` | El backend hoy solo expone lista completa de activos; no se amplía contrato. |
| Punto de baja | Habilitar delete desde listado y detalle | Ejecutar baja solo desde listado | Evita expandir el detalle readonly y reduce superficie del primer corte. |
| Error de conflicto | Mensaje genérico post-redirect | Traducir `409 ProblemDetails` a feedback visible y accionable | `DELETE /api/v1/cargos/{id}` ya informa conflictos como `CargoConPuestosActivos`; la UI debe preservarlo. |

## Flujo de interacción

```text
Shell -> menú Cargos -> GET /organizacion/cargos
-> IndexModel.OnGetAsync
-> ICargoApiClient.GetAllAsync()
-> GET /api/v1/cargos
-> filtro/search/sort/paginación en memoria
-> tabla SSR con acciones Detalle / Eliminar

Click Eliminar -> SweetAlert2 -> POST ?handler=Delete
-> ICargoApiClient.DeleteAsync(id)
-> 204: TempData + RedirectToPage(listado preservando p/search/sort)
-> 404/409/5xx: TempData danger + RedirectToPage(mismo contexto)

Click Detalle -> GET /organizacion/cargos/detalles/{id}
-> DetailsModel.OnGetAsync
-> ICargoApiClient.GetByIdAsync(id)
-> GET /api/v1/cargos/{id}
-> render readonly o estado no disponible + volver al listado
```

## Estrategia de errores

- Fallo de carga del listado o detalle: log con `ILogger`, estado vacío controlado y alerta visible sin romper la shell.
- `404` en detalle: mensaje de cargo no disponible y link de retorno al listado preservando contexto.
- `DELETE 409`: mostrar `ProblemDetails.Detail` junto al prefijo funcional (“No se pudo eliminar el cargo…”).
- `DELETE 404`: informar que el cargo ya no está disponible y volver al listado sin reintento implícito.
- No se incorporan flujos de reactivación, eliminados ni skills; cualquier respuesta fuera de este slice queda fuera de la UI.

## Cambios de archivos

| Archivo | Acción | Descripción |
|---|---|---|
| `src/SGV.Web/Program.cs` | Modificar | Registrar `ICargoApiClient` como cliente HTTP tipado. |
| `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` | Modificar | Agregar entrada autenticada para `Cargos` y estado activo del menú. |
| `src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs` | Crear | Contrato tipado para listado activo, detalle y baja. |
| `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs` | Crear | Consumir `GET /api/v1/cargos`, `GET /api/v1/cargos/{id}` y `DELETE /api/v1/cargos/{id}`. |
| `src/SGV.Web/Integration/Organizacion/CargoListItemViewModel.cs` | Crear | View model de grilla + contratos locales de query/delete. |
| `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml` | Crear | Tabla Inspinia, buscador, paginación local, feedback y forms de delete. |
| `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs` | Crear | `[Authorize]`, carga SSR, filtro/sort/paginación en memoria y `OnPostDeleteAsync`. |
| `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml` | Crear | Pantalla readonly con estado de no encontrado y retorno al listado. |
| `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml.cs` | Crear | Obtención por id y preservación de `p/search/sort` en retorno. |
| `src/SGV.Web/wwwroot/js/pages/cargos-index.js` | Crear | Confirmación SweetAlert2 para baja lógica. |
| `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` | Modificar | Permitir override de `ICargoApiClient`. |
| `tests/SGV.Tests/Web/CargoWebTests.cs` | Crear | Cobertura de shell, listado, detalle, delete y errores del módulo. |

## Interfaces / contratos

```csharp
public interface ICargoApiClient
{
    Task<IReadOnlyList<CargoDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CargoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CargoDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record CargoListQuery(int Page, int PageSize, string? Search, string? Sort);
public sealed record CargoDeleteResult(bool Succeeded, HttpStatusCode? StatusCode, string? Code, string? Message);
```

## Estrategia de pruebas

| Capa | Qué validar | Enfoque |
|---|---|---|
| Web integration | redirect anónimo, menú visible, listado activo, búsqueda/paginación local, detalle readonly, delete éxito/409/404 | `WebApplicationFactory` con cliente autenticado y fake `ICargoApiClient`. |
| Asset/script | confirmación cancela o envía una sola vez | Harness Node similar al de `unidades-organizativas-index.js`. |
| Build relevante | compatibilidad del shell Razor + assets | `dotnet test` del slice web y `bun run build` en implementación. |

## Migración / rollout

No requiere migración. El rollout queda acotado a `SGV.Web` y depende de que el backend mantenga sin cambios los endpoints ya existentes de `Cargos`.

## Preguntas abiertas

- [ ] Ninguna bloqueante para `sdd-tasks`; el principal límite funcional ya está cerrado: sin create/edit, sin eliminados/reactivación, sin skills y sin cambios backend.
