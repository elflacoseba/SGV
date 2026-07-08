# Tareas: Expone el botón Editar por fila en Puestos y cierra la frontera admin en PuestosController

> Cambio quirúrgico (~80-125 LoC) en 4 archivos: 2 slices paralelos que reusan patrones vigentes de Cargos (helper + `<a>` en `Index` y guard `[Authorize]` en `PuestosController`). Strict TDD: RED → GREEN → REFACTOR. El working tree actual trae `DatosSemilla.cs` + migración `20260706221558_*` sin commitear; el aplicador debe aislar **solo** los archivos listados vía `git diff -- src/SGV.Web/Pages/Organizacion/Puestos src/SGV.Api/Controllers/PuestosController.cs tests/SGV.Tests/...`.

## Revisión de carga de trabajo

| Campo | Valor |
|-------|-------|
| Líneas modificadas estimadas | ~80-125 |
| Riesgo vs. budget de 400 LoC | Bajo |
| Chained PRs recomendado | No |
| Cadena sugerida | Pendiente (un solo PR contra `develop`) |
| Estrategia de entrega | ask-on-risk (sin chained, no requiere pausa) |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Low

## Fase 1 — Frontend: helper y wiring del botón Editar

- [ ] 1.1 Agregar `public object BuildEditRouteValues(Guid id)` en `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs` con `id`, `p = CurrentPage`, `search = Search`, `sort = Sort`, `returnStatus = Segmento`.
- [ ] 1.2 Insertar en `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml:189` el `<a class="btn btn-warning btn-icon btn-sm rounded-circle" href="@Url.Page("/Organizacion/Puestos/Edit", Model.BuildEditRouteValues(item.Id))" data-bs-toggle="tooltip" data-bs-title="Editar" aria-label="Editar @item.Nombre"><i class="ti ti-edit fs-lg"></i></a>` entre el botón `Detalle` y el `<form data-puesto-delete-form>`.
- [ ] 1.3 Borrar el comment obsoleto en `Index.cshtml:183-186` ("PR 2 — solo Detalle y Eliminar…").

## Fase 2 — Backend: guard admin en `PuestosController`

- [ ] 2.1 Agregar `using Microsoft.AspNetCore.Authorization;` y `using SGV.Aplicacion.Seguridad;` en `src/SGV.Api/Controllers/PuestosController.cs`.
- [ ] 2.2 Aplicar `[Authorize]` a nivel clase (línea ~14).
- [ ] 2.3 Aplicar `[Authorize(Roles = RolesSgv.Administrador)]` en `Create` (`~63`), `Update` (`~87`), `Delete` (`~111`), `Reactivate` (`~130`).
- [ ] 2.4 Agregar `[ProducesResponseType(StatusCodes.Status401Unauthorized)]` en `GetAll`/`GetById` y `[ProducesResponseType(StatusCodes.Status401Unauthorized)]` + `[ProducesResponseType(StatusCodes.Status403Forbidden)]` en `Create`/`Update`/`Delete`/`Reactivate` para reflejar el delta `puesto-management`.

## Fase 3 — Tests web: presencia/ausencia del botón Editar

- [ ] 3.1 Extender `tests/SGV.Tests/Web/Puesto/PuestoIndexPageTests.cs::Get_Index_WhenAuthenticated_RendersActivePuestosTable` con asserts `$"/organizacion/puestos/editar/{first.Id}"` + `"data-bs-title=\"Editar\""`. RED hasta 1.1-1.3 verdes.
- [ ] 3.2 Agregar test RED `Get_Index_WhenDeletedView_DoesNotRenderEditButton` con `Assert.DoesNotContain("data-bs-title=\"Editar\"", …)` para el segmento `status=eliminadas`.

## Fase 4 — Tests API: matriz 401/403/2xx

- [ ] 4.1 Invertir `Controller_DoesNotHaveAuthorizeAttribute` → `Controller_HasAuthorizeAttribute` en `tests/SGV.Tests/Api/PuestosControllerTests.cs`. RED hasta 2.2 verde.
- [ ] 4.2 Migrar los `factory.CreateClient()` existentes (~14 ocurrencias) a `factory.CreateAdminClient()` para que la suite 2xx siga verde tras `[Authorize]`.
- [ ] 4.3 Agregar `GetAll_WithoutCredentials_ReturnsUnauthorized` y `GetById_WithoutCredentials_ReturnsUnauthorized` con `factory.CreateClient()` pelado. RED hasta 2.2 verde.
- [ ] 4.4 Agregar `Mutation_WithoutCredentials_ReturnsUnauthorized` con `[Theory]` + `[InlineData]` cubriendo POST/PUT/DELETE/PATCH. RED hasta 2.2 verde.
- [ ] 4.5 Agregar `Create_WithAuthenticatedNonAdmin_ReturnsForbidden`, `Update_WithAuthenticatedNonAdmin_ReturnsForbidden`, `Delete_WithAuthenticatedNonAdmin_ReturnsForbidden`, `Reactivate_WithAuthenticatedNonAdmin_ReturnsForbidden` con header `FakeAuthenticationDefaults.UserHeader`. RED hasta 2.3 verde.

## Fase 5 — Validación y aislamiento del working tree

- [ ] 5.1 Ejecutar `dotnet build SGV.slnx` y resolver regresiones.
- [ ] 5.2 Ejecutar `dotnet test SGV.slnx` — la suite (406+ tests) debe pasar sin regresión.
- [ ] 5.3 Antes del commit, ejecutar `git diff -- src/SGV.Web/Pages/Organizacion/Puestos src/SGV.Api/Controllers/PuestosController.cs tests/SGV.Tests/`. Si aparecen `DatosSemilla.cs` o `Migraciones/20260706221558_*`, abortar y reagrupar con el usuario.
