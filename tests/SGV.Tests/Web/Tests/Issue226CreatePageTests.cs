using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Ocupaciones;
using SGV.Tests.Web.Persona;
using SGV.Tests.Web.Puesto;
using SGV.Tests.Web.Usuario;
using SGV.Web.Integration.Ocupaciones;
using SGV.Web.Integration.Personas;
using SGV.Web.Integration.Usuarios;
using Xunit;

namespace SGV.Tests.Web.Tests;

/// <summary>
/// Inspección empírica del HTML renderizado por las páginas Create de
/// Usuario y Ocupación para la issue #226: "No abre el popup Buscar Persona
/// al crear un Usuario o una Ocupación".
///
/// Esta suite NO reemplaza al coverage existente en
/// <see cref="Issue226RegressionTests"/> (que valida el partial directamente
/// contra <c>/tests/persona-card-harness?mode=editable</c>). Complementa
/// ese coverage ejecutando el flow completo: GET autenticado como admin a
/// las páginas reales de Create, captura del HTML renderizado y verificación
/// de los seis puntos críticos para que el modal abra:
///
///   1. El modal <c>*-persona-buscador-modal</c> existe en el HTML.
///   2. El botón "Buscar Persona" existe, está DENTRO del
///      <c>&lt;div data-usuario-persona-empty&gt;</c> y lleva los atributos
///      Bootstrap <c>data-bs-toggle="modal"</c> + <c>data-bs-target</c>
///      apuntando al modal correspondiente.
///   3. El <c>&lt;div data-usuario-persona-empty&gt;</c> NO tiene atributo
///      <c>hidden</c> en el caso 6 (editable + PersonaDto null + sin
///      FallbackDisplay). Si lo tiene, el botón queda invisible vía CSS
///      y el popup no abre.
///   4. El input hidden <c>Input.PersonaId</c> existe en el form (sin él
///      el JS no puede escribir el id seleccionado).
///   5. El script <c>/js/pages/usuario-persona-buscador.js</c> se carga
///      vía <c>@section scripts</c> (sin él, el modal no se enlaza al
///      display container).
///   6. El bundle <c>/js/vendors.min.js</c> se carga en el footer (sin
///      Bootstrap inicializado, <c>data-bs-toggle="modal"</c> no abre
///      ningún popup).
///
/// En cualquier falla se imprime el HTML completo del fragmento relevante
/// para inspección manual.
/// </summary>
[Collection("WebIntegration")]
public sealed class Issue226CreatePageTests
{
    private readonly WebIntegrationFixture _fixture;

    public Issue226CreatePageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    // ────────────────────────────────────────────────────────────────
    // Test 1: GET /seguridad/usuarios/crear
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_UsuarioCrear_RenderizaModalYEmptyStateSinHidden()
    {
        var persona = new PersonaDto(
            Id: Guid.NewGuid(),
            Legajo: "L-001",
            Nombres: "Ana",
            Apellidos: "García",
            Email: null,
            TipoDocumentoId: null,
            TipoDocumentoCodigo: null,
            TipoDocumentoNombre: null,
            NumeroDocumento: null,
            Telefono: null,
            IsActive: true);
        var personaClient = FakePersonaApiClient.WithPersonaList(persona);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            new FakeUsuarioApiClient(),
            personaClient,
            adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AssertRenderBuscadorPersona(
            content,
            pageKind: "Usuarios/Create",
            modalId: "usuario-persona-buscador-modal",
            displayContainerId: "usuario-persona-display");
    }

