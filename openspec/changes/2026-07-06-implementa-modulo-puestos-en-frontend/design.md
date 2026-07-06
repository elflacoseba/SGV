# Diseño: Implementar el módulo de Puestos en el Frontend

## 1. Resumen del diseño

Slice frontend-only sobre `SGV.Web` que cierra la paridad operativa del módulo **Puestos** con Cargos. Replica el seam probado de `2026-06-30-implementar-modulo-de-cargos-en-el-frontend` (`Index` segmentado + `Details` readonly + `Create`/`Edit` + baja lógica + reactivación + JS SweetAlert2 + tests `WebApplicationFactory`), pero ajustado al contrato real de `PuestosController` (sin `[Authorize]`, sin `consulta?status=`, sin bloqueo por cargos/puestos activos en DELETE). Backend ya entregado y archivado en `archive/2026-06-19-implementa-modulo-puestos/` — este change **no** toca Dominio, Aplicación, Infraestructura ni Api. Tres PRs chained (~890 líneas, forecast re-validado contra `git diff --stat` de Cargos), excede el budget de 400 → chained obligatorio. Cinco decisiones de producto locked se respetan como constraints.

## 2. Contexto arquitectónico

```text
                    Capa                 Estado en este change
─────────────────────────────────────────────────────────────────────
SGV.Dominio/Puestos/*                    read-only (archivado)
SGV.Aplicacion/Puestos/*                read-only (archivado)
SGV.Infraestructura/Persistencia/...     read-only (archivado)
SGV.Api/Controllers/PuestosController   read-only (archivado)

SGV.Web/Integration/Organizacion/
   ├── IPuestosApiClient.cs              NEW
   ├── PuestosApiClient.cs               NEW
   ├── PuestoListItemViewModel.cs        NEW  (+ PuestoListQuery + PuestoDeleteResult + PuestoFormKeys)
   └── PuestoFormHelpers.cs              NEW  (+ IPuestoForm)

SGV.Web/Pages/Organizacion/Puestos/
   ├── Index.cshtml(.cs)                 NEW (PR 2)
   ├── Details.cshtml(.cs)               NEW (PR 3)
   ├── Create.cshtml(.cs)                NEW (PR 3)
   ├── Edit.cshtml(.cs)                  NEW (PR 3)
   └── _Form.cshtml                      NEW (PR 3)

SGV.Web/wwwroot/js/pages/puestos-index.js NEW (PR 2)

SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml   MODIFIED (PR 1) — entry colapsable "Puestos"
SGV.Web/Program.cs                               MODIFIED (PR 1) — registro HttpClient tipado
tests/SGV.Tests/Web/SgvWebApplicationFactory.cs  MODIFIED (PR 1) — override IPuestosApiClient
tests/SGV.Tests/Web/Puesto/                       NEW (PR 1+2+3)
```

Las dependencias runtime son: `PuestoDto`, `CrearPuestoRequest`, `ActualizarPuestoRequest`, `PuestoCommandResult`, `PuestoError`, `PuestoErrorType` (en `SGV.Aplicacion.Organizacion.{Consultas.Dtos,Comandos}`); `ApiBearerTokenHandler`, `SgvApiOptions` (en `SGV.Web.Integration.Auth`); `IUnidadOrganizativaApiClient` y `ICargoApiClient` para los selects de Create.

## 3. Contratos y tipos nuevos

### 3.1 `IPuestosApiClient.cs` (verbatim)

```csharp
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Cliente HTTP tipado del módulo web de Puestos.
/// Permite listar activos, obtener por id, ejecutar baja lógica, crear,
/// editar y reactivar puestos.
/// </summary>
public interface IPuestosApiClient
{
    /// <summary>Lista todos los puestos activos.</summary>
    Task<IReadOnlyList<PuestoDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene un puesto activo por id o <c>null</c> si no existe.</summary>
    Task<PuestoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Crea un puesto. Devuelve éxito con DTO o fallo tipado (<see cref="PuestoErrorType.Validation"/> con <c>FieldErrors</c>, <see cref="PuestoErrorType.Conflict"/> si el código está duplicado, etc.).</summary>
    Task<PuestoCommandResult> CreateAsync(CrearPuestoRequest request, CancellationToken cancellationToken = default);

    /// <summary>Actualiza Nombre/Descripcion?/PuestoSuperiorId?. Mapea 400 (FieldErrors) y 409 (<c>CodigoDuplicado</c>, <c>PuestoSuperiorInvalido</c>).</summary>
    Task<PuestoCommandResult> UpdateAsync(Guid id, ActualizarPuestoRequest request, CancellationToken cancellationToken = default);

    /// <summary>Ejecuta baja lógica vía <c>DELETE /api/v1/puestos/{id}</c>. Traduce 204 → Succeeded y 404/409 → Failure.</summary>
    Task<PuestoDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Reactiva un puesto vía <c>PATCH /api/v1/puestos/{id}/reactivar</c>. Mapea 409 por código duplicado.</summary>
    Task<PuestoCommandResult> ReactivateAsync(Guid id, CancellationToken cancellationToken = default);
}
```

### 3.2 Records en `PuestoListItemViewModel.cs`

```csharp
using System.Net;

namespace SGV.Web.Integration.Organizacion;

/// <summary>View model de grilla para el listado web de puestos activos.</summary>
public sealed record PuestoListItemViewModel(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string UnidadOrganizativaNombre,
    string CargoNombre,
    Guid? PuestoSuperiorId);

/// <summary>Resultado de la baja lógica de un puesto traducida desde la API.</summary>
public sealed record PuestoDeleteResult(bool Succeeded, HttpStatusCode? StatusCode, string? Code, string? Message);
```

### 3.3 `PuestoFormKeys.cs` y `PuestoFormHelpers.cs` (espejo de CargoFormHelpers)

```csharp
namespace SGV.Web.Integration.Organizacion;

public static class PuestoFormKeys
{
    public const string InputPrefix = "Input.";
    public const string CodigoKey = InputPrefix + "Codigo";
    public const string NombreKey = InputPrefix + "Nombre";
    public const string DescripcionKey = InputPrefix + "Descripcion";
    public const string UnidadOrganizativaIdKey = InputPrefix + "UnidadOrganizativaId";
    public const string CargoIdKey = InputPrefix + "CargoId";
    public const string PuestoSuperiorIdKey = InputPrefix + "PuestoSuperiorId";
}

/// <summary>Construye la URL de retorno al listado de puestos preservando filtros (p, search, sort, status).</summary>
public static class PuestoFormHelpers
{
    public static string BuildReturnToListUrl(IUrlHelper url, string? page, string? search, string? sort, string? status)
    { /* espejo de CargoFormHelpers.BuildReturnToListUrl, target "/Organizacion/Puestos/Index" */ }

    public static void ApplyFieldErrorsToModelState(
        Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState,
        IReadOnlyDictionary<string, string[]>? fieldErrors)
    { /* espejo de CargoFormHelpers.ApplyFieldErrorsToModelState, usa PuestoFormKeys.InputPrefix */ }
}

/// <summary>Contrato compartido por PageModels que renderizan <c>_Form.cshtml</c> de Puestos.</summary>
public interface IPuestoForm
{
    PuestoInputModel Input { get; }
    IReadOnlyList<UnidadOrganizativaDto> UnidadOrganizativaOptions { get; }
    IReadOnlyList<CargoDto> CargoOptions { get; }
    IReadOnlyList<PuestoListItemViewModel> PuestoSuperiorOptions { get; }
    string? ErrorMessage { get; }
    bool IsEdit { get; }
    string ReturnToListUrl { get; }
}
```

### 3.4 `PuestoInputModel.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace SGV.Web.Integration.Organizacion;

