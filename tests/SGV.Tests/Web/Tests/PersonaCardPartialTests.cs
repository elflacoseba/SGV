using System.Net;
using System.Web;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using Xunit;

namespace SGV.Tests.Web.Tests;

/// <summary>
/// Integration tests for the new partial view
/// <c>src/SGV.Web/Pages/Shared/Partials/_PersonaCard.cshtml</c>.
/// Slice 1 / PR 1 of change <c>reusable-persona-card</c> (issue #219).
///
/// El parcial NO se usa todavía por ningún consumer real (las
/// migraciones de Usuarios y Ocupaciones son Slice 2 y Slice 3
/// respectivamente); por eso la suite lo ejercita contra una página
/// "harness" interna (<c>/tests/persona-card-harness</c>) que vive en
/// <c>src/SGV.Web/Pages/Tests/PersonaCardHarness.cshtml</c> y actúa
/// como consumidor parametrizado: arma el <c>PersonaDto?</c> desde
/// query string y delega en la partial con los <c>ViewData</c>
/// solicitados. La página harness exige rol Administrador (mismo
/// patrón que el resto de páginas internas de diagnóstico), así que
/// un usuario anónimo recibe redirect a <c>/auth/sign-in</c> y la
/// superficie queda fuera del alcance de usuarios finales.
///
/// Cubre PER-CARD-01..05/08/10 (modos readonly/editable, defaults
/// de ViewData, contrato data-* idéntico al JS vigente, null
/// PersonaDto, partial DTO, fallback Display, link a detalle de
/// Persona). PER-CARD-06/07/09 dependen de consumers reales
/// (Slice 2/3/4) y se cubren en sus respectivos work units.
/// </summary>
[Collection("WebIntegration")]
public sealed class PersonaCardPartialTests
{
    private readonly WebIntegrationFixture _fixture;

    public PersonaCardPartialTests(WebIntegrationFixture fixture) => _fixture = fixture;

    private Task<WebClientLease> CreateAdminLeaseAsync()
        => _fixture.CreateAuthOnlyLeaseAsync(adminRole: true);