    // ────────────────────────────────────────────────────────────────
    // Test 2: GET /organizacion/ocupaciones/crear
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_OcupacionCrear_RenderizaModalYEmptyStateSinHidden()
    {
        var persona = new PersonaDto(
            Id: Guid.NewGuid(),
            Legajo: "L-001",
            Nombres: "Ana",
            Apellidos: "García",
            Email: null,
            TipoDocumentoId: null,
            TipoDocumentoCodigo: null,
            TipoDocumentoNombre: null,
            NumeroDocumento: null,
            Telefono: null,
            IsActive: true);
        var unidadId = Guid.NewGuid();
        var cargoId = Guid.NewGuid();
        var puesto = new PuestoDto(
            Id: Guid.NewGuid(),
            Codigo: "P-001",
            Nombre: "Analista",
            Descripcion: null,
            UnidadOrganizativaId: unidadId,
            UnidadOrganizativaNombre: "Ventas",
            CargoId: cargoId,
            CargoNombre: "Vendedor",
            PuestoSuperiorId: null);
        var personaClient = FakePersonaApiClient.WithPersonaList(persona);
        var puestosClient = FakePuestosApiClient.WithPuestoList(puesto);

        await using var lease = await _fixture.CreateOcupacionFormLeaseAsync(
            new FakeOcupacionApiClient(),
            personaClient,
            puestosClient,
            adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AssertRenderBuscadorPersona(
            content,
            pageKind: "Ocupaciones/Create",
            modalId: "ocupacion-persona-buscador-modal",
            displayContainerId: "ocupacion-persona-display");
    }

    // ────────────────────────────────────────────────────────────────
    // Helper: aplica las 6 verificaciones de la issue #226 sobre el HTML
    // capturado. En cualquier falla, incluye el HTML completo y los
    // fragmentos relevantes en el mensaje para inspección.
    // ────────────────────────────────────────────────────────────────

    private static void AssertRenderBuscadorPersona(
        string content,
        string pageKind,
        string modalId,
        string displayContainerId)
    {
        // ── Dump diagnóstico de los fragmentos relevantes. Se imprime SIEMPRE
        // (no sólo en failure) para que la salida del test sirva como
        // evidencia visual de la issue #226. ──
        Console.WriteLine($"=== {pageKind} · fragmentos relevantes ===");

        // 1. Modal presente en el HTML
        var modalPattern =
            $@"<div\s+class=""modal\s+fade""\s+id=""{Regex.Escape(modalId)}""";
        var modalMatch = Regex.Match(content, modalPattern, RegexOptions.IgnoreCase);
        Assert.True(
            modalMatch.Success,
            $"[{pageKind}] No se encontró el <div id=\"{modalId}\" class=\"modal fade\">.\n\n"
            + $"HTML completo:\n{content}");

        // 2/3. Empty state presente y SIN atributo 'hidden' en el tag del div
        var emptyDivMatch = Regex.Match(
            content,
            @"<div\s+data-usuario-persona-empty\b[^>]*?>",
            RegexOptions.IgnoreCase);
        Assert.True(
            emptyDivMatch.Success,
            $"[{pageKind}] No se encontró el <div data-usuario-persona-empty>.\n\n"
            + $"HTML completo:\n{content}");

        var emptyDivTag = emptyDivMatch.Value;
        Console.WriteLine($"[empty state div] {emptyDivTag}");

        // Display container (caso 6: contenedor presente, contenido vacío)
        var displayMatch = Regex.Match(
            content,
            $@"(<div\s+id=""{Regex.Escape(displayContainerId)}""[^>]*?>[\s\S]*?</div>)",
            RegexOptions.IgnoreCase);
        if (displayMatch.Success)
        {
            Console.WriteLine($"[display container] {displayMatch.Groups[1].Value.Trim()}");
        }

        Assert.False(
            Regex.IsMatch(
                emptyDivTag,
                @"\bhidden(\s*=\s*(""(?:[^""]*)""|'[^']*'|[^\s>]*))?",
                RegexOptions.IgnoreCase),
            $"[{pageKind}] El <div data-usuario-persona-empty> contiene el atributo "
            + $"'hidden' en el tag emitido por Razor. Esto oculta el botón "
            + $"\"Buscar Persona\" vía CSS y bloquea la apertura del modal "
            + $"(issue #226).\n\n"
            + $"Tag emitido por Razor:\n{emptyDivTag}\n\n"
            + $"HTML completo:\n{content}");

        // El display container debe existir (caso 6: contenedor vacío).
        var displayPattern = $@"<div\s+id=""{Regex.Escape(displayContainerId)}""";
        Assert.True(
            Regex.IsMatch(content, displayPattern, RegexOptions.IgnoreCase),
            $"[{pageKind}] No se encontró el contenedor <div id=\"{displayContainerId}\">.\n\n"
            + $"HTML completo:\n{content}");

        // 2. Botón "Buscar Persona" DENTRO del empty state div, con los
        // atributos Bootstrap correctos.
        var emptyDivContent = ExtractBalancedDivContent(content, emptyDivMatch.Index);
        Assert.True(
            !string.IsNullOrEmpty(emptyDivContent),
            $"[{pageKind}] No se pudo extraer el contenido del empty state div.\n\n"
            + $"HTML completo:\n{content}");

        var btnMatch = Regex.Match(
            emptyDivContent,
            @"<button\b[^>]*?>[\s\S]*?Buscar Persona[\s\S]*?</button>",
            RegexOptions.IgnoreCase);
        Assert.True(
            btnMatch.Success,
            $"[{pageKind}] No se encontró el botón \"Buscar Persona\" dentro del "
            + $"<div data-usuario-persona-empty>.\n\n"
            + $"Empty state div content:\n{emptyDivContent}\n\n"
            + $"HTML completo:\n{content}");

        var btnTag = btnMatch.Value;
        Console.WriteLine($"[buscar persona button] {btnTag}");

        Assert.Contains(
            "data-bs-toggle=\"modal\"",
            btnTag,
            StringComparison.OrdinalIgnoreCase);

        // Issue #226: Bootstrap 5 trata `data-bs-target` como selector CSS
        // vía `SelectorEngine.getElementFromSelector(...)` (verificado en
        // `vendors.min.js` por la presencia de 10 ocurrencias). Sin `#`
        // inicial, `document.querySelector("<id>")` busca un elemento con
        // ese tag (no por id) y devuelve null → el modal no abre. El
        // prefijo `#` es OBLIGATORIO.
        Assert.True(
            Regex.IsMatch(
                btnTag,
                $@"data-bs-target\s*=\s*""\s*#{Regex.Escape(modalId)}\s*""",
                RegexOptions.IgnoreCase),
            $"[{pageKind}] El botón \"Buscar Persona\" no apunta correctamente al modal. "
            + $"Expected data-bs-target=\"#{modalId}\" (Bootstrap 5 requiere prefijo '#' "
            + $"porque trata el atributo como selector CSS, no como id). "
            + $"Sin '#' el modal no abre.\n\n"
            + $"Button tag emitido por Razor:\n{btnTag}\n\n"
            + $"HTML completo:\n{content}");

        // 4. Hidden input Input.PersonaId presente en el form.
        Assert.True(
            Regex.IsMatch(
                content,
                @"<input(?=[^>]*name=""Input\.PersonaId"")(?=[^>]*type=""hidden"")[^>]*>",
                RegexOptions.IgnoreCase),
            $"[{pageKind}] No se encontró el hidden input name=\"Input.PersonaId\" en el form.\n\n"
            + $"HTML completo:\n{content}");

        var personaIdInput = Regex.Match(
            content,
            @"<input(?=[^>]*name=""Input\.PersonaId"")(?=[^>]*type=""hidden"")[^>]*>",
            RegexOptions.IgnoreCase).Value;
        Console.WriteLine($"[hidden input PersonaId] {personaIdInput}");

        // 5. Script /js/pages/usuario-persona-buscador.js cargado.
        Assert.Contains(
            "<script src=\"/js/pages/usuario-persona-buscador.js\"></script>",
            content,
            StringComparison.OrdinalIgnoreCase);

        // 6. Bundle /js/vendors.min.js (Bootstrap) en el footer.
        Assert.Contains(
            "<script src=\"/js/vendors.min.js\"></script>",
            content,
            StringComparison.OrdinalIgnoreCase);

        Console.WriteLine(
            $"[script usuario-persona-buscador.js] presente en @section scripts");
        Console.WriteLine(
            $"[script vendors.min.js] presente en footer (Bootstrap inicializado)");
        Console.WriteLine(
            $"[modal root primer 240 chars] {modalMatch.Value.Substring(0, Math.Min(240, modalMatch.Value.Length))}…");
        Console.WriteLine();
    }

    /// <summary>
    /// Dado el HTML y el índice donde abre un <c>&lt;div ...&gt;</c>,
    /// devuelve el contenido desde ese índice hasta el <c>&lt;/div&gt;</c>
    /// de cierre balanceado (incluyendo ambos tags). Si el div no cierra
    /// balanceado devuelve <see cref="string.Empty"/>.
    /// </summary>
    private static string ExtractBalancedDivContent(string html, int startIndex)
    {
        var depth = 0;
        var i = startIndex;
        while (i < html.Length)
        {
            if (i + 4 <= html.Length && html.Substring(i, 4) == "<div")
            {
                depth++;
                i += 4;
            }
            else if (i + 5 <= html.Length && html.Substring(i, 5) == "</div")
            {
                depth--;
                i += 5;
                if (depth == 0)
                {
                    return html.Substring(startIndex, i - startIndex);
                }
            }
            else
            {
                i++;
            }
        }
        return string.Empty;
    }
}