using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.UnidadesOrganizativas;

[Microsoft.AspNetCore.Authorization.Authorize]
public sealed class OrganigramaModel(IUnidadOrganizativaApiClient unidadOrganizativaApiClient, ILogger<OrganigramaModel> logger) : PageModel
{
    public IReadOnlyList<UnidadOrganizativaTreeNodeViewModel> TreeItems { get; private set; } = [];

    public string? LoadErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await unidadOrganizativaApiClient.GetTreeAsync(cancellationToken);
            TreeItems = result.Select(MapToViewModel).ToArray();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load organigrama tree.");
            TreeItems = [];
            LoadErrorMessage = "No se pudo cargar el organigrama. Intentá nuevamente.";
        }

        return Page();
    }

    private static UnidadOrganizativaTreeNodeViewModel MapToViewModel(UnidadOrganizativaTreeNodeDto item)
        => new(
            item.Id,
            item.Codigo,
            item.Nombre,
            item.TipoUnidadNombre,
            item.Hijas.Select(MapToViewModel).ToArray());
}