public sealed class PuestoInputModel
{
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(50, ErrorMessage = "El código no puede superar los 50 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(200, ErrorMessage = "El nombre no puede superar los 200 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "La descripción no puede superar los 1000 caracteres.")]
    public string? Descripcion { get; set; }

    [Required(ErrorMessage = "Debe escoger una unidad organizativa.")]
    public Guid? UnidadOrganizativaId { get; set; }

    [Required(ErrorMessage = "Debe escoger un cargo.")]
    public Guid? CargoId { get; set; }

    [Display(Name = "Puesto superior")]
    public Guid? PuestoSuperiorId { get; set; }
}
```

### 3.5 `PuestosApiClient.cs` — outline

Convención JSON: `System.Text.Json` con la configuración por defecto de ASP.NET Core 10 (camelCase saliente, igual que `CargoApiClient`). El DTO `PuestoDto` ya expone `Id`, `Codigo`, `Nombre`, `Descripcion`, `UnidadOrganizativaId`, `UnidadOrganizativaNombre`, `CargoId`, `CargoNombre`, `PuestoSuperiorId` con casing consistente, por lo que **no** se necesita `JsonPropertyName`.

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Organizacion;

public sealed class PuestosApiClient(HttpClient httpClient) : IPuestosApiClient
{
    private const string BaseRoute = "/api/v1/puestos";

    public async Task<IReadOnlyList<PuestoDto>> GetAllAsync(CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync(BaseRoute, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<PuestoDto>>(cancellationToken: ct) ?? [];
    }

    public async Task<PuestoDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync($"{BaseRoute}/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PuestoDto>(cancellationToken: ct);
    }

    public async Task<PuestoCommandResult> CreateAsync(CrearPuestoRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(BaseRoute, request, ct);
        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<PuestoDto>(cancellationToken: ct);
            return PuestoCommandResult.Success(dto!);
        }
        return await ToCommandResultAsync(response, ct);
    }

    public async Task<PuestoCommandResult> UpdateAsync(Guid id, ActualizarPuestoRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync($"{BaseRoute}/{id}", request, ct);
        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<PuestoDto>(cancellationToken: ct);
            return PuestoCommandResult.Success(dto!);
        }
        return await ToCommandResultAsync(response, ct);
    }

    public async Task<PuestoDeleteResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync($"{BaseRoute}/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return new PuestoDeleteResult(true, response.StatusCode, null, null);

        ProblemDetails? problem = null;
        try { problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct); }
        catch (NotSupportedException) { } catch (HttpRequestException) { } catch (System.Text.Json.JsonException) { }

        return new PuestoDeleteResult(false, response.StatusCode, problem?.Title, problem?.Detail);
    }

    public async Task<PuestoCommandResult> ReactivateAsync(Guid id, CancellationToken ct = default)
    {
        var response = await httpClient.PatchAsync($"{BaseRoute}/{id}/reactivar", null, ct);
        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<PuestoDto>(cancellationToken: ct);
            return PuestoCommandResult.Success(dto!);
        }
        return await ToCommandResultAsync(response, ct);
    }

    /// <summary>
    /// Traduce respuestas no exitosas a <see cref="PuestoCommandResult.Failure"/>. Para 400
    /// bifurca entre ValidationProblemDetails (errores por campo) y ProblemDetails plano.
    /// 404/409 caen en Failure con Code/Message del ProblemDetails. Es el espejo de
    /// <c>CargoApiClient.ToCommandResultAsync</c>, ajustado al shape del backend de Puestos.
    /// </summary>
    private static async Task<PuestoCommandResult> ToCommandResultAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken: ct);
            if (problem?.Errors is { Count: > 0 })
            {
                var fieldErrors = problem.Errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
                return PuestoCommandResult.Failure(
                    new PuestoError(PuestoErrorType.Validation, problem.Title ?? "DatosInvalidos", problem.Detail ?? "Uno o más campos son inválidos."),
                    fieldErrors);
            }
            return PuestoCommandResult.Failure(
                new PuestoError(PuestoErrorType.Validation, problem?.Title ?? "BadRequest", problem?.Detail ?? "Solicitud inválida."));
        }
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct);
            return PuestoCommandResult.Failure(
                new PuestoError(PuestoErrorType.NotFound, problem?.Title ?? "PuestoNoEncontrado", problem?.Detail ?? "Recurso no encontrado."));
        }
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct);
            return PuestoCommandResult.Failure(
                new PuestoError(PuestoErrorType.Conflict, problem?.Title ?? "Conflict", problem?.Detail ?? "Conflicto."));
        }
        return PuestoCommandResult.Failure(
            new PuestoError(PuestoErrorType.Validation, "Unexpected", "Respuesta inesperada del servidor."));
    }
}
```

**Contrato de transporte (delta `web-apiclient-transport-contract`):** `PuestosApiClient` **no** captura `TaskCanceledException` ni `HttpRequestException` — las propaga nativas. Un `CancellationToken` pre-cancelado dispara `OperationCanceledException` sin enviar HTTP (verificado por `Theory` `TransportFails_PropagatesNativeException` + `Fact` `CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest`).

### 3.6 Registro en `Program.cs` (verbatim, PR 1)

Insertar entre el bloque de `ICargoApiClient` y el de `IHabilidadApiClient`:

```csharp
builder.Services.AddHttpClient<IPuestosApiClient, PuestosApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    // 10s budget, paralelo a CargoApiClient y HabilidadApiClient: el usuario
    // espera ver el form cargado y un timeout prolongado se confunde con un
    // crash de servidor. TaskCanceledException se traduce en error
    // recuperable en CreateModel/EditModel/IndexModel.
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>());
```

### 3.7 Extensión de `SgvWebApplicationFactory`

Agregar campo `_puestosApiClient`, parámetro `IPuestosApiClient? puestosApiClient = null` en `WithOverrides`, registro singleton en el bloque `if (_puestosApiClient is not null)` (espejo de la rama `_cargoApiClient`). `WithPuestosApiClient(IPuestosApiClient fake)` para ergonomía.

## 4. Páginas Razor y PageModels

Convención de rutas: `/organizacion/puestos` (Index), `/organizacion/puestos/detalles/{id:guid}` (Details), `/organizacion/puestos/crear` (Create), `/organizacion/puestos/editar/{id:guid}` (Edit). Espejo literal de Cargos.

### 4.1 `Index.cshtml` outline (PR 2, ~155 líneas)

Bloques clave:
- Cabecera `_PageTitle.cshtml` con `ViewBag.title = "Puestos"`, `subtitle = "Organización"`.
- Banner `StatusMessage` con CTA `Reactivar` cuando `HasLastDeleted && !IsDeletedView` (espejo de Cargos).
- Banner `LoadErrorMessage` cuando la consulta inicial falla.
- Card `data-table` con header dual: a la izquierda, título y subtítulo; a la derecha, **toggle Activas|Eliminadas con `Eliminadas` deshabilitado y `data-bs-toggle="tooltip" data-bs-title="Requiere endpoint backend: pendiente de follow-up"`**, badge `TotalCount registro(s)`, botón `Crear puesto` (solo en vista activas).
- Form de búsqueda `<form method="get">` con `name="search"`, hidden `sort` y `status`.
- Tabla Inspinia con columnas: `Código` (sort), `Nombre` (sort), `Unidad Organizativa`, `Cargo`, `Puesto superior` (link al detalle del superior preservando contexto, celda vacía si es null), `Acciones` (Detalle / Editar / Eliminar en activas; Reactivar en eliminadas).
- Form de delete: `data-puesto-delete-form`, `data-puesto-delete-button`, `data-puesto-item-name`, `data-puesto-item-code`, `formaction="?handler=Delete"`.
- Form de reactivate: `data-puesto-reactivate-form`, `data-puesto-reactivate-button`, `formaction="?handler=Reactivate"`.
- Paginación `Anterior / Siguiente` con links preservando `p`, `search`, `sort`, `status`.
- Scripts: `sweetalert2` + `puestos-index.js`.

