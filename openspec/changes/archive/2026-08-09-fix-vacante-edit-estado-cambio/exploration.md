## Exploration: Issue #268 — Edición de Vacante no funciona

### Estado actual del flujo Edit

El flujo completo de la edición de estado de una Vacante es:

1. **GET `/organizacion/vacantes/editar/{id}`** → `OnGetAsync` → `LoadCurrentAsync` (obtiene la vacante via `ObtenerPorIdAsync`) + `LoadStatesAsync` (carga catálogos de estado). `PopulateInput` alimenta el `InputModel` con los valores actuales, incluyendo `EstadoVacanteId`.

2. **POST** → `OnPostAsync(id)` → se llama a `LoadCurrentAsync` nuevamente (re-fetch), se valida `ModelState.IsValid`, y se invoca `vacanteApiClient.CambiarEstadoAsync(id, new CambiarEstadoVacanteRequest(Input.EstadoVacanteId!.Value, ...))`.

3. El cliente HTTP (`VacanteApiClient.CambiarEstadoAsync`) envía un `PATCH /api/v1/vacantes/{id}/estado` con el `CambiarEstadoVacanteRequest`.

4. El controller `VacantesController.CambiarEstado` recibe el request, llama a `VacanteServicioComandos.CambiarEstadoAsync`, que valida con `CambiarEstadoVacanteRequestValidator` (exige `EstadoVacanteId != Guid.Empty`), luego valida transiciones de estado terminal, y persiste el cambio atómicamente con EF.

5. En éxito, `OnPostAsync` hace `RedirectToPage("/Organizacion/Vacantes/Details", new { id })` (PRG).

### Causa raíz más probable (con evidencia)

**Hipótesis (a) — ALTA PROBABILIDAD**

El `<select>` de `EstadoVacanteId` en la vista tiene el atributo `disabled`:

```razor
@* src/SGV.Web/Pages/Organizacion/Vacantes/Edit.cshtml, línea 50 *@
<select asp-for="Input.EstadoVacanteId" class="form-select" disabled="@(Model.CatalogsReady ? null : "disabled")">
```

Cuando un `<select>` tiene `disabled`, **el navegador no envía su valor en el POST**. Esto significa que `Input.EstadoVacanteId` llega como `null` al handler.

Además, no existe un `<input type="hidden">` que sostenga el valor entre GET y POST (a diferencia de `PuestoId` y `FechaApertura` que sí tienen hidden fields en las líneas 45-46).

Esto produce que:
- `Input.EstadoVacanteId` es `null` en el POST.
- `Input.EstadoVacanteId!.Value` en la línea 95 de `Edit.cshtml.cs` lanza `NullReferenceException`... pero NO se atrapa en el bloque de `TransportFailureClassifier`, propagándose como exception no manejada que resulta en un 500 o una página de error.
- Alternativamente (si `!` está soportado de otra forma), la validación de FluentValidation `Guid.Empty` del `CambiarEstadoVacanteRequestValidator` rechaza el request con `Validation`, y el handler re-renderiza `Page()` sin mensaje de error visible porque el `select` está disabled y no puede cambiarse.

**Confirmación desde el test existente** (`Post_Edit_WhenSuccessful`, línea 302-348 de `VacantesCreateEditForbidTests.cs`): el test pasa porque usa `FormUrlEncodedContent` y el valor se inyecta directamente en el POST body, **bypaseando completamente la semántica de `disabled` del navegador real**. El test demuestra que la lógica del handler y API client funciona, pero no reproduce el bug del navegador real con campos disabled.

### Causas alternativas (clasificadas)

- **Media probabilidad — Hipótesis (d)**: El validador `CambiarEstadoVacanteRequestValidator` (línea 17-19 de `CambiarEstadoVacanteRequestValidator.cs`) exige `EstadoVacanteId != Guid.Empty`. Si llega `Guid.Empty` (valor por defecto de un Guid no seteado), la validación falla silenciosamente y el handler re-renderiza `Page()` sin feedback visible porque la UI no puede corregir el valor. Esto es consistente con (a) como causa raíz.

