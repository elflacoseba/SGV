using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Personas.Consultas.Dtos;

namespace SGV.Web.Pages.Tests;

/// <summary>
/// PageModel del harness de testing
/// <c>/tests/persona-card-harness</c>. Construye un <see cref="PersonaDto"/> a
/// partir de query string y delega en la partial
/// <c>_PersonaCard.cshtml</c> con los <see cref="ViewData"/> parametrizados.
/// Existe para que la suite de integración web pueda ejercitar la partial
/// antes de que los consumers reales (Usuarios y Ocupaciones) migren — ver
/// Slice 1 / PR 1 del change <c>reusable-persona-card</c> (issue #219).
/// </summary>
/// <remarks>
/// Sólo accesible a usuarios autenticados. La ruta está pensada para el
/// alcance de <c>SgvWebApplicationFactory</c> + tests de integración;
/// los usuarios finales no la invocan en producción porque no aparece en
/// ningún link ni navegación.
/// </remarks>
[Authorize]
public sealed class PersonaCardHarnessModel : PageModel
{
    public PersonaDto? Persona { get; private set; }

    public string Mode { get; private set; } = "readonly";

    public bool ShowStatusBadge { get; private set; } = true;

    public bool ShowQuitarCambiar { get; private set; } = true;

    public string? PersonaDetailUrl { get; private set; }

    public string? FallbackDisplay { get; private set; }

    public string? FallbackUrl { get; private set; }

    public string ModalId { get; private set; } = "usuario-persona-buscador-modal";

    public string PersonaIdInputName { get; private set; } = "Input.PersonaId";

    public string DisplayContainerId { get; private set; } = "usuario-persona-display";

    public void OnGet()
    {
        var rawMode = (Request.Query["mode"].ToString() ?? "readonly").Trim().ToLowerInvariant();
        Mode = string.Equals(rawMode, "editable", StringComparison.Ordinal)
            ? "editable"
            : "readonly";

        ShowStatusBadge = TryParseBool(Request.Query["showStatusBadge"], defaultValue: true);
        ShowQuitarCambiar = TryParseBool(Request.Query["showQuitarCambiar"], defaultValue: true);

        var rawPersonaId = Request.Query["personaId"].ToString();
        if (Guid.TryParse(rawPersonaId, out var personaId))
        {
            var tipoDoc = NullIfEmpty(Request.Query["tipoDocCodigo"]);
            Persona = new PersonaDto(
                Id: personaId,
                Legajo: NullIfEmpty(Request.Query["legajo"]),
                Nombres: NullIfEmpty(Request.Query["nombres"]) ?? "Ana",
                Apellidos: NullIfEmpty(Request.Query["apellidos"]) ?? "García",
                Email: NullIfEmpty(Request.Query["email"]),
                TipoDocumentoId: tipoDoc is null ? null : Guid.NewGuid(),
                TipoDocumentoCodigo: tipoDoc,
                TipoDocumentoNombre: tipoDoc,
                NumeroDocumento: NullIfEmpty(Request.Query["numeroDocumento"]),
                Telefono: NullIfEmpty(Request.Query["telefono"]),
                IsActive: TryParseBool(Request.Query["isActive"], defaultValue: true));
        }

        PersonaDetailUrl = NullIfEmpty(Request.Query["personaDetailUrl"]);
        FallbackDisplay = NullIfEmpty(Request.Query["fallbackDisplay"]);
        FallbackUrl = NullIfEmpty(Request.Query["fallbackUrl"]);
        ModalId = NullIfEmpty(Request.Query["modalId"]) ?? ModalId;
        PersonaIdInputName = NullIfEmpty(Request.Query["personaIdInputName"]) ?? PersonaIdInputName;
        DisplayContainerId = NullIfEmpty(Request.Query["displayContainerId"]) ?? DisplayContainerId;
    }

    private static bool TryParseBool(Microsoft.Extensions.Primitives.StringValues values, bool defaultValue)
    {
        var raw = values.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return bool.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static string? NullIfEmpty(Microsoft.Extensions.Primitives.StringValues v)
        => NullIfEmpty(v.ToString());
}