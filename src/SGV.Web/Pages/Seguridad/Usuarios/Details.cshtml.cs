using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Web.Integration.Usuarios;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Seguridad.Usuarios;

/// <summary>
/// Readonly Usuarios detail page with recoverable not-found behavior and
/// navigation context preserved back to the segmented index.
/// </summary>
[Authorize]
public sealed class DetailsModel(
    IUsuarioApiClient usuarioApiClient,
    ILogger<DetailsModel> logger) : PageModel
{
    private const string ActiveView = "activas";
    private const string DeletedView = "eliminadas";

    public UsuarioDto? Usuario { get; private set; }

    public bool IsNotFound { get; private set; }

    public int CurrentPage { get; private set; } = 1;

    public string? Search { get; private set; }

    public string? Sort { get; private set; }

    public string Segmento { get; private set; } = ActiveView;

    public bool IsDeletedView =>
        string.Equals(Segmento, DeletedView, StringComparison.OrdinalIgnoreCase);

    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    public string? StatusMessage => PageFeedback.GetStatusMessage(TempData);

    public string StatusKind => PageFeedback.GetStatusKind(TempData);

    public async Task OnGetAsync(
        string id,
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
            Usuario = await usuarioApiClient.GetByIdAsync(id, cancellationToken);
            if (Usuario is null)
            {
                IsNotFound = true;
                logger.LogWarning("Usuario with Id {UsuarioId} was not found or is no longer available.", id);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load usuario with Id {UsuarioId}.", id);
            IsNotFound = true;
        }
    }

    public string BuildIndexUrl() => BuildContextUrl(
        "/seguridad/usuarios",
        Segmento,
        statusKey: "status");

    public string BuildEditUrl(string id) => BuildContextUrl(
        $"/seguridad/usuarios/editar/{Uri.EscapeDataString(id)}",
        Segmento,
        statusKey: "returnStatus");

    private string BuildContextUrl(string basePath, string status, string statusKey)
    {
        var values = new List<string>();
        if (CurrentPage > 1)
        {
            values.Add($"p={CurrentPage}");
        }
        if (!string.IsNullOrWhiteSpace(Search))
        {
            values.Add($"search={Uri.EscapeDataString(Search)}");
        }
        if (!string.IsNullOrWhiteSpace(Sort))
        {
            values.Add($"sort={Uri.EscapeDataString(Sort)}");
        }
        values.Add($"{statusKey}={Uri.EscapeDataString(status)}");

        return $"{basePath}?{string.Join("&", values)}";
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeSegmento(string? status) =>
        string.Equals(status, DeletedView, StringComparison.OrdinalIgnoreCase)
            ? DeletedView
            : ActiveView;
}
