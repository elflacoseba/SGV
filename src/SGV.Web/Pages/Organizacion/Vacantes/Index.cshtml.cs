using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Vacantes.Consultas;
using SGV.Contracts.Vacantes.Enums;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Vacantes;

namespace SGV.Web.Pages.Organizacion.Vacantes;

/// <summary>
/// PageModel for the segmented Vacantes list.
/// </summary>
[Authorize]
public sealed class IndexModel(
    IVacanteApiClient vacanteApiClient,
    ILogger<IndexModel> logger) : PageModel
{
    private const string Abiertas = "abiertas";
    private const string Cerradas = "cerradas";
    private const string Todas = "todas";

    /// <summary>Rows rendered by the current page.</summary>
    public IReadOnlyList<VacanteListItemViewModel> Items { get; private set; } = [];

    /// <summary>Current one-based page.</summary>
    public int CurrentPage { get; private set; } = 1;

    /// <summary>Total rows in the selected backend segment.</summary>
    public int TotalCount { get; private set; }

    /// <summary>Total pages calculated from the backend result.</summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>Current search value.</summary>
    public string? Search { get; private set; }

    /// <summary>Current server-side sort value.</summary>
    public string? Sort { get; private set; }

    /// <summary>Normalized segment sent to the backend.</summary>
    public string Segmento { get; private set; } = Abiertas;

    /// <summary>Visible error when the API cannot be reached.</summary>
    public string? LoadErrorMessage { get; private set; }

    /// <summary>Whether the current user may execute vacante mutations.</summary>
    public bool CanMutate => User.IsInRole("Administrador") || User.IsInRole("GestorVacantes");

    /// <summary>Whether the current segment includes terminal vacantes.</summary>
    public bool IsCerradasView => string.Equals(Segmento, Cerradas, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether the current segment includes both open and terminal vacantes.</summary>
    public bool IsTodasView => string.Equals(Segmento, Todas, StringComparison.OrdinalIgnoreCase);

    public async Task OnGetAsync(
        [FromQuery(Name = "p")] int currentPage = 1,
        string? search = null,
        string? sort = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        CurrentPage = Math.Max(1, currentPage);
        Search = Normalize(search);
        Sort = Normalize(sort);
        Segmento = NormalizeSegmento(status);

        try
        {
            var result = await vacanteApiClient.ListarAsync(
                new VacanteListQuery(
                    CurrentPage,
                    PageSize: 20,
                    Search,
                    Sort,
                    ToSegmento(Segmento)),
                cancellationToken);

            CurrentPage = Math.Max(1, result.Page);
            TotalCount = Math.Max(0, result.TotalCount);
            TotalPages = Math.Max(1, (int)Math.Ceiling(
                TotalCount / (double)Math.Max(1, result.PageSize)));
            Items = result.Items.Select(VacanteListItemViewModel.FromDto).ToArray();
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load vacantes page: transport failure.");
            SetLoadErrorState();
        }
    }

    /// <summary>Builds the details link while preserving list context.</summary>
    public string BuildDetailsUrl(Guid id)
        => Url.Page(
            "/Organizacion/Vacantes/Details",
            new { id, p = CurrentPage, search = Search, sort = Sort, returnStatus = Segmento })
            ?? $"/organizacion/vacantes/detalles/{id:D}";

    /// <summary>Builds the edit link while preserving list context.</summary>
    public string BuildEditUrl(Guid id)
        => Url.Page(
            "/Organizacion/Vacantes/Edit",
            new { id, p = CurrentPage, search = Search, sort = Sort, returnStatus = Segmento })
            ?? $"/organizacion/vacantes/editar/{id:D}";

    /// <summary>Builds a segment toggle URL with pagination reset.</summary>
    public object BuildToggleSegmentoRouteValues(string targetSegmento) => new
    {
        p = 1,
        search = Search,
        sort = Sort,
        status = NormalizeSegmento(targetSegmento)
    };

    /// <summary>Builds a pagination URL preserving filters and segment.</summary>
    public object BuildPagedRouteValues(int page) => new
    {
        p = Math.Max(1, page),
        search = Search,
        sort = Sort,
        status = Segmento
    };

    private void SetLoadErrorState()
    {
        Items = [];
        TotalCount = 0;
        CurrentPage = 1;
        TotalPages = 1;
        LoadErrorMessage = "No se pudo cargar el listado de vacantes. Intentá nuevamente.";
    }

    private static VacanteSegmentoListado ToSegmento(string segment) => segment switch
    {
        Cerradas => VacanteSegmentoListado.Cerradas,
        Todas => VacanteSegmentoListado.Todas,
        _ => VacanteSegmentoListado.Abiertas
    };

    private static string NormalizeSegmento(string? status)
        => status?.Trim().ToLowerInvariant() switch
        {
            Cerradas => Cerradas,
            Todas => Todas,
            _ => Abiertas
        };

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