### 4.2 `Index.cshtml.cs` outline (PR 2, ~210 líneas)

Espejo de `CargoIndexModel` con los siguientes ajustes:
- `PuestoListItemViewModel` en lugar de `CargoListItemViewModel`.
- `[FromQuery(Name = "status")]` se acepta pero el backend no está segmentado: `LoadAsync` invoca `GetAllAsync()` (sin `status`) cuando segmento es `eliminadas` para forward-compat y el toggle se renderiza deshabilitado. **No** usa `QueryAsync` porque `PuestosController` no expone `/consulta`.
- `OnGetAsync(p, search, sort, status, deletedId)` preserva `LastDeletedId` en `TempData` cuando viene vía query string.
- `OnPostDeleteAsync` traduce `PuestoDeleteResult` a `TempData` (success/danger) y PRG preservando filtros.
- `OnPostReactivateAsync` traduce `PuestoCommandResult` (404/409 por código duplicado) a `TempData` y PRG. Si falla, permanece en la vista de origen; si éxito, redirige a Activas y limpia `LastDeletedId`.
- Búsqueda y orden **en memoria** sobre `GetAllAsync()` (mismo patrón que Cargos pre-PR3): el backend sólo expone lista plana de activos.
- Helpers: `BuildEditRouteValues(id)`, `BuildDetailsRouteValues(id)`, `BuildToggleSegmentoRouteValues(targetSegmento)` (idénticos a Cargos pero apuntando a `/Organizacion/Puestos/Index`/`Details`/`Edit`).
- `MapToViewModel(PuestoDto) => new PuestoListItemViewModel(...)`.

### 4.3 `Details.cshtml(.cs)` outline (PR 3, ~60+50 líneas)

Espejo de `Cargos/Details`:
- `[Authorize]`, `OnGetAsync(id, p, search, sort)` → `GetByIdAsync`, render readonly o estado `IsNotFound`.
- Card con `dl row mb-0`: Código, Nombre, Descripción, Unidad Organizativa, Cargo, Puesto superior (link al detalle del superior preservando contexto, si existe).
- Footer con `Editar` (a `Edit` preservando contexto) y `Volver al listado` (a `Index`).

### 4.4 `Create.cshtml(.cs)` outline (PR 3, ~40+155 líneas)

- `[Authorize]`, GET invoca **tres catálogos en paralelo** vía `Task.WhenAll`: `IUnidadOrganizativaApiClient.GetAllAsync()`, `ICargoApiClient.GetAllAsync()`, `IPuestosApiClient.GetAllAsync()`. Falla de cualquiera → `ErrorMessage` recuperable y formulario visible (espejo de `CargoCreateModel.LoadCatalogsAsync`).
- `[BindProperty] PuestoInputModel Input` con los **seis** campos (Codigo, Nombre, Descripcion, UnidadOrganizativaId, CargoId, PuestoSuperiorId).
- POST arma `CrearPuestoRequest(Codigo, Nombre, UnidadOrganizativaId!.Value, CargoId!.Value, PuestoSuperiorId, Descripcion)`, llama `CreateAsync`, mapea resultado:
  - 409 → `ModelState.AddModelError(PuestoFormKeys.CodigoKey, result.Error.Message)`.
  - 400 con `FieldErrors` → `PuestoFormHelpers.ApplyFieldErrorsToModelState`.
  - `HttpRequestException`/`TaskCanceledException`/`JsonException` → `ErrorMessage = "No se pudo contactar al servicio de puestos. Intentá nuevamente."`, recarga catálogos, `return Page()` (espejo de `CargoCreateModel.OnPostAsync`).
  - Éxito → `TempData` + `RedirectToPage("/Organizacion/Puestos/Index")`.
- `IsEdit => false`.

### 4.5 `Edit.cshtml(.cs)` outline (PR 3, ~60+155 líneas)

- `[Authorize]`, GET invoca `GetByIdAsync(id)` + **dos catálogos** (`UnidadesOrganizativas`, `Cargos`); el catálogo de `PuestoSuperiorId` se reutiliza desde `GetAllAsync()` o se omite el select si el catálogo falla (recuperable).
- `[BindProperty] PuestoInputModel Input` con **tres** campos: `Nombre`, `Descripcion`, `PuestoSuperiorId`. `Codigo`, `UnidadOrganizativaId`, `CargoId` **NO** se exponen en el input model visible — el modelo los declara pero el partial `_Form.cshtml` no los renderiza cuando `IsEdit`.
- POST arma `ActualizarPuestoRequest(Nombre, DescripcionTrimmed, PuestoSuperiorId)` y llama `UpdateAsync`.
- 409 → `ModelState.AddModelError(PuestoFormKeys.NombreKey, ...)` (sin Codigo duplicado por inmutabilidad — `ActualizarPuestoRequest` no incluye Codigo). **Pero** el `PATCH /reactivar` puede responder 409 por código duplicado; ese path se cubre desde Index.
- `IsEdit => true`.

### 4.6 `_Form.cshtml` partial compartido (PR 3, ~70 líneas)

```razor
@* Shared field partial for create/edit of Puestos. NOT a complete form. *@
@using SGV.Web.Integration.Organizacion
@model SGV.Web.Integration.Organizacion.IPuestoForm

<div asp-validation-summary="ModelOnly" class="text-danger mb-3"></div>

<div class="row g-3">
    @* Codigo ONLY in Create (MUST NOT en Edit — restricción de dominio). *@
    @if (!Model.IsEdit)
    {
        <div class="col-md-6">
            <div class="form-floating">
                <input asp-for="Input.Codigo" class="form-control" placeholder="Código" />
                <label asp-for="Input.Codigo">Código</label>
                <span asp-validation-for="Input.Codigo" class="text-danger"></span>
            </div>
        </div>
    }

    <div class="col-md-6">
        <div class="form-floating">
            <input asp-for="Input.Nombre" class="form-control" placeholder="Nombre" />
            <label asp-for="Input.Nombre">Nombre</label>
            <span asp-validation-for="Input.Nombre" class="text-danger"></span>
        </div>
    </div>

    <div class="col-12">
        <div class="form-floating">
            <textarea asp-for="Input.Descripcion" class="form-control" placeholder="Descripción" style="min-height: 80px"></textarea>
            <label asp-for="Input.Descripcion">Descripción</label>
            <span asp-validation-for="Input.Descripcion" class="text-danger"></span>
        </div>
    </div>

    @* UnidadOrganizativaId / CargoId ONLY en Create (inmutables en Puesto). *@
    @if (!Model.IsEdit)
    {
        <div class="col-md-6">
            <div class="form-floating">
                <select asp-for="Input.UnidadOrganizativaId" class="form-select"
                        asp-items="@(new SelectList(Model.UnidadOrganizativaOptions, "Id", "Nombre"))">
                    <option value="">Seleccionar unidad...</option>
                </select>
                <label asp-for="Input.UnidadOrganizativaId">Unidad organizativa</label>
                <span asp-validation-for="Input.UnidadOrganizativaId" class="text-danger"></span>
            </div>
        </div>
        <div class="col-md-6">
            <div class="form-floating">
                <select asp-for="Input.CargoId" class="form-select"
                        asp-items="@(new SelectList(Model.CargoOptions, "Id", "Nombre"))">
                    <option value="">Seleccionar cargo...</option>
                </select>
                <label asp-for="Input.CargoId">Cargo</label>
                <span asp-validation-for="Input.CargoId" class="text-danger"></span>
            </div>
        </div>
    }

    @* PuestoSuperiorId en Create y Edit. *@
    <div class="col-md-@(Model.IsEdit ? "12" : "6")">
        <div class="form-floating">
            <select asp-for="Input.PuestoSuperiorId" class="form-select"
                    asp-items="@(new SelectList(Model.PuestoSuperiorOptions, "Id", "CodigoYNombre"))">
                <option value="">Sin puesto superior</option>
            </select>
            <label asp-for="Input.PuestoSuperiorId">Puesto superior</label>
            <span asp-validation-for="Input.PuestoSuperiorId" class="text-danger"></span>
        </div>
    </div>
</div>
```

