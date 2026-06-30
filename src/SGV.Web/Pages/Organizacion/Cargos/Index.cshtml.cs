using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SGV.Web.Pages.Organizacion.Cargos;

/// <summary>
/// Placeholder del listado web de cargos. La implementación completa (tabla,
/// búsqueda, orden, paginación y baja lógica) se entrega en PR 2.
/// </summary>
[Authorize]
public sealed class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}