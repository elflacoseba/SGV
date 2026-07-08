# Design: Expone el botón Editar por fila en Puestos y cierra la frontera admin en PuestosController

## Technical Approach

Slice en dos partes que reusa patrones vigentes: (1) **frontend web**: espejar el botón "Editar" de `Cargos/Index` en `Puestos/Index`, porque la página `Edit` ya existe desde PR #93 y `Details` ya expone Editar desde PR #94; solo falta el entry point por fila que el spec canónico `puesto-web-listado-detalle-baja/spec.md:27` exige. (2) **backend API**: replicar el guard transversal aplicado en `CargosController` (archive `2026-07-01-cargos-crear-autorizacion-admin`) sobre `PuestosController`: `[Authorize]` a nivel controller + `[Authorize(Roles = RolesSgv.Administrador)]` en `Create`, `Update`, `Delete`, `Reactivate`. GETs (`GetAll`, `GetById`) solo cambian el requisito de acceso, no su contrato. Dominio/Aplicación/Infraestructura: **sin cambios**.

## Architecture Decisions

| # | Opción | Tradeoff | Decisión |
|---|--------|----------|----------|
| 1 | `[Authorize]` clase + overrides admin por método | Reduce repetición; sigue `UsuariosController.cs:11` y `CargosController.cs:16` | **Adoptada** |
|   | Atributos por método en cada acción | Duplica 6 líneas idénticas | Descartada |
|   | Policy global en `Program.cs` | Acopla controllers no relacionados | Descartada |
| 2 | Helper `BuildEditRouteValues(Guid id)` + `<a>` con `Url.Page("/Organizacion/Puestos/Edit", …)` | Espejo 1:1 de `Cargos/Index.cshtml.cs:237-244`; preserva contexto (`p`, `search`, `sort`, `returnStatus`) | **Adoptada** |
|   | Hardcodear `…/editar/{id}` | Pierde preservación de contexto | Descartada |
|   | Sub-item "Editar" en sidenav | Viola convención cross-módulo (Cargos/Habilidades/Puestos ya consolidan Edit por fila) | Descartada |
| 3 | Botón Editar solo en bloque `if (!Model.IsDeletedView)` de `Index.cshtml:187` | Puesto eliminado no es editable hasta reactivarse (espejo del comportamiento backend) | **Adoptada** |
|   | Permitir Editar también en Eliminadas | Rompe contrato: `IPuestoServicioConsulta.GetByIdAsync` solo devuelve activos | Descartada |
| 4 | Reusar `ApiWebApplicationFactory.{CreateAdminClient, CreateNonAdminClient}` | Harness ya extendido en archive Cargos (líneas 29-47, 770-815, 830-846); no modificar | **Adoptada** |
|   | Extender harness con un tercer role | Introduce variabilidad sin caso de uso | Descartada |

## Data Flow

