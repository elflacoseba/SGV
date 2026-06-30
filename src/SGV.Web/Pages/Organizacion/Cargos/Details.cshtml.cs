using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SGV.Web.Pages.Organizacion.Cargos;

/// <summary>
/// Placeholder del detalle readonly de cargos. La implementación completa
/// (visualización y estado de no encontrado) se entrega en PR 3.
/// </summary>
[Authorize]
public sealed class DetailsModel : PageModel
{
    public void OnGet()
    {
    }
}