    // ──────────────────────────────────────────────
    // PER-CARD-01 / readonly con PersonaDto poblado
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ReadonlyWithPersona_RendersNombreYDocumentoSinBotonesMutables()
    {
        var personaId = Guid.NewGuid();
        var query = BuildQuery(mode: "readonly", personaId: personaId);

        await using var lease = await CreateAdminLeaseAsync();
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("García, Ana", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DNI 30123456", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-display-text", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-quitar", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-buscar", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // PER-CARD-01 / editable emite Quitar y Buscar/Cambiar
    // ──────────────────────────────────────────────

    [Fact]
    public async Task EditableWithPersona_EmitsQuitarAndBuscarButtonsAndModalBinding()
    {
        var personaId = Guid.NewGuid();
        var query = BuildQuery(mode: "editable", personaId: personaId);

        await using var lease = await CreateAdminLeaseAsync();
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-usuario-persona-quitar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Quitar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-buscar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-bs-toggle=\"modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-bs-target=\"#usuario-persona-buscador-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cambiar", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // PER-CARD-01 / Mode omitido cae a readonly
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ModeOmitted_FallsBackToReadonly()
    {
        var personaId = Guid.NewGuid();
        // Sin mode= en query string — debe comportarse como readonly.
        var query = $"personaId={personaId:D}&legajo=L-7777&nombres=Ana&apellidos=García&email=ana@example.com&tipoDocCodigo=DNI&numeroDocumento=30123456&telefono=%2B541155550000&isActive=true";

        await using var lease = await CreateAdminLeaseAsync();
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("data-usuario-persona-quitar", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-buscar", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // PER-CARD-02 / PersonaDto nulo no rompe; muestra vacío
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PersonaNull_DoesNotThrowAndRendersEmptyDisplay()
    {
        var query = "mode=readonly"; // sin personaId → la harness construye DTO null

        await using var lease = await CreateAdminLeaseAsync();
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Sin email/teléfono/documento en la salida — la partial no
        // debe renderizar filas con datos null/undefined.
        Assert.DoesNotContain("null", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("undefined", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // PER-CARD-02 / Datos completos: Email + Teléfono + Estado
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ReadonlyWithFullPersona_RendersEmailAndTelefonoAndEstadoBadge()
    {
        var personaId = Guid.NewGuid();
        var query = BuildQuery(mode: "readonly", personaId: personaId, isActive: true);

        await using var lease = await CreateAdminLeaseAsync();
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ana.garcia@example.com", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("+54 11 5555-0000", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Activa", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mailto:ana.garcia@example.com", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // PER-CARD-02 / Estado Inactivo cuando IsActive=false
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ReadonlyWithPersonaInactive_RendersInactivaBadge()
    {
        var personaId = Guid.NewGuid();
        var query = BuildQuery(mode: "readonly", personaId: personaId, isActive: false);

        await using var lease = await CreateAdminLeaseAsync();
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Inactiva", content, StringComparison.OrdinalIgnoreCase);
        // El badge "Activa" (sola) NO debe estar — coincide con la
        // palabra "Inactiva" si se busca sin anchor, así que validamos
        // por el span de bootstrap.
        Assert.Contains("badge-soft-secondary", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("badge-soft-success", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // PER-CARD-03 / ShowStatusBadge=false oculta Estado
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ShowStatusBadgeFalse_HidesEstadoBadgeButKeepsRestOfCard()
    {
        var personaId = Guid.NewGuid();
        var query = BuildQuery(mode: "readonly", personaId: personaId) + "&showStatusBadge=false";

        await using var lease = await CreateAdminLeaseAsync();
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Activa", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Inactiva", content, StringComparison.OrdinalIgnoreCase);
        // El resto de la card sigue renderizándose: nombre, documento, email, teléfono.
        Assert.Contains("García, Ana", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DNI 30123456", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ana.garcia@example.com", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // PER-CARD-05 / Contrato data-* idéntico al JS vigente
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Readonly_DoesNotEmitForbiddenDataAttributes()
    {
        var personaId = Guid.NewGuid();
        var query = BuildQuery(mode: "readonly", personaId: personaId);

        await using var lease = await CreateAdminLeaseAsync();
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Atributos inexistentes que el spec PER-CARD-05 prohíbe emitir.
        Assert.DoesNotContain("data-usuario-persona-cambiar", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-persona-id", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-modal-id", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-display-container-id=", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Editable_DoesNotEmitForbiddenDataAttributes()
    {
        var personaId = Guid.NewGuid();
        var query = BuildQuery(mode: "editable", personaId: personaId);

        await using var lease = await CreateAdminLeaseAsync();
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("data-usuario-persona-cambiar", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-persona-id", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-modal-id", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // PER-CARD-05 / El contenedor display preserva la
    // jerarquía data-* que el JS vigente espera:
    // display > card + display-text + empty.
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Editable_RendersDisplayContainerWithCardAndDisplayTextAndEmptyChildren()
    {
        var personaId = Guid.NewGuid();
        var query = BuildQuery(mode: "editable", personaId: personaId);

        await using var lease = await CreateAdminLeaseAsync();
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // El contenedor display está presente (binding JS lo lee por id).
        Assert.Contains("data-usuario-persona-display", content, StringComparison.OrdinalIgnoreCase);
        // El display-text con el nombre completo.
        Assert.Contains("data-usuario-persona-display-text", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("García, Ana", content, StringComparison.OrdinalIgnoreCase);
        // El contenedor empty (alternativa cuando NO hay persona seleccionada).
        Assert.Contains("data-usuario-persona-empty", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Buscar Persona", content, StringComparison.OrdinalIgnoreCase);
        // El hidden editable con el input name parametrizable.
        Assert.Contains("data-usuario-persona-display-input", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // PER-CARD-08 / PersonaDto parcial: sin Email/Teléfono
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ReadonlyWithPersonaSinContacto_OmiteFilasVaciasSinTextoLiteralNull()
    {
        var personaId = Guid.NewGuid();
        // Sin email ni telefono ni numeroDocumento; sólo nombres + legajo + tipo.
        var query = $"mode=readonly&personaId={personaId:D}&legajo=L-0001&nombres=Ana&apellidos=García&email=&tipoDocCodigo=DNI&numeroDocumento=&telefono=&isActive=true";

        await using var lease = await CreateAdminLeaseAsync();
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("García, Ana", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DNI", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("L-0001", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("null", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("undefined", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // PER-CARD-10 / Readonly envuelve Nombre en <a>
    // cuando PersonaDetailUrl está presente.
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ReadonlyWithPersonaDetailUrl_WrapsNombreInAnchor()
    {
        var personaId = Guid.NewGuid();
        var query = BuildQuery(mode: "readonly", personaId: personaId) + $"&personaDetailUrl=/personas/detalle/{personaId:D}";

        await using var lease = await CreateAdminLeaseAsync();
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            $"href=\"/personas/detalle/{personaId:D}\"",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("García, Ana", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // PER-CARD-10 / Readonly sin PersonaDetailUrl → texto plano
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ReadonlyWithoutPersonaDetailUrl_RendersPlainTextNombre()
    {
        var personaId = Guid.NewGuid();
        var query = BuildQuery(mode: "readonly", personaId: personaId);

        await using var lease = await CreateAdminLeaseAsync();
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("García, Ana", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            $"href=\"/personas/detalle/{personaId:D}\"",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // PER-CARD-10 / Fallback Display + FallbackUrl cuando
    // PersonaDto es null.
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ReadonlyWithFallbackDisplayAndUrl_RendersAnchorWithFallbackText()
    {
        var personaId = Guid.NewGuid();
        var query = "mode=readonly&fallbackDisplay=García, Ana&fallbackUrl=/personas/detalle/" + personaId.ToString("D");

        await using var lease = await CreateAdminLeaseAsync();
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            $"href=\"/personas/detalle/{personaId:D}\"",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("García, Ana", content, StringComparison.OrdinalIgnoreCase);
        // El fallback no debe emitir botones mutables (es readonly).
        Assert.DoesNotContain("data-usuario-persona-quitar", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-buscar", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadonlyWithFallbackDisplayOnly_RendersPlainFallbackText()
    {
        var query = "mode=readonly&fallbackDisplay=García, Ana";

        await using var lease = await CreateAdminLeaseAsync();
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("García, Ana", content, StringComparison.OrdinalIgnoreCase);
        // Sin FallbackUrl, no debe haber anchor envolviendo el texto.
        Assert.DoesNotContain("href=\"/personas/detalle/", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // El helper se invoca desde la partial
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ReadonlyWithPersona_UsesFormatDocumentoHelper()
    {
        // El documento "DNI 30123478" es distinto al del BuildQuery — verifica
        // que la partial delega en PersonaFormatHelper.FormatDocumento.
        var personaId = Guid.NewGuid();
        var query = "mode=readonly"
            + $"&personaId={personaId:D}"
            + "&legajo=L-9999"
            + "&nombres=Bea"
            + "&apellidos=Suárez"
            + "&email=bea@example.com"
            + "&tipoDocCodigo=PAS"
            + "&numeroDocumento=30123478"
            + "&telefono=%2B541166660000"
            + "&isActive=true";

        await using var lease = await CreateAdminLeaseAsync();
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Espacio como separador (PERFMT-01).
        Assert.Contains("PAS 30123478", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("L-9999", content, StringComparison.OrdinalIgnoreCase);
        // Legajo visible como fila separada.
        Assert.Contains("Legajo", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Slice 2 / PER-CARD-01 extensión: editable + PersonaDto null +
    // FallbackDisplay emite una card editable fallback con
    // Quitar/Cambiar. Caso ejercido por Usuarios/_Form cuando el
    // fetch del API falla pero el UsuarioDto tiene PersonaId
    // asignado (Input.PersonaId.HasValue = true).
    // ──────────────────────────────────────────────

    [Fact]
    public async Task EditableWithPersonaNullAndFallbackDisplay_EmitsEditableFallbackCardWithQuitarCambiar()
    {
        var query = "mode=editable&fallbackDisplay=García%2C%20Ana";

        await using var lease = await CreateAdminLeaseAsync();
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Card y display-text visibles con el texto fallback.
        Assert.Contains("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-display-text", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("García, Ana", content, StringComparison.OrdinalIgnoreCase);
        // Acciones mutables Quitar/Cambiar con binding al modal.
        Assert.Contains("data-usuario-persona-quitar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Quitar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-buscar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-bs-toggle=\"modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-bs-target=\"#usuario-persona-buscador-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cambiar", content, StringComparison.OrdinalIgnoreCase);
        // El empty state se emite pero con hidden="hidden" porque la
        // fallback card ya cubre la presentación visible. El JS lo
        // necesita presente para `empty.hidden = false/true` cuando
        // el usuario pulsa Quitar.
        Assert.Contains("data-usuario-persona-empty", content, StringComparison.OrdinalIgnoreCase);
        // El empty debe estar oculto vía atributo hidden cuando la
        // fallback card ocupa el espacio visible.
        var emptyMatch = System.Text.RegularExpressions.Regex.Match(
            content, @"<div\s+data-usuario-persona-empty[^>]*hidden=""hidden""");
        Assert.True(
            emptyMatch.Success,
            "El empty state editable debe emitirse con hidden=\"hidden\" cuando la fallback card ocupa la presentación visible.");
        Assert.Contains("Buscar Persona", content, StringComparison.OrdinalIgnoreCase);
        // El hidden que el JS lee/escribe sigue presente con el valor fallback.
        Assert.Contains("data-usuario-persona-display-input", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"García, Ana\"", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Slice 2 / PER-CARD-01: editable + PersonaDto null + sin
    // FallbackDisplay emite el empty state con Buscar Persona.
    // Caso del flujo "crear nuevo usuario sin persona asignada".
    // ──────────────────────────────────────────────

    [Fact]
    public async Task EditableWithPersonaNullAndNoFallback_EmitsEmptyStateWithBuscarPersona()
    {
        var query = "mode=editable";

        await using var lease = await CreateAdminLeaseAsync();
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-usuario-persona-empty", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Buscar Persona", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-buscar", content, StringComparison.OrdinalIgnoreCase);
        // Sin card ni Quitar (no hay fallback ni persona).
        Assert.DoesNotContain("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-quitar", content, StringComparison.OrdinalIgnoreCase);
        // Sin hidden editable porque no hay nada que sincronizar.
        Assert.DoesNotContain("data-usuario-persona-display-input", content, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildQuery(
        string mode,
        Guid personaId,
        bool isActive = true)
    {
        var legajo = "L-7777";
        var nombres = "Ana";
        var apellidos = "García";
        var email = "ana.garcia@example.com";
        var tipoDoc = "DNI";
        var numeroDoc = "30123456";
        var telefono = "+54 11 5555-0000";
        return $"mode={mode}"
            + $"&personaId={personaId:D}"
            + $"&legajo={Uri.EscapeDataString(legajo)}"
            + $"&nombres={Uri.EscapeDataString(nombres)}"
            + $"&apellidos={Uri.EscapeDataString(apellidos)}"
            + $"&email={Uri.EscapeDataString(email)}"
            + $"&tipoDocCodigo={Uri.EscapeDataString(tipoDoc)}"
            + $"&numeroDocumento={Uri.EscapeDataString(numeroDoc)}"
            + $"&telefono={Uri.EscapeDataString(telefono)}"
            + $"&isActive={isActive.ToString().ToLowerInvariant()}";
    }
}