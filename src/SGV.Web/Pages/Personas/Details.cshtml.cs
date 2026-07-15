using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Web.Integration.Personas;

namespace SGV.Web.Pages.Personas;

/// <summary>
/// PageModel del detalle readonly de personas. Carga una persona por su
/// identificador y expone la vista de solo lectura o un estado de no
/// disponible cuando la persona no puede consultarse. Acceso autenticado
/// (cualquier rol); preserva <c>p/search/sort/status</c> para el enlace
/// de retorno al listado.
/// </summary>
[Authorize]
public sealed class DetailsModel(IPersonaApiClient personaApiClient, ILogger<DetailsModel> logger) : PageModel
{
    /// <summary>Datos de la persona obtenidos desde la API.</summary>
    public PersonaDto? Persona { get; private set; }

    /// <summary>
    /// Indica si la persona solicitada no pudo obtenerse (no encontrada
    /// o error de consulta). La vista debe mostrar un estado recuperable.
    /// </summary>
    public bool IsNotFound { get; private set; }

    /// <summary>Página del listado desde la que se navegó al detalle.</summary>
    public int CurrentPage { get; private set; } = 1;

    /// <summary>Término de búsqueda activo al navegar al detalle.</summary>
    public string? Search { get; private set; }

    /// <summary>Orden activo al navegar al detalle.</summary>
    public string? Sort { get; private set; }

    /// <summary>Segmento activo al navegar al detalle (<c>eliminadas</c> o null).</summary>
    public string? Status { get; private set; }

    /// <summary>
    /// Mensaje de feedback (success/warning/danger) entregado vía
    /// TempData tras una redirección PRG desde otra página (e.g. Edit).
    /// </summary>
    public string? StatusMessage => TempData[nameof(StatusMessage)] as string;

    /// <summary>
    /// Tipo de feedback: <c>success</c>, <c>warning</c> o <c>danger</c>.
    /// Por defecto <c>success</c>.
    /// </summary>
    public string StatusKind => TempData[nameof(StatusKind)] as string ?? "success";

    /// <summary>
    /// Handler GET del detalle readonly. Carga la persona por id y, si no
    /// se encuentra o la consulta falla, marca <see cref="IsNotFound"/>.
    /// Los parámetros <c>p</c>, <c>search</c>, <c>sort</c> y
    /// <c>returnStatus</c> se preservan para el enlace de retorno.
    /// </summary>
    public async Task OnGetAsync(
        Guid id,
        [FromQuery(Name = "p")] int currentPage = 1,
        [FromQuery(Name = "search")] string? search = null,
        [FromQuery(Name = "sort")] string? sort = null,
        [FromQuery(Name = "returnStatus")] string? returnStatus = null,
        CancellationToken cancellationToken = default)
    {
        CurrentPage = Math.Max(1, currentPage);
        Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        Sort = string.IsNullOrWhiteSpace(sort) ? null : sort.Trim();
        Status = string.Equals(returnStatus, "eliminadas", StringComparison.OrdinalIgnoreCase)
            ? "eliminadas"
            : null;

        try
        {
            Persona = await personaApiClient.GetByIdAsync(id, cancellationToken);

            if (Persona is null)
            {
                IsNotFound = true;
                logger.LogWarning("Persona with Id {PersonaId} was not found or is no longer available.", id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load persona with Id {PersonaId}.", id);
            IsNotFound = true;
        }
    }
}