# Design: Permitir reactivar las unidades organizativas eliminadas

## Technical Approach

El cambio se resolverá en `SGV.Web`, reutilizando el endpoint existente `PATCH /api/v1/unidades-organizativas/{id}/reactivar` y SIN alterar la lógica de negocio backend. La web seguirá consumiendo solo lecturas activas (`GetByIdAsync`, `QueryAsync`, `GetTreeAsync`), pero agregará un flujo de “estado recuperable” y una acción POST de reactivación en listado, detalle y edición.

## Architecture Decisions

| Tema | Alternativas consideradas | Decisión | Rationale |
|---|---|---|---|
| Reactivación desde listado | Mostrar eliminados en la tabla vs recordar la última eliminación | Usar `TempData` para recordar la última unidad eliminada y mostrar un banner con acción de reactivar | Preserva que `Index` siga mostrando solo activos y evita inventar una consulta de eliminados. |
| Detail/Edit con unidad eliminada | Nueva lectura “incluyendo eliminados” vs estado recuperable con el `id` de la ruta | Mantener `GetByIdAsync` activo-only; si devuelve `null`, renderizar estado recuperable con POST de reactivación | Respeta el contrato actual y evita expandir backend solo para la shell web. |
| Contrato web | Reusar `DeleteResult` vs contrato simétrico de comando | Agregar `ReactivateAsync` que devuelva `UnidadOrganizativaCommandResult` | Ya existe el mapeo de `ProblemDetails`/`Conflict` para create/update; conviene reutilizar ese camino para mensajes accionables. |
| Destino tras reactivar | Volver siempre al listado vs volver al contexto de origen | Listado vuelve a `Index`; detail/edit redirigen a `Details` con `returnPage/search/sort/view` | Mantiene PRG y continuidad de navegación. |

## Data Flow

```text
Listado:
POST Delete -> DeleteAsync(id) -> success
-> TempData(lastDeletedId/codigo/nombre/contexto)
-> Redirect Index
-> banner “Reactivar” -> POST Reactivate -> ReactivateAsync(id)
-> success/conflict -> Redirect Index con mismo contexto

Detail/Edit:
GET /detalles|editar/{id}
-> GetByIdAsync(id)
-> null => estado recuperable
-> POST Reactivate(id)
-> success => Redirect Details(id)
-> conflict/notfound => permanecer/volver al listado con feedback
```

## File Changes

| File | Action | Description |
|---|---|---|
| `src/SGV.Web/Integration/Organizacion/IUnidadOrganizativaApiClient.cs` | Modify | Agrega `ReactivateAsync(Guid)` al contrato tipado. |
| `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs` | Modify | Consume `PATCH /api/v1/unidades-organizativas/{id}/reactivar` y traduce `404/409` a `UnidadOrganizativaCommandResult`. |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs` | Modify | Guarda en `TempData` la última unidad eliminada, expone handler `OnPostReactivateAsync` y preserva `p/search/sort/view`. |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml` | Modify | Renderiza banner de eliminación exitosa con CTA de reactivación y feedback de conflicto/éxito. |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Details.cshtml.cs` | Modify | Cuando `GetByIdAsync` devuelve `null`, mantiene `IsNotFound` pero agrega contexto de recuperación y POST de reactivación. |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Details.cshtml` | Modify | Reemplaza el estado terminal por estado recuperable con botón “Reactivar”. |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml.cs` | Modify | Si la unidad no aparece en lectura activa, deja de redirigir automáticamente y muestra estado recuperable con acción de reactivar. |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml` | Modify | Muestra banner/empty-state recuperable en vez del formulario cuando la unidad está eliminada. |
| `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Modify | Cubre banner de reactivación en listado, detalle/edit recuperables, éxito y conflicto. |
| `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` | Modify | Agrega casos `PATCH /{id}/reactivar` para 200/404/409. |
| `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` | Modify | Verifica que `/api/v1/unidades-organizativas/{id}/reactivar` quede documentado igual que otros módulos. |
| `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` | Modify | Agrega cobertura de `ReactivateAsync` restaurando flags de soft delete. |

## Interfaces / Contracts

```csharp
public interface IUnidadOrganizativaApiClient
{
    Task<UnidadOrganizativaCommandResult> ReactivateAsync(Guid id, CancellationToken cancellationToken = default);
}
```

No se agregan DTOs nuevos. La web reutiliza `UnidadOrganizativaCommandResult` para éxito, not-found y conflict. Las consultas `GET /api/v1/unidades-organizativas`, `/consulta`, `/arbol` y `GetByIdAsync` conservan comportamiento active-only.

## Testing Strategy

| Layer | What to Test | Approach |
|---|---|---|
| Aplicación/API contract | Reactivación exitosa, inexistente y en conflicto | Extender `UnidadesOrganizativasControllerTests` con `FakeUnidadOrganizativaServicioComandos`. |
| Persistencia MySQL | `ReactivateAsync` vuelve `IsActive=true`, `IsDeleted=false`, `DeletedAt=null` | Agregar integración en `UnidadOrganizativaRepositoryTests`. |
| Web integration | Banner de reactivación tras delete, conflicto desde listado, detail recuperable, edit recuperable, redirect exitoso a details con contexto | Extender `UnidadOrganizativaWebTests` y su `FakeUnidadOrganizativaApiClient` con `ReactivateAsync`. |

## Migration / Rollout

No migration required. El rollout queda limitado a `SGV.Web` y a pruebas de contrato/documentación del endpoint existente.

## Open Questions

- [ ] Ninguna bloqueante: el diseño asume que el listado solo necesita reactivar la última unidad eliminada en la sesión de navegación, tal como pide la spec del flujo.
