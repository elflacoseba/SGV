using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.UnidadesOrganizativas;

[Authorize]
public sealed class DetailsModel(
    IUnidadOrganizativaApiClient unidadOrganizativaApiClient,
    ILogger<DetailsModel> logger) : PageModel
{
    public UnidadOrganizativaDto? Unidad { get; private set; }

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

    public string ReturnToListUrl => UnidadOrganizativaFormHelpers.BuildReturnToListUrl(Url, ReturnPage, ReturnSearch, ReturnSort, ReturnView);

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        string? p = null,
        string? page = null,
        string? search = null,
        string? sort = null,
        string? view = null,
        string? returnPage = null,
        string? returnSearch = null,
        string? returnSort = null,
        string? returnView = null,
        CancellationToken cancellationToken = default)
    {
        ReturnPage = returnPage ?? p ?? page ?? string.Empty;
        ReturnSearch = returnSearch ?? search ?? string.Empty;
        ReturnSort = returnSort ?? sort ?? string.Empty;
        ReturnView = returnView ?? view ?? string.Empty;
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
        CurrentId = id;

        var result = await unidadOrganizativaApiClient.ReactivateAsync(id, cancellationToken);

        if (result.IsSuccess)
        {
            TempData["StatusMessage"] = "La unidad organizativa se reactivó correctamente.";
            TempData["StatusKind"] = "success";
            return RedirectToPage("/Organizacion/UnidadesOrganizativas/Details", new { id, returnPage = ReturnPage, returnSearch = ReturnSearch, returnSort = ReturnSort, returnView = ReturnView });
        }

        var message = result.Error?.Type switch
        {
            UnidadOrganizativaErrorType.Conflict => $"No se pudo reactivar la unidad organizativa. {result.Error.Message}",
            UnidadOrganizativaErrorType.NotFound => "La unidad organizativa ya no está disponible para reactivar.",
            _ => "No se pudo reactivar la unidad organizativa. Intentá nuevamente."
        };

        TempData["StatusMessage"] = message;
        TempData["StatusKind"] = "danger";

        IsNotFound = true;
        IsRecoverable = true;
        CurrentId = id;
        return Page();
    }
}