- **Baja probabilidad — Hipótesis (b)/(f)**: El handler no bindea `EstadoVacanteId` o no lo pasa al comando. Pero el código de `OnPostAsync` (línea 95) sí lo usa explícitamente: `Input.EstadoVacanteId!.Value`. Descartado tras lectura directa.

- **Baja probabilidad — Hipótesis (c)/(e)/(h)**: Problemas de cliente tipado o API. El `VacanteApiClient.CambiarEstadoAsync` (línea 121-144) maneja correctamente la respuesta y devuelve `VacanteCommandResult`. El controller (línea 190-203) devuelve `Ok(result.Value)` en éxito y `ToProblemResult` en error. El flujo es correcto.

- **Baja probabilidad — Hipótesis (g)**: El handler retorna `Page()` tras éxito. El código de la línea 112 hace `RedirectToPage("/Organizacion/Vacantes/Details", ...)` correctamente. No es el bug.

### Áreas afectadas

- `src/SGV.Web/Pages/Organizacion/Vacantes/Edit.cshtml:50` — el `<select>` con `disabled` es la causa directa. El valor seleccionado por el usuario nunca se envía al servidor.
- `src/SGV.Web/Pages/Organizacion/Vacantes/Edit.cshtml.cs:95` — uso de `Input.EstadoVacanteId!.Value` con fallback null que o bien lanza NRE o bien pasa `Guid.Empty` al validador.
- `src/SGV.Web/Integration/Vacantes/VacanteInputModel.cs` — el modelo tiene `EstadoVacanteId` como `Guid?` pero no existe hidden input que lo sostenga entre GET y POST.

### Tests existentes que cubren (parcialmente) el flujo

- `tests/SGV.Tests/Web/Vacantes/VacantesCreateEditForbidTests.cs`:
  - `Get_Edit_WhenMutationRole_PrepopulatesStateAndObservations` (línea 272): verifica GET de Edit, cobertura parcial.
  - `Post_Edit_WhenSuccessful_InvokesStateChangeAndRedirectsToDetails` (línea 302): **NO reproduce el bug** porque usa `FormUrlEncodedContent` directo, sin atravesar la semántica de `disabled` del navegador real. Cubre el happy path del handler + API client.
- **Gap**: No hay test que envíe el form real (con `<select disabled>`) y verifique que `EstadoVacanteId` llega como `null` al handler, ni test que verifique que la validación falla sin mensaje de error visible para el usuario.

### Tests de regresión sugeridos para el fix

1. **Test unitario de PageModel**: Given un `EditModel` con `Input.EstadoVacanteId = null`, cuando se llama `OnPostAsync`, entonces el resultado es un `Page()` con `ModelState` conteniendo un error en `"Input.EstadoVacanteId"`.

2. **Test de integración web**: Given un GET a Edit con un estado selecionado, cuando el form se postea con `EstadoVacanteId` ausente (como sucede con un `<select disabled>` en navegador real), entonces la respuesta es 200 con mensaje de error visible en el campo `EstadoVacanteId`.

3. **Test de la vista**: Given la página Edit, el `<select>` de `EstadoVacanteId` NO tiene el atributo `disabled`.

### Recomendación

El fix es cambiar `disabled` a `readonly` o `aria-disabled` con CSS que impida la interacción visual, Y agregar un hidden input que sostenga el valor para el binding: `<input type="hidden" asp-for="Input.EstadoVacanteId" />` junto al select no-disabled. Alternativamente, usar la técnica de `asp-items` con `@Html.DropDownListFor` que preserva el value binding.

**Primero**: cambiar la vista Edit.cshtml y verificar con un test de la página real que el POST ahora incluye `EstadoVacanteId`.

### Listo para propuesta

**No.** Antes de pasar a propose, el usuario debe decidir:

1. ¿Se desea que el campo de estado sea editable por todos los roles con `CanMutate` (Administrador y GestorVacantes) o solo por Administrador?
2. ¿El `disabled` se usaba como gate para evitar cambios de estado en ciertos escenarios (ej. vacantes ya cerradas)? Si había una intención, hay que replicarla con lógica de negocio, no con `disabled` en la UI.
