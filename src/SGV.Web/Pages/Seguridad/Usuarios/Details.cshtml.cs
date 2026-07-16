using System.Security.Claims;
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
/// <remarks>
/// <para>
/// Phase 3 del change <c>2026-07-15-quita-soft-delete-usuario</c>: la
/// página consulta el DTO real vía API y delega en el flag
/// <see cref="UsuarioDto.Bloqueado"/> la decisión visual del estado.
/// El query string <c>returnStatus</c> queda como hint de view (e.g.
/// para deep-links desde el listado bloqueadas) pero NO es fuente de
/// verdad: si la API devuelve <c>Bloqueado=true</c>, el banner de
/// cuenta bloqueada se renderiza aunque <c>returnStatus</c> diga
/// <c>activas</c>.
/// </para>
/// </remarks>
[Authorize]
public sealed class DetailsModel(
    IUsuarioApiClient usuarioApiClient,
    ILogger<DetailsModel> logger) : PageModel
{
    private const string ActiveView = "activas";
    private const string BlockedView = "bloqueadas";

    public UsuarioDto? Usuario { get; private set; }

    public bool IsNotFound { get; private set; }

    public int CurrentPage { get; private set; } = 1;

    public string? Search { get; private set; }

    public string? Sort { get; private set; }

    public string Segmento { get; private set; } = ActiveView;

    /// <summary>
    /// View hint solicitado por el caller vía <c>returnStatus</c>. Es
    /// sólo un hint — la verdad sobre el lockout sale del DTO. La vista
    /// usa este valor para construir el "Volver al listado" con el
    /// query string preservado.
    /// </summary>
    public bool IsBlockedView =>
        string.Equals(Segmento, BlockedView, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Estado efectivo leído del DTO. Si el usuario está cargado y su
    /// <see cref="UsuarioDto.Bloqueado"/> es <c>true</c>, la vista
    /// muestra el banner de "Cuenta bloqueada" y las acciones de
    /// Desbloquear. Tiene precedencia sobre <see cref="IsBlockedView"/>.
    /// </summary>
    public bool Bloqueado => Usuario?.Bloqueado == true;

    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    /// <summary>
    /// Identificador del admin actualmente autenticado (claim
    /// <see cref="ClaimTypes.NameIdentifier"/>). Se usa para auto-fence
    /// contra Bloquear/Eliminar en la vista.
    /// </summary>
    public string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// Helper que la vista usa para decidir si debe renderizar el form
    /// de Bloquear / Eliminar sobre la fila del usuario con
    /// identificador <paramref name="targetUserId"/>.
    /// </summary>
    public bool EsAutoAccion(string targetUserId) =>
        !string.IsNullOrEmpty(CurrentUserId)
        && string.Equals(CurrentUserId, targetUserId, StringComparison.Ordinal);

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
                // CodeQL [SM02379]: structured logging placeholder, not interpolated.
                logger.LogWarning("Usuario with Id {UsuarioId} was not found or is no longer available.", id);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // CodeQL [SM02379]: structured logging placeholder, not interpolated.
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
        string.Equals(status, BlockedView, StringComparison.OrdinalIgnoreCase)
            ? BlockedView
            : ActiveView;
}