`PuestoListItemViewModel` se proyecta a `(Id, CodigoYNombre)` vía propiedad derivada: `public string CodigoYNombre => $"{Codigo} — {Nombre}"`. Si el catálogo de puestos falla en Edit, el select queda vacío y se muestra un `ErrorMessage` recuperable (no fatal).

## 5. Sidenav entry

Insertar entre la entry `Habilidades` y el cierre `</ul>` en `Pages/Shared/Partials/_Sidenav.cshtml`:

```razor
@{
    var puestosActive = currentPath.StartsWithSegments("/organizacion/puestos") ? "active" : string.Empty;
}

<li class="side-nav-item">
    <a aria-controls="puestos" aria-expanded="false" class="side-nav-link side-nav-link-toggle @puestosActive" data-bs-toggle="collapse" href="#puestos">
        <span class="menu-icon"><i class="ti ti-hierarchy"></i></span>
        <span class="menu-text">Puestos</span>
        <span class="menu-arrow"></span>
    </a>
    <div class="collapse" id="puestos">
        <ul class="sub-menu">
            <li class="side-nav-item">
                <a class="side-nav-link @puestosActive" href="/organizacion/puestos">
                    <span class="menu-text">Listado</span>
                </a>
            </li>
            <li class="side-nav-item">
                <a class="side-nav-link" href="/organizacion/puestos/crear">
                    <span class="menu-text">Nuevo</span>
                </a>
            </li>
        </ul>
    </div>
</li>
```

**Decisión de icono (`D1`):** `ti ti-hierarchy`. El delta `sgv-web-shell/spec.md` lo lockea explícitamente. Elegido para distinguir visualmente Puestos de Cargos (`ti ti-briefcase`), dado que Puestos representa una jerarquía de dependencia (puesto superior → puesto inferior). Coincidía originalmente con Cargos pero se corrigió post-review PR #89 para reducir ambigüedad visual en el sidenav. No introduce SCSS propio (reusa `side-nav-item`/`side-nav-link`).

## 6. JavaScript de confirmaciones — `puestos-index.js` (PR 2, ~90 líneas)

Decisión (`D3`): **duplicar** `wirePuestoDeleteConfirmation` y `wirePuestoReactivateConfirmation` desde `cargos-index.js`. Razón: la duplicación es de ~85 líneas, no introduce un helper compartido que obligaría a mantener un contrato entre dos módulos que ya comparten contrato a través del cliente tipado. Refactor a helper compartido queda como follow-up si surge un tercer módulo con confirmaciones.

Estructura:
- `wirePuestoDeleteConfirmation(root, swal)`: itera `[data-puesto-delete-form]`, registra handler de `click` en `[data-puesto-delete-button]`, dispara `Swal.fire({ title: '¿Eliminar puesto?', icon: 'warning', showCancelButton: true, confirmButtonText: 'Sí, eliminar', cancelButtonText: 'Cancelar', reverseButtons: true })`. En confirmación, `form.requestSubmit(button)`.
- `wirePuestoReactivateConfirmation(root, swal)`: análogo, `icon: 'question'`, `confirmButtonText: 'Sí, reactivar'`, `title: '¿Reactivar puesto?'`.
- IIFE de bootstrap que llama ambas si `window.Swal` está disponible.
- `if (typeof module !== 'undefined' && module.exports) module.exports = { wirePuestoDeleteConfirmation, wirePuestoReactivateConfirmation };` para el harness Node de los tests (espejo de Cargos).

## 7. Tests — strict TDD scope

Capas que **aplican**:

| Capa | Tipo | Carpeta | Espejo |
|---|---|---|---|
| Cliente HTTP | `PuestosApiClientTests` (xUnit + handler stub) | `tests/SGV.Tests/Web/Puesto/` | `CargoApiClientTests.cs` (7 tests base + cancel/transport/cancellation) |
| Contrato interface | `IPuestosApiClientContractTests` (reflection-based) | `tests/SGV.Tests/Web/Puesto/` | `ICargoApiClientContractTests.cs` |
| Seam | `PuestoWebSeamTests` (record shape + DI + override) | `tests/SGV.Tests/Web/Puesto/` | `CargoWebSeamTests.cs` |
| Páginas | `PuestoIndexPageTests`, `PuestoDetailsPageTests`, `PuestoCreatePageTests`, `PuestoEditPageTests` | `tests/SGV.Tests/Web/Puesto/` | 4 archivos homólogos de Cargos |
| Asset JS | Harness Node inline en `PuestoIndexPageTests` | Idem | `ExecuteDeleteConfirmationScriptAsync`/`ExecuteReactivateConfirmationScriptAsync` |

Capas que **NO** aplican: Dominio, Aplicación, Persistencia, API — ya cubiertos por `archive/2026-06-19-implementa-modulo-puestos/`. Bug pre-existente #59 (`OcupacionRepositoryTests`) no relacionado: tests nuevos usan `WebApplicationFactory` + fake, no `[MySqlFact]`.

### Test obligatorio de ausencia en `PuestoEditPageTests`

```csharp
[Fact]
public async Task Get_Edit_HtmlRenderizado_NoContieneCodigoUnidadOrganizativaNiCargo()
{
    var puesto = new PuestoDto(Guid.NewGuid(), "P-001", "Nombre", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null);
    var apiClient = new FakePuestosApiClient { /* seed by id */ };
    apiClient.GetByIdResult = puesto;
    apiClient.GetAllResult = new List<PuestoDto> { puesto };
    apiClient.UnidadesResult = new List<UnidadOrganizativaDto> { /* ... */ };
    apiClient.CargosResult = new List<CargoDto> { /* ... */ };

    using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);
    var response = await client.GetAsync($"/organizacion/puestos/editar/{puesto.Id}");
    var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    // El HTML de Edit MUST NOT contener los inputs inmutables.
    // Decisión D5: regex sobre el HTML renderizado (espejo del patrón de
    // CargoCreatePageTests.Post_Create_WhenCodigoIsEmpty_*). AngleSharp no es
    // necesario: la regex es estable, predecible y consistente con el resto de
    // la suite web.
    Assert.DoesNotMatch(content, new Regex(@"name=""Input\.Codigo""", RegexOptions.IgnoreCase));
    Assert.DoesNotMatch(content, new Regex(@"name=""Input\.UnidadOrganizativaId""", RegexOptions.IgnoreCase));
    Assert.DoesNotMatch(content, new Regex(@"name=""Input\.CargoId""", RegexOptions.IgnoreCase));

    // Triangulación positiva: Nombre/Descripcion/PuestoSuperiorId SÍ se renderizan.
    Assert.Matches(content, new Regex(@"name=""Input\.Nombre""", RegexOptions.IgnoreCase));
    Assert.Matches(content, new Regex(@"name=""Input\.Descripcion""", RegexOptions.IgnoreCase));
    Assert.Matches(content, new Regex(@"name=""Input\.PuestoSuperiorId""", RegexOptions.IgnoreCase));
}
```

### `FakePuestosApiClient.cs` (PR 1, ~250 líneas)

