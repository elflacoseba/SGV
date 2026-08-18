using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Habilidades;

namespace SGV.Web.Pages.Organizacion.Habilidades;

/// <summary>
/// PageModel del detalle readonly de una Habilidad.
/// </summary>
[Authorize]
public sealed class DetailsModel(IHabilidadApiClient habilidadApiClient, ILogger<DetailsModel> logger) : PageModel
{
    public HabilidadDto? Habilidad { get; private set; }

    /// <summary>
    /// <c>true</c> cuando la habilidad solicitada no se encontró o la
    /// consulta falló. La vista debe mostrar un estado recuperable sin
    /// acción de reactivación.
    /// </summary>
    public bool IsNotFound { get; private set; }

    /// <summary>
    /// Página del listado desde la que se navegó al detalle.
    /// </summary>
    public int CurrentPage { get; private set; } = 1;

    /// <summary>
    /// Término de búsqueda activo al navegar al detalle.
    /// </summary>
    public string? Search { get; private set; }

    /// <summary>
    /// Orden activo al navegar al detalle.
    /// </summary>
    public string? Sort { get; private set; }

    /// <summary>
    /// Segmento del listado desde el que se llegó.
    /// </summary>
    public string? ReturnStatus { get; private set; }

    public string ReturnToListUrl
    {
        get
        {
            var status = string.Equals(ReturnStatus, "eliminadas", StringComparison.OrdinalIgnoreCase)
                ? "eliminadas"
                : null;

            return Url.Page("/Organizacion/Habilidades/Index", new
            {
                p = CurrentPage,
                search = Search,
                sort = Sort,
                status
            }) ?? "/organizacion/habilidades";
        }
    }

    public async Task OnGetAsync(
        Guid id,
        [FromQuery(Name = "p")] int currentPage = 1,
        string? search = null,
        string? sort = null,
        string? returnStatus = null,
        CancellationToken cancellationToken = default)
    {
        CurrentPage = Math.Max(1, currentPage);
        Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        Sort = string.IsNullOrWhiteSpace(sort) ? null : sort.Trim();
        ReturnStatus = string.IsNullOrWhiteSpace(returnStatus) ? null : returnStatus.Trim();

        try
        {
            Habilidad = await habilidadApiClient.GetByIdAsync(id, cancellationToken);

            if (Habilidad is null)
            {
                IsNotFound = true;
                logger.LogWarning("Habilidad with Id {HabilidadId} was not found or is no longer available.", id);
            }
        }
        // Issue #125: catch centralizado via TransportFailureClassifier; la
        // cancelación cooperativa del caller NO se captura (request
        // cancelado = no renderizamos). El includeOperationCanceled: true
        // acepta OperationCanceledException cuando el token del caller NO
        // fue el origen de la cancelación (preserva semántica anterior).
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(
            ex, includeOperationCanceled: !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Failed to load habilidad with Id {HabilidadId}.", id);
            IsNotFound = true;
        }
    }

    public string? StatusMessage => TempData[nameof(StatusMessage)] as string;

    public string StatusKind => TempData[nameof(StatusKind)] as string ?? "success";
}