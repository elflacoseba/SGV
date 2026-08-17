using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Organizacion.UnidadesOrganizativas;

/// <summary>
/// PageModel para la página Details de unidades organizativas.
/// <para>
/// Issue #125 / Slice 3: switch exhaustivo sobre
/// <see cref="ErrorCategoria"/> en OnPostReactivateAsync.
/// <c>Unauthorized</c> redirige vía <see cref="IAuthSessionRedirector"/>.
/// </para>
/// <para>
/// Issue #281: <see cref="Vigencia"/> se expone como
/// <see cref="VigenciaViewModel"/> (texto + clase CSS opcional) para
/// colorear la badge fuera de rango; <see cref="ReturnVigenteEn"/>
/// preserva el filtro al volver al listado.
/// </para>
/// </summary>
[Authorize]
public sealed class DetailsModel(
    IUnidadOrganizativaApiClient unidadOrganizativaApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<DetailsModel> logger) : PageModel
{
    public UnidadOrganizativaDto? Unidad { get; private set; }

    public VigenciaViewModel? Vigencia { get; private set; }

    public bool IsNotFound { get; private set; }

    public bool IsRecoverable { get; private set; }

    public Guid CurrentId { get; private set; }

    public bool HasParent => Unidad?.UnidadPadreId is not null;

    public string? StatusMessage => TempData[nameof(StatusMessage)] as string;

    public string StatusKind => TempData[nameof(StatusKind)] as string ?? "success";

    public string ReturnPage { get; private set; } = string.Empty;

    public string ReturnSearch { get; private set; } = string.Empty;

    public string ReturnSort { get; private set; } = string.Empty;

    public string ReturnView { get; private set; } = string.Empty;

    public string ReturnStatus { get; private set; } = string.Empty;

    public string ReturnVigenteEn { get; private set; } = string.Empty;

    public string ReturnToListUrl => UnidadOrganizativaFormHelpers.BuildReturnToListUrl(Url, ReturnPage, ReturnSearch, ReturnSort, ReturnView, ReturnStatus, ReturnVigenteEn);

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        string? p = null,
        string? page = null,
        string? search = null,
        string? sort = null,
        string? view = null,
        string? vigenteEn = null,
        string? returnPage = null,
        string? returnSearch = null,
        string? returnSort = null,
        string? returnView = null,
        string? returnStatus = null,
        string? returnVigenteEn = null,
        CancellationToken cancellationToken = default)
    {
        ReturnPage = returnPage ?? p ?? page ?? string.Empty;
        ReturnSearch = returnSearch ?? search ?? string.Empty;
        ReturnSort = returnSort ?? sort ?? string.Empty;
        ReturnView = returnView ?? view ?? string.Empty;
        ReturnStatus = returnStatus ?? string.Empty;
        ReturnVigenteEn = returnVigenteEn ?? vigenteEn ?? string.Empty;
        CurrentId = id;

        try
        {
            Unidad = await unidadOrganizativaApiClient.GetByIdAsync(id, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load unidad organizativa {Id}.", id);
            Unidad = null;
        }

        if (Unidad is not null)
        {
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            Vigencia = VigenciaViewModel.Desde(Unidad.VigenteDesde, Unidad.VigenteHasta, hoy);
        }

        if (Unidad is null)
        {
            IsNotFound = true;
            IsRecoverable = true;
            return Page();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReturnPage = Request.Form[nameof(ReturnPage)].FirstOrDefault() ?? string.Empty;
        ReturnSearch = Request.Form[nameof(ReturnSearch)].FirstOrDefault() ?? string.Empty;
        ReturnSort = Request.Form[nameof(ReturnSort)].FirstOrDefault() ?? string.Empty;
        ReturnView = Request.Form[nameof(ReturnView)].FirstOrDefault() ?? string.Empty;
        ReturnStatus = Request.Form[nameof(ReturnStatus)].FirstOrDefault() ?? string.Empty;
        ReturnVigenteEn = Request.Form[nameof(ReturnVigenteEn)].FirstOrDefault() ?? string.Empty;
        CurrentId = id;

        var result = await unidadOrganizativaApiClient.ReactivateAsync(id, cancellationToken);

        if (result.IsSuccess)
        {
            TempData["StatusMessage"] = "La unidad organizativa se reactivó correctamente.";
            TempData["StatusKind"] = "success";
            return RedirectToPage("/Organizacion/UnidadesOrganizativas/Details", new { id, returnPage = ReturnPage, returnSearch = ReturnSearch, returnSort = ReturnSort, returnView = ReturnView, returnStatus = ReturnStatus, returnVigenteEn = ReturnVigenteEn });
        }

        // Issue #125 / Slice 3: Unauthorized redirige vía IAuthSessionRedirector.
        if (result.Error?.Categoria == ErrorCategoria.Unauthorized)
        {
            var redirect = authRedirector.TryRedirectToLogin(Request.Path);
            if (redirect is not null)
            {
                return redirect;
            }
        }

        var categoria = result.Error?.Categoria ?? ErrorCategoria.Unexpected;
        var message = categoria switch
        {
            ErrorCategoria.Conflict => $"No se pudo reactivar la unidad organizativa. {result.Error.Message}",
            ErrorCategoria.NotFound => "La unidad organizativa ya no está disponible para reactivar.",
            ErrorCategoria.Transport => "No se pudo reactivar la unidad organizativa. Intentá nuevamente.",
            ErrorCategoria.Unexpected => "No se pudo reactivar la unidad organizativa. Intentá nuevamente.",
            _ => ErrorCategoryMapper.Map(categoria,
                notFoundMessage: "La unidad organizativa solicitada no está disponible.",
                conflictMessage: "Conflicto al procesar la operación.")
        };

        TempData["StatusMessage"] = message;
        TempData["StatusKind"] = "danger";

        IsNotFound = true;
        IsRecoverable = true;
        CurrentId = id;
        return Page();
    }
}
