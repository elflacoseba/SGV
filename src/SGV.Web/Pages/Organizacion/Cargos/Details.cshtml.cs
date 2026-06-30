using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.Cargos;

/// <summary>
/// PageModel del detalle readonly de cargos. Carga un cargo por su
/// identificador y expone la vista de solo lectura o un estado de
/// no disponible cuando el cargo no puede consultarse.
/// </summary>
[Authorize]
public sealed class DetailsModel(ICargoApiClient cargoApiClient, ILogger<DetailsModel> logger) : PageModel
{
    /// <summary>
    /// Datos del cargo obtenidos desde la API. <c>null</c> cuando el
    /// cargo no se encuentra o la consulta falla.
    /// </summary>
    public CargoDto? Cargo { get; private set; }

    /// <summary>
    /// Indica si el cargo solicitado no pudo obtenerse (no encontrado
    /// o error de consulta). La vista debe mostrar un estado recuperable
    /// sin acción de reactivación.
    /// </summary>
    public bool IsNotFound { get; private set; }

    /// <summary>
    /// Página del listado desde la que se navegó al detalle (se preserva
    /// en el enlace de retorno). Por defecto <c>1</c>.
    /// </summary>
    public int CurrentPage { get; private set; } = 1;

    /// <summary>
    /// Término de búsqueda activo al navegar al detalle (se preserva en
    /// el enlace de retorno).
    /// </summary>
    public string? Search { get; private set; }

    /// <summary>
    /// Orden activo al navegar al detalle (se preserva en el enlace de
    /// retorno).
    /// </summary>
    public string? Sort { get; private set; }

    /// <summary>
    /// Handler GET del detalle readonly. Carga el cargo por id y, si no
    /// se encuentra o la consulta falla, marca <see cref="IsNotFound"/>.
    /// Los parámetros <c>p</c>, <c>search</c> y <c>sort</c> se preservan
    /// para el enlace de retorno al listado.
    /// </summary>
    public async Task OnGetAsync(
        Guid id,
        [FromQuery(Name = "p")] int currentPage = 1,
        string? search = null,
        string? sort = null,
        CancellationToken cancellationToken = default)
    {
        CurrentPage = Math.Max(1, currentPage);
        Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        Sort = string.IsNullOrWhiteSpace(sort) ? null : sort.Trim();

        try
        {
            Cargo = await cargoApiClient.GetByIdAsync(id, cancellationToken);

            if (Cargo is null)
            {
                IsNotFound = true;
                logger.LogWarning("Cargo with Id {CargoId} was not found or is no longer available.", id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load cargo with Id {CargoId}.", id);
            IsNotFound = true;
        }
    }
}