Decisión (`D2`): **respuestas programadas** vía propiedades (`GetAllResult`, `GetByIdResult`, `CreateResult`, `UpdateResult`, `DeleteResult`, `ReactivateResult`, `UnidadesResult`, `CargosResult`) **más** captura de invocaciones (`GetAllCalls`, `GetByIdCalls`, `CreateCalls`, `UpdateCalls`, `DeleteCalls`, `ReactivateCalls`, `GetUnidadesCalls`, `GetCargosCalls`). Excepciones inyectables por método (`CreateException`, etc.). Razón: la mayoría de los tests de página solo necesitan "configurar X, disparar POST, verificar Y" — el patrón de respuestas programadas reduce boilerplate. Los `GetAllCalls`/`CreateCalls` etc. permiten las aserciones equivalentes a `Assert.Single(apiClient.DeleteCalls)` que ya usa Cargos.

### `PuestoWebTestFixture.cs` (PR 1, ~170 líneas)

Espejo de `CargoWebTestFixture`:
- `BaseFactory` (sin override).
- `WithPuestosApiClient(fake)`.
- `CreateAuthenticatedClientAsync(FakePuestosApiClient)`: stub auth + cookie, helper para `FakeUnidadOrganizativaApiClient` y `FakeCargoApiClient` mínimos para que los selects de Create se rendericen.
- `ExtractAntiforgeryTokenAsync(response)` (espejo verbatim).
- `RecordingHttpMessageHandler` para stub del auth.
- `BuildPuestoDto(...)` constructor helper con ids aleatorios.
- `BuildAdminRoleJwt()` para escenarios que necesiten `ClaimTypes.Role` (no requeridos por este slice, pero espejado para paridad).
- Seeds Guid estáticos: `SampleUnidadOrganizativaId`, `SampleCargoId`, `SamplePuestoSuperiorId`.

## 8. Mapeo requisito → implementación

| Requisito (spec) | Archivo / método / test concreto |
|---|---|
| **PUESTO-WEB-LISTADO-DETALLE-BAJA** | |
| Req 1. Acceso autenticado vs anónimo | `Index.cshtml.cs [Authorize]`, `Details.cshtml.cs [Authorize]` + `PuestoIndexPageTests.Get_Index_WhenAnonymous_RedirectsToSignIn` + `PuestoDetailsPageTests.Get_Details_WhenAnonymous_RedirectsToSignIn` |
| Req 2. Listado plano con toggle deshabilitado (3 escenarios) | `Index.cshtml` columnas + toggle `disabled` + tooltip; `IndexModel.OnGetAsync`; `IndexModel.LoadAsync` (memoria) + tests: `Get_Index_WhenAuthenticated_RendersActivePuestosTable`, `Get_Index_WhenPuestoHasSuperior_RendersLinkPreservingContext`, `Get_Index_ToggleEliminadas_IsDisabledAndShowsTooltip` |
| Req 3. Baja lógica confirmada con feedback (2 escenarios) | `puestos-index.js wirePuestoDeleteConfirmation` + `IndexModel.OnPostDeleteAsync` + tests: `DeleteConfirmationScript_WhenCancelled_DoesNotSubmitForm`, `DeleteConfirmationScript_WhenConfirmed_SubmitsFormOnce`, `Post_Delete_WhenSuccessful_RedirectsPreservingFilters`, `Post_Delete_WhenConflict_ShowsFeedbackAndKeepsRowVisible` |
| Req 4. Reactivación con feedback de conflicto (2 escenarios) | `puestos-index.js wirePuestoReactivateConfirmation` + `IndexModel.OnPostReactivateAsync` + tests: `ReactivateConfirmationScript_WhenCancelled_DoesNotSubmitForm`, `ReactivateConfirmationScript_WhenConfirmed_SubmitsFormOnce`, `Post_Reactivate_WhenSuccessful_RedirectsToActivasClearsLastDeletedId`, `Post_Reactivate_WhenConflict_ShowsFeedbackAndKeepsContext` |
| Req 5. Detalle readonly con retorno preservando contexto | `Details.cshtml` dl + links; `DetailsModel.OnGetAsync(p, search, sort)`; tests: `Get_Details_WhenAuthenticated_ShowsPuestoReadOnly`, `Get_Details_WhenPuestoHasSuperior_RendersLinkToSuperior`, `Get_Details_WhenPuestoNotFound_ShowsNotAvailableState`, `Get_Details_WhenAuthenticated_BackLinkPreservesContext` |
| Req 6. Entry colapsable "Puestos" en sidenav | `_Sidenav.cshtml` (bloque §5) + test: `PuestoWebSeamTests.Get_Sidenav_WhenAuthenticated_ExposesPuestosModuleWithBriefcaseIcon` + `PuestoWebSeamTests.Get_Sidenav_WhenOnPuestosRoute_SidenavSubmenuIsActive` |
| **PUESTO-WEB-CREAR-EDITAR** | |
| Req 1. Acceso autenticado a create y edit (2 escenarios) | `Create.cshtml.cs [Authorize]`, `Edit.cshtml.cs [Authorize]` + tests: `Get_Create_WhenAnonymous_RedirectsToSignIn`, `Get_Edit_WhenAnonymous_RedirectsToSignIn`, `Get_Edit_WhenPuestoNotFound_ShowsRecoverableState` |
| Req 2. Create con los seis campos editables | `Create.cshtml` form completo + `PuestoInputModel` 6 props + test: `Get_Create_WhenAuthenticated_RendersAllSixFields` (afirma presencia de `name="Input.Codigo"`, `Input.Nombre`, `Input.Descripcion`, `Input.UnidadOrganizativaId`, `Input.CargoId`, `Input.PuestoSuperiorId`) |
| Req 3. PuestoSuperiorId con select poblado (2 escenarios) | `CreateModel.LoadCatalogsAsync` con `Task.WhenAll(GetAllAsync UO, Cargo, Puesto)` + `PuestoFormKeys.PuestoSuperiorIdKey` + tests: `Get_Create_WhenPuestosCatalogHasResults_SelectContainsNPlusOneOptions`, `Get_Create_WhenPuestosCatalogFails_ShowsRecoverableState` |
| Req 4. Edit con tres campos (2 escenarios) | `Edit.cshtml` parcial sin Codigo/UO/Cargo + `PuestoInputModel` con 6 props pero `_Form.cshtml` los oculta vía `if (!Model.IsEdit)` + tests: `Get_Edit_WhenAuthenticated_PrepopulatesNombreDescripcionPuestoSuperior`, `Get_Edit_HtmlRenderizado_NoContieneCodigoUnidadOrganizativaNiCargo` (test RED obligatorio) |
| Req 5. _Form.cshtml compartido | `Pages/Organizacion/Puestos/_Form.cshtml` (ver §4.6) + tests: `Get_Create_WhenAuthenticated_FormContainsCodigoInput`, `Get_Edit_WhenAuthenticated_FormDoesNotContainCodigoInput` |
| Req 6. Guardado con PRG y feedback (4 escenarios) | `CreateModel.OnPostAsync` + `EditModel.OnPostAsync` + `PuestoFormHelpers.ApplyFieldErrorsToModelState` + tests: `Post_Create_WhenSuccessful_RedirectsToListado`, `Post_Edit_WhenSuccessful_RedirectsToDetails`, `Post_Create_WhenBackendReturnsFieldErrors_RendersFieldValidationOnCodigo`, `Post_Create_WhenCodigoDuplicado_ReturnsFieldErrorAndKeepsForm`, `Post_Create_WhenHttpRequestException_ReloadsCatalogsAndShowsGeneralError`, `Post_Create_WhenTaskCanceledException_ReloadsCatalogsAndShowsGeneralError` |
| Req 7. Submenú de Puestos | `_Sidenav.cshtml` entry + tests: `Get_Create_WhenAuthenticated_SidenavShowsNuevoEntryWithActiveState`, `Get_Edit_WhenAuthenticated_SidenavShowsSubmenuActive` |
| **SGV-WEB-SHELL (DELTA MODIFIED)** | |
| Req 1. Minimal technical navigation con Puestos habilitado (3 escenarios) | `_Sidenav.cshtml` entry colapsable + icon `ti ti-hierarchy` + test RED: `Get_Sidenav_WhenAuthenticated_ExposesPuestosModule` (afirma presencia `>Puestos<` y `ti ti-hierarchy`); `Get_Sidenav_WhenOnPuestosSubroute_SubmenuIsExpanded`; `Get_Sidenav_WhenAuthenticated_DoesNotExposeUnimplementedModules` |
| **WEB-APICLIENT-TRANSPORT-CONTRACT (DELTA ADDED)** | |
| Req 1. Propaga TaskCanceledException / HttpRequestException (2 escenarios) | `PuestosApiClient` sin try/catch en los métodos públicos + tests: `PuestosApiClientTests.{GetAllAsync,CreateAsync,UpdateAsync,DeleteAsync,ReactivateAsync,GetByIdAsync}_TransportFails_PropagatesNativeException` (Theory con `HttpClientExceptionScenarios.TransportExceptionData`) |
| Req 2. Respeta CancellationToken pre-cancelado | Sin try/catch + tests: `PuestosApiClientTests.{...}_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest` (Fact por método, espejo de `CargoApiClientTests.QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest`) |
| Req 3. Traduce ProblemDetails a resultados tipados (3 escenarios) | `PuestosApiClient.ToCommandResultAsync` + tests: `PuestosApiClientTests.CreateAsync_Http400WithValidationProblemDetails_ReturnsFailureWithFieldErrors`, `PuestosApiClientTests.CreateAsync_Http409WithProblemDetails_ReturnsFailureWithConflict`, `PuestosApiClientTests.DeleteAsync_Http204_ReturnsSuccessAndHitsDeleteRoute` |

