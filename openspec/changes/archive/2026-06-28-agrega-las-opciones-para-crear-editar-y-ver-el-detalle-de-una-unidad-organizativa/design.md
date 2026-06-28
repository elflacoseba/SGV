# Design: Agrega las opciones para crear, editar y ver el detalle de una Unidad Organizativa

## Technical Approach

Se ampliará `SGV.Web` con tres Razor Pages autenticadas (`Create`, `Details`, `Edit`) apoyadas en un cliente HTTP tipado más rico, sin introducir lógica de negocio en la UI. El backend solo recibe un ajuste mínimo de contrato de lectura: `UnidadOrganizativaDto` expondrá `unidadPadreCodigo` y `unidadPadreNombre` para que detail/edit puedan mostrar contexto legible del padre sin consultas extra por fila.

## Architecture Decisions

| Tema | Opciones | Decisión | Rationale |
|---|---|---|---|
| Pantallas web | Una sola página modal vs páginas Razor separadas | `Create`, `Details` y `Edit` como páginas independientes | Sigue el patrón actual de `SGV.Web`, facilita PRG, antiforgery y pruebas con `WebApplicationFactory`. |
| Selector de padre | Reusar `GET /consulta` plano vs `GET /arbol` | Consumir `GET /api/v1/unidades-organizativas/arbol` y aplanar en opciones indentadas | Permite mostrar jerarquía legible y excluir `self` + descendientes en edit antes del submit. |
| Riesgo de fallo parcial en edit | Nuevo endpoint transaccional vs composición web de `PUT` + `PATCH` | Mantener contratos actuales; ejecutar `PUT` primero y `PATCH` solo si cambia el padre, con warning explícito si el segundo paso falla | Evita rediseño backend. Si `PUT` falla no hay cambios; si `PATCH` falla, la UI refleja que los datos generales sí quedaron guardados y guía el reintento del padre. |
| Traducción de errores | Pasar `ProblemDetails` crudo | Mapear `ValidationProblemDetails` a `ModelState` y `ProblemDetails` a banner accionable | Cumple el requerimiento de feedback claro y preserva datos ingresados. |

## Data Flow

### Carga de create/edit/detail

`GET page` → `PageModel.OnGetAsync` → `IUnidadOrganizativaApiClient`
→ `GET /tipos-unidad-organizativa`
→ `GET /unidades-organizativas/arbol`
→ (`edit/detail`) `GET /unidades-organizativas/{id}`
→ map a `InputModel` + opciones de padre + botón “Volver” con `returnPage/search/sort`.

### Guardado edit con posible cambio de padre

```text
Browser -> EditModel.OnPostAsync
EditModel -> PUT /api/v1/unidades-organizativas/{id}
PUT 400/409/404 -> ModelState/banner + recarga de catálogos
PUT 200 y padre sin cambio -> RedirectToPage(Details)
PUT 200 y padre cambió -> PATCH /api/v1/unidades-organizativas/{id}/unidad-padre
PATCH 200 -> RedirectToPage(Details) con success
PATCH 400/404/409 -> RedirectToPage(Edit) con warning de éxito parcial y refresh desde GET /{id}
```

Mitigación elegida: NO se simula atomicidad falsa. El warning debe explicar “Se guardaron los datos generales, pero no se pudo actualizar la unidad padre” y sugerir corregir jerarquía o reintentar.

## File Changes

| File | Action | Description |
|---|---|---|
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml` | Modify | Agrega botón crear y acciones por fila para detalle/editar preservando `page/search/sort`. |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs` | Modify | Expone helpers para construir `return*` y mensajes de navegación. |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Create.cshtml` + `.cs` | Create | Formulario SSR con catálogos, validación, PRG y success message. |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml` + `.cs` | Create | Edición con snapshot del padre original, flujo `PUT`/`PATCH`, warning de éxito parcial y estado recuperable. |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Details.cshtml` + `.cs` | Create | Vista read-only con padre legible, acciones de editar y volver. |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/_Form.cshtml` | Create | Parcial compartido para campos y summary de errores. |
| `src/SGV.Web/Integration/Organizacion/IUnidadOrganizativaApiClient.cs` | Modify | Agrega `GetByIdAsync`, `GetTreeAsync`, `GetTiposAsync`, `CreateAsync`, `UpdateAsync`, `ChangeParentAsync`. |
| `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs` | Modify | Serializa requests y traduce `ValidationProblemDetails`/`ProblemDetails` a resultados web. |
| `src/SGV.Web/Integration/Organizacion/*ViewModel.cs` | Modify/Create | `InputModel`, `ParentOptionViewModel`, `WriteResult` y helper de navegación. |
| `src/SGV.Web/Program.cs` | Modify | Mantiene un solo cliente tipado; no requiere nuevos registrations externos. |
| `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/UnidadOrganizativaDto.cs` | Modify | Agrega `UnidadPadreCodigo` y `UnidadPadreNombre`. |
| `src/SGV.Aplicacion/Organizacion/Consultas/UnidadOrganizativaServicioConsulta.cs` | Modify | Mapea el resumen del padre. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs` | Modify | Incluye navegación `UnidadPadre` en `GetById`/`QueryAsync` para sostener el DTO enriquecido. |
| `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioConsultaTests.cs` | Modify | Verifica mapeo de `unidadPadreCodigo/nombre`. |
| `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` | Modify | Aserta el contrato JSON enriquecido en `GET /{id}` y `/consulta`. |
| `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Modify | Cobertura de auth, create/detail/edit, validaciones, warning parcial y conservación de contexto. |

## Interfaces / Contracts

```csharp
public sealed record UnidadOrganizativaDto(
    Guid Id,
    string Codigo,
    string Nombre,
    Guid TipoUnidadOrganizativaId,
    string TipoUnidadNombre,
    string? Descripcion,
    DateOnly? VigenteDesde,
    DateOnly? VigenteHasta,
    Guid? UnidadPadreId,
    string? UnidadPadreCodigo,
    string? UnidadPadreNombre);
```

`WriteResult` web encapsulará: `Succeeded`, `StatusCode`, `Summary`, `FieldErrors`, `ProblemCode`.

## Testing Strategy

| Layer | What to Test | Approach |
|---|---|---|
| Aplicación | DTO enriquecido y nulls para unidad raíz | RED con `UnidadOrganizativaServicioConsultaTests`, luego ajuste en servicio/repositorio fake. |
| API | JSON de lectura incluye `unidadPadreCodigo/nombre` | Extender `UnidadesOrganizativasControllerTests` existentes. |
| Web | redirect anónimo, render create/detail/edit, no disponible, validación por campo, éxito, warning parcial por `PATCH` fallido y retorno al listado | Extender `UnidadOrganizativaWebTests` con `FakeUnidadOrganizativaApiClient`; mantener patrón actual de `WebApplicationFactory`. |

## Migration / Rollout

No migration required. El rollout queda limitado a `SGV.Web` y a enriquecer el DTO de lectura existente.

## Open Questions

- [ ] Ninguna bloqueante para `sdd-tasks`; el warning de éxito parcial queda definido como comportamiento esperado, no como error abierto.
