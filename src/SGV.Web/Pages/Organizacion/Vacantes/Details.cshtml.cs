using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Vacantes;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Organizacion.Vacantes;

/// <summary>
/// Read-only vacante detail page, including chronological state history.
/// </summary>
[Authorize]
public sealed class DetailsModel(
    IVacanteApiClient vacanteApiClient,
    ILogger<DetailsModel> logger) : PageModel
{
    /// <summary>Mapped detail displayed by the page.</summary>
    public VacanteDetailViewModel? ViewModel { get; private set; }

    /// <summary>Whether the requested vacante is unavailable.</summary>
    public bool IsNotFound { get; private set; }

    /// <summary>Recoverable load error.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>List page to preserve when returning.</summary>
    public int CurrentPage { get; private set; } = 1;

    /// <summary>Search filter to preserve when returning.</summary>
    public string? Search { get; private set; }

    /// <summary>Sort filter to preserve when returning.</summary>
    public string? Sort { get; private set; }

    /// <summary>Active segment to preserve when returning.</summary>
    public string Segmento { get; private set; } = "abiertas";

    /// <summary>One-time success feedback after a mutation.</summary>
    public string? StatusMessage => PageFeedback.GetStatusMessage(TempData);

    /// <summary>Feedback CSS kind.</summary>
    public string StatusKind => PageFeedback.GetStatusKind(TempData);

    /// <summary>Whether the current user may navigate to Edit.</summary>
    public bool CanMutate => User.IsInRole(RolesSgv.Administrador) || User.IsInRole(RolesSgv.GestorVacantes);

    public async Task OnGetAsync(
        Guid id,
        [FromQuery(Name = "p")] int currentPage = 1,
        string? search = null,
        string? sort = null,
        string? returnStatus = null,
        CancellationToken cancellationToken = default)
    {
        CurrentPage = Math.Max(1, currentPage);
        Search = Normalize(search);
        Sort = Normalize(sort);
        Segmento = NormalizeSegmento(returnStatus);

        try
        {
            var dto = await vacanteApiClient.ObtenerPorIdAsync(id, cancellationToken);
            if (dto is null)
            {
                IsNotFound = true;
                logger.LogWarning("Vacante with Id {Id} was not found.", id);
                return;
            }

            ViewModel = VacanteDetailViewModel.FromDto(dto);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            IsNotFound = true;
            ErrorMessage = PageFeedback.TransportMessage;
            logger.LogError(ex, "Failed to load vacante with Id {Id}.", id);
        }
    }

    public string BuildEditUrl()
        => ViewModel is null
            ? "/organizacion/vacantes"
            : Url.Page(
                "/Organizacion/Vacantes/Edit",
                new { id = ViewModel.Id, p = CurrentPage, search = Search, sort = Sort, returnStatus = Segmento })
                ?? $"/organizacion/vacantes/editar/{ViewModel.Id:D}";

    public string BuildBackUrl()
        => Url.Page(
            "/Organizacion/Vacantes/Index",
            new { p = CurrentPage, search = Search, sort = Sort, status = Segmento })
            ?? "/organizacion/vacantes";

    private static string NormalizeSegmento(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "cerradas" => "cerradas",
            "todas" => "todas",
            _ => "abiertas"
        };

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