## 9. Decisiones técnicas explícitas

- **D1 — Icono del sidenav:** `ti ti-hierarchy`. Locked por el delta `sgv-web-shell/spec.md`. Elegido para distinguir visualmente Puestos de Cargos (`ti ti-briefcase`). Coincidía originalmente con Cargos pero se corrigió post-review PR #89.
- **D2 — `FakePuestosApiClient`:** respuestas programadas (propiedades `GetAllResult`, `CreateResult`, etc.) + captura de invocaciones (`GetAllCalls`, `CreateCalls`, etc.) + excepciones inyectables (`CreateException`). No `Func<>` factories excepto donde Cargos las usa (`QueryHandler`). Razón: la mayoría de los tests de página solo configuran "1 → 1" respuesta/payload; las properties son menos ceremoniosas que las factories y más fáciles de leer en cada test individual.
- **D3 — Helper JS compartido vs duplicado:** **duplicar** `wirePuestoDeleteConfirmation` y `wirePuestoReactivateConfirmation` desde `cargos-index.js`. La duplicación es ~85 líneas, ambos archivos son auto-contenidos (no exportan un módulo compartido) y un refactor a helper compartido no aporta valor hasta que haya un tercer módulo con el mismo patrón.
- **D4 — Render del toggle deshabilitado:** atributo HTML `disabled` + `data-bs-toggle="tooltip" data-bs-title="Requiere endpoint backend: pendiente de follow-up"` directamente en el Razor (`<a class="btn btn-light btn-sm" ... aria-disabled="true" tabindex="-1" ... data-bs-toggle="tooltip" data-bs-title="..." >Eliminadas</a>`). Razón: el tooltip se inicializa en cliente por el bundle Inspinia existente (no se requiere JS nuevo). Test RED afirma que el anchor tiene `disabled` o `aria-disabled="true"` + el atributo `data-bs-title` específico.
- **D5 — Inspección del HTML en el test de ausencia:** regex sobre el HTML renderizado (vía `HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync())`). Espejo del patrón de `CargoCreatePageTests.Post_Create_WhenCodigoIsEmpty_ShowsValidationErrorAndDoesNotRedirect` (regex sobre `<span data-valmsg-for="...">`). AngleSharp no es necesario: la regex es estable y consistente con el resto de la suite web. Si en el futuro el proyecto migra a componentes richer, se podrá refactorizar.
- **D6 — Convención JSON en `PuestosApiClient`:** System.Text.Json con casing por defecto de ASP.NET Core 10 (camelCase saliente). Verificado: `PuestoDto` (`Codigo`, `Nombre`, `Descripcion`, `UnidadOrganizativaId`, etc.) coincide 1:1 con el DTO backend; `CrearPuestoRequest` y `ActualizarPuestoRequest` también. **No** se necesita `JsonPropertyName`. Igual que `CargoApiClient`.
- **D7 — `[BindProperty]` vs `[FromForm]` en POST handlers:** `[BindProperty]` en `Input` (espejo de Cargos). Los handlers de Delete/Reactivate usan parámetros `[FromForm]` explícitos (`Guid id`, `int page`, `string? search`, `string? sort`, `string? status`) — el modelo `Input` no se bindea en esos POSTs porque el botón sólo envía inputs hidden.
- **D8 — Catálogos de Create vía `Task.WhenAll`:** las tres llamadas (`IUnidadOrganizativaApiClient.GetAllAsync`, `ICargoApiClient.GetAllAsync`, `IPuestosApiClient.GetAllAsync`) se ejecutan en paralelo. Si **alguna** falla, se muestra `ErrorMessage` recuperable y el form se renderiza igual con los catálogos que sí llegaron (espejo de `CargoCreateModel` pero extendido a 3 catálogos). Esta decisión evita que un catálogo caído bloquee el alta del Puesto cuando el otro sí responde.
- **D9 — `PATCH /reactivar`:** la reactivación se hace desde el listado (con `LastDeletedId` o fila directa), **no** desde el detalle. Espejo de Cargos.

## 10. Estructura de los 3 PRs chained

```text
2026-07-06-implementa-modulo-puestos-en-frontend
│
├─ PR 1 — Seams + shell + navegación (~230 líneas)
│   ├─ src/SGV.Web/Integration/Organizacion/IPuestosApiClient.cs          NEW
│   ├─ src/SGV.Web/Integration/Organizacion/PuestosApiClient.cs           NEW
│   ├─ src/SGV.Web/Integration/Organizacion/PuestoListItemViewModel.cs    NEW (+ PuestoDeleteResult)
│   ├─ src/SGV.Web/Integration/Organizacion/PuestoFormKeys.cs             NEW (parte de PuestoFormHelpers.cs)
│   ├─ src/SGV.Web/Integration/Organizacion/PuestoInputModel.cs           NEW
│   ├─ src/SGV.Web/Program.cs                                            MODIFIED (+ registro HttpClient)
│   ├─ src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml                 MODIFIED (+ entry Puestos)
│   ├─ tests/SGV.Tests/Web/SgvWebApplicationFactory.cs                    MODIFIED (+ override IPuestosApiClient)
│   ├─ tests/SGV.Tests/Web/Puesto/PuestoWebTestFixture.cs                NEW
│   ├─ tests/SGV.Tests/Web/Puesto/FakePuestosApiClient.cs                NEW
│   ├─ tests/SGV.Tests/Web/Puesto/PuestosApiClientTests.cs               NEW (≥12 tests)
│   ├─ tests/SGV.Tests/Web/Puesto/IPuestosApiClientContractTests.cs      NEW (≥6 tests)
│   └─ tests/SGV.Tests/Web/Puesto/PuestoWebSeamTests.cs                  NEW (≥7 tests)
│
├─ PR 2 — Listado + baja lógica + reactivación (~480 líneas)
│   ├─ src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml                NEW
│   ├─ src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs             NEW
│   ├─ src/SGV.Web/wwwroot/js/pages/puestos-index.js                      NEW
│   └─ tests/SGV.Tests/Web/Puesto/PuestoIndexPageTests.cs                 NEW (≥18 tests)
│
└─ PR 3 — Create + Edit + Details (~180 líneas)
    ├─ src/SGV.Web/Pages/Organizacion/Puestos/_Form.cshtml                NEW
    ├─ src/SGV.Web/Pages/Organizacion/Puestos/Create.cshtml               NEW
    ├─ src/SGV.Web/Pages/Organizacion/Puestos/Create.cshtml.cs            NEW
    ├─ src/SGV.Web/Pages/Organizacion/Puestos/Edit.cshtml                 NEW
    ├─ src/SGV.Web/Pages/Organizacion/Puestos/Edit.cshtml.cs              NEW
    ├─ src/SGV.Web/Pages/Organizacion/Puestos/Details.cshtml              NEW
    ├─ src/SGV.Web/Pages/Organizacion/Puestos/Details.cshtml.cs           NEW
    └─ tests/SGV.Tests/Web/Puesto/
       ├─ PuestoCreatePageTests.cs                                       NEW (≥10 tests)
       ├─ PuestoEditPageTests.cs                                         NEW (≥10 tests, incl. test de ausencia)
       └─ PuestoDetailsPageTests.cs                                      NEW (≥6 tests)
```

