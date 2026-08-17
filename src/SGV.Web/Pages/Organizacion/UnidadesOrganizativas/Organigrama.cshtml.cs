using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.UnidadesOrganizativas;

[Microsoft.AspNetCore.Authorization.Authorize]
public sealed class OrganigramaModel(IUnidadOrganizativaApiClient unidadOrganizativaApiClient, ILogger<OrganigramaModel> logger) : PageModel
{
    public IReadOnlyList<UnidadOrganizativaTreeNodeViewModel> TreeItems { get; private set; } = [];

    /// <summary>
    /// IDs de los nodos involucrados en ciclos detectados por el backend
    /// (issue #277). Si está vacío, no se muestra ningún warning.
    /// </summary>
    public IReadOnlyList<Guid> CyclicNodeIds { get; private set; } = [];

    public bool HasCyclicNodes => CyclicNodeIds.Count > 0;

    public string? LoadErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await unidadOrganizativaApiClient.GetTreeAsync(cancellationToken);
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            TreeItems = result.Arbol.Select(node => MapToViewModel(node, hoy)).ToArray();
            CyclicNodeIds = result.NodosConCiloDetectado;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load organigrama tree.");
            TreeItems = [];
            CyclicNodeIds = [];
            LoadErrorMessage = "No se pudo cargar el organigrama. Intentá nuevamente.";
        }

        return Page();
    }

    private static UnidadOrganizativaTreeNodeViewModel MapToViewModel(UnidadOrganizativaTreeNodeDto item, DateOnly hoy)
        => new(
            item.Id,
            item.Codigo,
            item.Nombre,
            item.TipoUnidadNombre,
            VigenciaViewModel.Desde(item.VigenteDesde, item.VigenteHasta, hoy),
            EsVigente(item.VigenteDesde, item.VigenteHasta, hoy),
            item.Hijas.Select(child => MapToViewModel(child, hoy)).ToArray());

    /// <summary>
    /// Proyecta la ventana de vigencia persistida a un booleano que el
    /// JavaScript pueda consumir para filtrar visualmente las unidades
    /// cuya vigencia ya cerró (issue #286). Espeja
    /// <c>UnidadOrganizativa.EsVigente</c> pero opera sobre datos del
    /// wire sin materializar una entidad de dominio.
    /// </summary>
    private static bool EsVigente(DateOnly? desde, DateOnly? hasta, DateOnly hoy)
    {
        if (desde.HasValue && desde.Value > hoy) return false;
        if (hasta.HasValue && hasta.Value < hoy) return false;
        return true;
    }
}