```
GET /organizacion/puestos (anónimo → 302 /auth/sign-in por [Authorize] en IndexModel)
   ▼ usuario autenticado
IndexModel.OnGet → IPuestosApiClient.GetAll → GET /api/v1/puestos (bearer)
   ▼ render tabla con botones por fila
   ├─ Detalle:  Url.Page("/Organizacion/Puestos/Details", BuildDetailsRouteValues)  (existente)
   ├─ Editar:   Url.Page("/Organizacion/Puestos/Edit",    BuildEditRouteValues)    ← NUEVO
   └─ Eliminar: <form action="?handler=Delete"> (existente)

PUT /api/v1/puestos/{id}  (anónimo→401; autenticado no-admin→403; admin→200)  ← atributo NUEVO
   ▼ [Authorize(Roles = RolesSgv.Administrador)]
   ▼ IPuestoServicioComandos.ActualizarAsync → 200 OK / 400 / 404 / 409
   ▼ Página Edit → PRG → Index con TempData success
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs` | Modify | `public object BuildEditRouteValues(Guid id)` (espejo `CargosIndexModel.cs:237-244`). |
| `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml` | Modify | `<a class="btn btn-warning btn-icon btn-sm rounded-circle" href="@Url.Page("/Organizacion/Puestos/Edit", Model.BuildEditRouteValues(item.Id))" data-bs-toggle="tooltip" data-bs-title="Editar" aria-label="Editar @item.Nombre"><i class="ti ti-edit fs-lg"></i></a>` entre Detalle y `<form data-puesto-delete-form>`. Borrar comment obsoleto líneas 183-186. |
| `src/SGV.Api/Controllers/PuestosController.cs` | Modify | `using Microsoft.AspNetCore.Authorization;` + `using SGV.Aplicacion.Seguridad;`. `[Authorize]` clase (línea ~14). `[Authorize(Roles = RolesSgv.Administrador)]` en `Create`/`Update`/`Delete`/`Reactivate`. Añadir `[ProducesResponseType(401/403)]` correspondiente. |
| `tests/SGV.Tests/Web/Puesto/PuestoIndexPageTests.cs` | Modify | Extender `Get_Index_WhenAuthenticated_RendersActivePuestosTable` con asserts presencia (espejo `CargoIndexPageTests.cs:52-53`). Agregar RED `Get_Index_WhenDeletedView_RowHasNoEditButton`. |
| `tests/SGV.Tests/Api/PuestosControllerTests.cs` | Modify | Invertir `Controller_DoesNotHaveAuthorizeAttribute` → `Controller_HasAuthorizeAttribute`. Reemplazar `factory.CreateClient()` → `factory.CreateAdminClient()` (~14 ocurrencias). Agregar tests `*WithoutCredentials_ReturnsUnauthorized` y `*WithAuthenticatedNonAdmin_ReturnsForbidden` (mutaciones). |
| `openspec/specs/puesto-management/spec.md` | Sin cambios | Delta ya creado por `sdd-spec`. |
| `DatosSemilla.cs` y `Migraciones/20260706221558_*` | **Out** | Work-in-progress sin commitear (`exploration.md`). Aplicador debe aislar vía `git diff -- src/SGV.Web/Pages/Organizacion/Puestos tests/SGV.Tests/`. |

## Interfaces / Contracts

Sin nuevos contratos públicos. Shape JSON de `PuestoDto`, `PuestoError`, `ValidationProblemDetails` y códigos 200/201/204/400/404/409 vigentes se preservan. Helper nuevo en `IndexModel`:

```csharp
public object BuildEditRouteValues(Guid id) => new
{
    id,
    p = CurrentPage,
    search = Search,
    sort = Sort,
    returnStatus = Segmento   // espeja BuildDetailsRouteValues (no "status")
};
```

`returnStatus` (no `status`) es el nombre que `Puestos/Edit` acepta (`Edit.cshtml.cs:85-95`), idéntico al patrón de `Details`.

## Testing Strategy (strict TDD)

| Capa | Qué | Cómo |
|------|-----|------|
| Web (PageModel) | Fila activa expone botón Editar con href y `data-bs-title="Editar"` | Extender `PuestoIndexPageTests.Get_Index_WhenAuthenticated_RendersActivePuestosTable` con 2 asserts (espejo `CargoIndexPageTests.cs:52-53`). |
| Web | Fila eliminada NO expone Editar | Nuevo `Get_Index_WhenDeletedView_RowHasNoEditButton` con `Assert.DoesNotContain("data-bs-title=\"Editar\"", …)`. |
| API integration | `[Authorize]` declarado en el controller | `Controller_HasAuthorizeAttribute` (inversión del test actual). RED hasta agregar atributo. |
| API integration | Anónimo → `401` en GET y mutaciones | 6 tests `*WithoutCredentials_ReturnsUnauthorized` para `GetAll`/`GetById`/`Create`/`Update`/`Delete`/`Reactivate`. |
| API integration | No-admin → `403`; admin → `2xx` (no regresión) | `*WithAuthenticatedNonAdmin_ReturnsForbidden` (mutaciones); mantener `2xx` con `CreateAdminClient`. |

Convención crítica: tests existentes usan `factory.CreateClient()` pelado — al introducir `[Authorize]` **todos** deben migrar a `CreateAdminClient()` para seguir pasando `2xx` (`CargosControllerTests.cs:72-119`).

## Migration / Rollout

No migration required. No feature flag. Cambio backwards-compatible para admin (sin cambios observables), degradación segura para anónimos (`401` API + redirect sign-in) y autenticados sin rol (`403` en mutaciones que ya era error mapeado por `IPuestosApiClient.ToCommandResultAsync`).

## Open Questions

- Ninguna bloqueante. Decisiones heredadas de los archives `2026-07-06-implementa-modulo-puestos-en-frontend` y `2026-07-01-cargos-crear-autorizacion-admin`.