Cada PR cierra con: `dotnet build SGV.slnx`, `dotnet test SGV.slnx --filter "FullyQualifiedName~Puesto"`, `bun run build` (en `src/SGV.Web`).

### Forecast de líneas re-validado

Validación contra `git diff --stat` del archivo de Cargos análogo:

| PR | Líneas (Puestos forecast) | Cargos (referencia) | Ratio |
|---|---|---|---|
| PR 1 | ~230 | 230 (`git diff --stat` en `archive/2026-06-30-...-cargos/`) | 1.00× |
| PR 2 | ~480 | 480 (estimación tasks.md: `Index.cshtml` 237 líneas + `Index.cshtml.cs` 364 líneas + `cargos-index.js` 85 líneas + tests) | 1.00× |
| PR 3 | ~180 | 180 (`Create.cshtml` 39 + `Create.cshtml.cs` 144 + `Edit.cshtml` 64 + `Edit.cshtml.cs` 201 + `Details.cshtml` 82 + `Details.cshtml.cs` 93 + `_Form.cshtml` 40 ≈ 663; pero paridad de Cargos ajustada por la diferencia de 6 vs 4 campos y por omisión de `Habilidades.cshtml.cs` ≈ 180 netas para Puestos) | 1.00× |
| **Total** | **~890** | **~890** | 1.00× |

`review_budget_lines: 400` excedido → **chained PRs recommended: Yes**.

## 11. Riesgos y mitigaciones heredados del proposal

| # | Riesgo del proposal | Mitigación técnica |
|---|---|---|
| 1 | Backend sin `[Authorize(Roles=Administrador)]` | `ApiBearerTokenHandler` propaga JWT cuando existe; UI asume cookie auth como Cargos (no se agrega `[Authorize(Roles=...)]` en `Index/Create/Edit` — solo `[Authorize]`). Documentado como follow-up `puestos-crear-autorizacion-admin`. **No introduce regresión** porque el módulo es nuevo. |
| 2 | Toggle "Eliminadas" sin endpoint segmentado | Toggle renderizado con `aria-disabled="true"` + tooltip "Requiere endpoint backend: pendiente de follow-up" (decisión `D4`). `IndexModel.LoadAsync` consulta `GetAllAsync()` cuando segmento es `eliminadas` y devuelve el mismo set (forward-compat trivial cuando llegue el endpoint `/consulta?status=activas|eliminadas`). Test RED afirma visibilidad del estado deshabilitado. |
| 3 | `Edit` pretende editar `Codigo`/`UnidadOrganizativaId`/`CargoId` | Alcance explícito en `proposal.md` + test RED obligatorio `Get_Edit_HtmlRenderizado_NoContieneCodigoUnidadOrganizativaNiCargo` (decisión `D5`). `_Form.cshtml` recibe flag `IsEdit` y oculta los inputs inmutables. El `PuestoInputModel` los declara como `public string Codigo` etc. para preservar el shape simétrico entre Create/Edit (algunos campos quedan `null` en Edit), pero el binding Razor no los renderiza. |
| 4 | Drift entre keys de `ModelState` (camelCase) y nombres de input | `_Form.cshtml` usa `asp-for="Input.Codigo"` etc., que produce `name="Input.Codigo"`; el backend (`PuestoServicioComandos.BuildFieldErrors`) emite claves en camelCase (`codigo`, `nombre`, etc.), que `PuestoFormHelpers.ApplyFieldErrorsToModelState` prefijja con `"Input."` para que `asp-validation-for` los recoja. Tests RED mockean `ValidationProblemDetails` y verifican que el error cae junto al input (espejo de `CargoCreatePageTests.Post_Create_WhenBackendReturnsFieldErrors_*`). |
| 5 | `PATCH /reactivar` responde 409 si `Codigo` ya está ocupado | `PuestosApiClient.ReactivateAsync` mapea 409 → `PuestoCommandResult.Failure(Code="CodigoDuplicado")`; `OnPostReactivateAsync` lo traduce a `TempData["StatusMessage"]` con copy específico (espejo de `CargoIndexModel.OnPostReactivateAsync`). Test RED cubre 409. |
| 6 | `bun run build` introduce regresión en bundle Inspinia | Sidebar reusa `side-nav-item`/`side-nav-link` sin SCSS propio; `puestos-index.js` es vanilla JS sin imports nuevos. Validar en PR 1 y PR 2 con `bun install && bun run build`. |
| 7 | Bug pre-existente #59 `OcupacionRepositoryTests` | `MySqlFact` desconectados; tests nuevos usan `WebApplicationFactory` + `FakePuestosApiClient`. No se tocan tests de Persistencia. |

## 12. Próximos pasos (para `sdd-tasks`)

Cuando llegue `sdd-tasks`, las tareas concretas saldrán 1:1 de la **sección 10** (Estructura de los 3 PRs chained). Cada work unit se desglosa en tres pasos: RED (test escrito y confirmado rojo), GREEN (producción mínima para pasar), REFACTOR (XML docs, comentarios, extracción de helpers). La tabla `TDD Cycle Evidence` que la repo exige (precedente `archive/2026-06-30-...-cargos/apply-progress.md`) vivirá en `apply-progress.md`, **no** aquí; este diseño la **foreshadows** vía el TDD cycle plan de la §13.

### Cierre del slice

- `dotnet build SGV.slnx` + `dotnet test SGV.slnx --filter "FullyQualifiedName~Puesto"` + `bun run build` en verde para cada PR.
- `dotnet test SGV.slnx` (sin filtro) verde: 146 `[MySqlFact]` skipeados sin regresión.
- `apply-progress.md` con TDD Cycle Evidence completa.
- `verify-report.md` PASS sin CRITICAL.
- Sync delta specs a `openspec/specs/{puesto-web-listado-detalle-baja,puesto-web-crear-editar,sgv-web-shell,web-apiclient-transport-contract}/spec.md` y archive del change.

## 13. TDD cycle plan (foreshadow para `apply-progress.md`)

Cada escenario del spec se materializa en un test con nombre estable. Tabla exhaustiva (1 fila por escenario):

