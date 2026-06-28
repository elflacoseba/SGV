using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

    public bool HasParent => Unidad?.UnidadPadreId is not null;

    public string ReturnPage { get; private set; } = string.Empty;

    public string ReturnSearch { get; private set; } = string.Empty;

    public string ReturnSort { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid id, string? page = null, string? search = null, string? sort = null, CancellationToken cancellationToken = default)
    {
        ReturnPage = page ?? string.Empty;
        ReturnSearch = search ?? string.Empty;
        ReturnSort = sort ?? string.Empty;

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
            return Page();
        }

        return Page();
    }
}
