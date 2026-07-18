using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Personas;
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
/// <para>
/// <see cref="AutoValidateAntiforgeryTokenAttribute"/> se aplica a nivel
/// de PageModel como defensa en profundidad (RIS-001): los forms de
/// Bloquear / Desbloquear / Eliminar en la vista postean contra
/// <c>/seguridad/usuarios?handler=…</c> (IndexModel), pero si en el
/// futuro DetailsModel suma POST handlers propios, el atributo ya
/// estará en su lugar. La vista sigue emitiendo
/// <c>@Html.AntiForgeryToken()</c> en cada form.
/// </para>
/// </remarks>
[Authorize]
[AutoValidateAntiforgeryToken]
public sealed class DetailsModel(
    IUsuarioApiClient usuarioApiClient,
    IPersonaApiClient personaApiClient,
    ILogger<DetailsModel> logger) : PageModel
{
    private const string ActiveView = "activas";
    private const string BlockedView = "bloqueadas";

    public UsuarioDto? Usuario { get; private set; }

    public bool IsNotFound { get; private set; }

    /// <summary>
    /// Persona vinculada al usuario, proyectada como DTO para que la
    /// vista renderice la card enriquecida read-only. <c>null</c> cuando
    /// el usuario no tiene persona asignada, cuando el API devolvió 404,
    /// o cuando el fetch sufrió un fallo de transporte: en esos casos la
    /// UI cae al fallback <see cref="PersonaDisplay"/>.
    /// </summary>
    /// <remarks>
    /// Espejo 1-a-1 de <c>EditModel.PersonaVinculada</c> introducido en
    /// PR #168. La card enriquecida replica el árbol DOM de
    /// <c>_Form.cshtml</c> sin los botones Quitar/Cambiar ni el modal —
    /// Details es estrictamente read-only.
    /// </remarks>
    public PersonaDto? PersonaVinculada { get; private set; }

    /// <summary>
    /// Display plano "Apellidos, Nombres" derivado del
    /// <see cref="UsuarioDto"/>. Se usa como fallback cuando
    /// <see cref="PersonaVinculada"/> es <c>null</c>.
    /// </summary>
    public string? PersonaDisplay { get; private set; }

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
    /// <remarks>
    /// REA-014: el nombre anterior <c>Bloqueado</c> sombreaba el
    /// homónimo del DTO (<see cref="UsuarioDto.Bloqueado"/>) y hacía
    /// trivial confundir el "flag de lockout del usuario X" con "este
    /// PageModel cree que X está bloqueado". Se renombra a
    /// <c>IsCuentaBloqueada</c> para hacer explícita la proyección.
    /// </remarks>
    public bool IsCuentaBloqueada => Usuario?.Bloqueado == true;

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
            else
            {
                PersonaDisplay = FormatPersonaDisplay(Usuario.Apellidos, Usuario.Nombres);
                await TryLoadPersonaVinculadaAsync(Usuario.PersonaId, cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Enriquecimiento opcional de la card de Persona vinculada. 404 y
    /// fallos de transporte son no-bloqueantes: la vista cae al fallback
    /// <see cref="PersonaDisplay"/>. Un <c>Guid.Empty</c> en el id se
    /// trata como "sin persona asignada" sin tocar el API.
    /// </summary>
    /// <remarks>
    /// Espejo 1-a-1 de <c>EditModel.TryLoadPersonaVinculadaAsync</c>
    /// (PR #168). El catch usa
    /// <see cref="TransportFailureClassifier.IsTransportFailure"/> para
    /// NO marcar <see cref="IsNotFound"/>: el usuario sí existe, sólo se
    /// degrada la presentación de la card.
    /// </remarks>
    private async Task TryLoadPersonaVinculadaAsync(
        Guid personaId,
        CancellationToken cancellationToken)
    {
        if (personaId == Guid.Empty)
        {
            return;
        }

        try
        {
            PersonaVinculada = await personaApiClient
                .GetByIdAsync(personaId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            // CodeQL [SM02379]: structured logging placeholder, not interpolated.
            logger.LogWarning(
                ex,
                "Failed to enrich linked persona {PersonaId} for detail page; falling back to PersonaDisplay.",
                personaId);
            PersonaVinculada = null;
        }
    }

    private static string FormatPersonaDisplay(string? apellidos, string? nombres)
    {
        var display = string.Join(", ", new[] { apellidos, nombres }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(display) ? "Persona vinculada" : display;
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