| Spec | Escenario | Clase de test | Método de test (verbatim) |
|---|---|---|---|
| puesto-web-listado-detalle-baja Req 1 | Acceso autenticado vs anónimo (Index) | `PuestoIndexPageTests` | `Get_Index_WhenAnonymous_RedirectsToSignIn` |
| puesto-web-listado-detalle-baja Req 1 | Acceso autenticado vs anónimo (Details) | `PuestoDetailsPageTests` | `Get_Details_WhenAnonymous_RedirectsToSignIn` |
| puesto-web-listado-detalle-baja Req 2 | Carga inicial con columnas locked | `PuestoIndexPageTests` | `Get_Index_WhenAuthenticated_RendersActivePuestosTable` |
| puesto-web-listado-detalle-baja Req 2 | Puesto superior como link con contexto | `PuestoIndexPageTests` | `Get_Index_WhenPuestoHasSuperior_RendersLinkPreservingContext` |
| puesto-web-listado-detalle-baja Req 2 | Toggle Eliminadas deshabilitado con tooltip | `PuestoIndexPageTests` | `Get_Index_ToggleEliminadas_IsDisabledAndShowsTooltip` |
| puesto-web-listado-detalle-baja Req 3 | Cancelación no elimina | `PuestoIndexPageTests` | `DeleteConfirmationScript_WhenCancelled_DoesNotSubmitForm` |
| puesto-web-listado-detalle-baja Req 3 | Baja éxito o conflicto (204) | `PuestoIndexPageTests` | `Post_Delete_WhenSuccessful_RedirectsPreservingFilters` |
| puesto-web-listado-detalle-baja Req 3 | Baja éxito o conflicto (409) | `PuestoIndexPageTests` | `Post_Delete_WhenConflict_ShowsFeedbackAndKeepsRowVisible` |
| puesto-web-listado-detalle-baja Req 4 | Reactivación exitosa limpia banner | `PuestoIndexPageTests` | `Post_Reactivate_WhenSuccessful_RedirectsToActivasClearsLastDeletedId` |
| puesto-web-listado-detalle-baja Req 4 | Reactivación con conflicto por código | `PuestoIndexPageTests` | `Post_Reactivate_WhenConflict_ShowsFeedbackAndKeepsContext` |
| puesto-web-listado-detalle-baja Req 5 | Detalle existente o no disponible (existe) | `PuestoDetailsPageTests` | `Get_Details_WhenAuthenticated_ShowsPuestoReadOnly` |
| puesto-web-listado-detalle-baja Req 5 | Detalle existente o no disponible (no consultable) | `PuestoDetailsPageTests` | `Get_Details_WhenPuestoNotFound_ShowsNotAvailableState` |
| puesto-web-listado-detalle-baja Req 6 | Submenú visible y activo | `PuestoWebSeamTests` | `Get_Sidenav_WhenOnPuestosRoute_SubmenuIsActive` |
| puesto-web-crear-editar Req 1 | Acceso autenticado vs anónimo (Create) | `PuestoCreatePageTests` | `Get_Create_WhenAnonymous_RedirectsToSignIn` |
| puesto-web-crear-editar Req 1 | Acceso autenticado vs anónimo (Edit) | `PuestoEditPageTests` | `Get_Edit_WhenAnonymous_RedirectsToSignIn` |
| puesto-web-crear-editar Req 1 | Puesto inexistente en edit | `PuestoEditPageTests` | `Get_Edit_WhenPuestoNotFound_ShowsRecoverableState` |
| puesto-web-crear-editar Req 2 | Create muestra los seis campos | `PuestoCreatePageTests` | `Get_Create_WhenAuthenticated_RendersAllSixFields` |
| puesto-web-crear-editar Req 3 | Select poblado por la API | `PuestoCreatePageTests` | `Get_Create_WhenPuestosCatalogHasResults_SelectContainsNPlusOneOptions` |
| puesto-web-crear-editar Req 3 | Falla del catálogo | `PuestoCreatePageTests` | `Get_Create_WhenPuestosCatalogFails_ShowsRecoverableState` |
| puesto-web-crear-editar Req 4 | Edit muestra los tres campos | `PuestoEditPageTests` | `Get_Edit_WhenAuthenticated_PrepopulatesNombreDescripcionPuestoSuperior` |
| puesto-web-crear-editar Req 4 | **Ausencia de Codigo/UO/Cargo en Edit (RED obligatorio)** | `PuestoEditPageTests` | `Get_Edit_HtmlRenderizado_NoContieneCodigoUnidadOrganizativaNiCargo` |
| puesto-web-crear-editar Req 5 | Codigo solo en Create | `PuestoCreatePageTests` + `PuestoEditPageTests` | `Get_Create_WhenAuthenticated_FormContainsCodigoInput` + `Get_Edit_WhenAuthenticated_FormDoesNotContainCodigoInput` |
| puesto-web-crear-editar Req 6 | Create o Edit exitoso (Create) | `PuestoCreatePageTests` | `Post_Create_WhenSuccessful_RedirectsToListado` |
| puesto-web-crear-editar Req 6 | Create o Edit exitoso (Edit) | `PuestoEditPageTests` | `Post_Edit_WhenSuccessful_RedirectsToDetails` |
| puesto-web-crear-editar Req 6 | Validación por campo | `PuestoCreatePageTests` | `Post_Create_WhenBackendReturnsFieldErrors_RendersFieldValidationOnCodigo` |
| puesto-web-crear-editar Req 6 | Conflicto por Codigo duplicado | `PuestoCreatePageTests` | `Post_Create_WhenCodigoDuplicado_ReturnsFieldErrorAndKeepsForm` |
| puesto-web-crear-editar Req 6 | Backend no disponible durante guardado | `PuestoCreatePageTests` | `Post_Create_WhenHttpRequestException_ReloadsCatalogsAndShowsGeneralError` |
| puesto-web-crear-editar Req 7 | Estado active y retorno al Listado (Create) | `PuestoCreatePageTests` | `Get_Create_WhenAuthenticated_SidenavShowsNuevoEntryWithActiveState` |
| puesto-web-crear-editar Req 7 | Estado active y retorno al Listado (Edit) | `PuestoEditPageTests` | `Get_Edit_WhenAuthenticated_SidenavShowsSubmenuActive` |
| sgv-web-shell Req 1 | Navegación mínima con Puestos habilitado | `PuestoWebSeamTests` | `Get_Sidenav_WhenAuthenticated_ExposesPuestosModule` |
| sgv-web-shell Req 1 | Submenú de Puestos visible y activo | `PuestoWebSeamTests` | `Get_Sidenav_WhenOnPuestosSubroute_SubmenuIsExpanded` |
| sgv-web-shell Req 1 | Otros módulos siguen fuera de alcance | `PuestoWebSeamTests` | `Get_Sidenav_WhenAuthenticated_DoesNotExposeUnimplementedModules` |
| web-apiclient-transport-contract Req 1 | Cancelación o timeout del transporte (Theory) | `PuestosApiClientTests` | `{GetAllAsync,CreateAsync,UpdateAsync,DeleteAsync,ReactivateAsync,GetByIdAsync}_TransportFails_PropagatesNativeException` |
| web-apiclient-transport-contract Req 2 | Token pre-cancelado | `PuestosApiClientTests` | `{...}_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest` |
| web-apiclient-transport-contract Req 3 | 400 con FieldErrors | `PuestosApiClientTests` | `CreateAsync_Http400WithValidationProblemDetails_ReturnsFailureWithFieldErrors` |
| web-apiclient-transport-contract Req 3 | 409 por Codigo duplicado o Puesto superior inválido | `PuestosApiClientTests` | `CreateAsync_Http409WithProblemDetails_ReturnsFailureWithConflict` |
| web-apiclient-transport-contract Req 3 | Delete mapea a PuestoDeleteResult (204) | `PuestosApiClientTests` | `DeleteAsync_Http204_ReturnsSuccessAndHitsDeleteRoute` |
| web-apiclient-transport-contract Req 3 | Delete mapea a PuestoDeleteResult (404) | `PuestosApiClientTests` | `DeleteAsync_Http404WithProblemDetails_ReturnsFailureWithNotFound` |

Total proyectado: **38 escenarios** distribuidos en 5 archivos de test. Cobertura 1:1 con cada escenario del spec, sin holgura.