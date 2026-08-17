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

    /// <summary>
    /// Proyecta el DTO del árbol al ViewModel que consume el shell web.
    /// Las fechas de vigencia se exponen CRUDAS al JavaScript para que el
    /// filtro de "Mostrar unidades expiradas" se calcule enteramente en el
    /// cliente (issue #286 — tercer feedback). Ya no proyectamos un
    /// <c>EsVigente</c> server-side porque daba resultados confusos
    /// cuando las unidades no tenían <c>VigenteHasta</c> configurado.
    /// </summary>
    private static UnidadOrganizativaTreeNodeViewModel MapToViewModel(UnidadOrganizativaTreeNodeDto item, DateOnly hoy)
        => new(
            item.Id,
            item.Codigo,
            item.Nombre,
            item.TipoUnidadNombre,
            VigenciaViewModel.Desde(item.VigenteDesde, item.VigenteHasta, hoy),
            item.VigenteDesde,
            item.VigenteHasta,
            item.Hijas.Select(child => MapToViewModel(child, hoy)).ToArray());
}