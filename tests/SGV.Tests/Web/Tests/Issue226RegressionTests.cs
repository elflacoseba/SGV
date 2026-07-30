using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using SGV.Tests.Web.Collections;
using Xunit;

namespace SGV.Tests.Web.Tests;

/// <summary>
/// Tests de regresión para la issue #226: "No abre el popup Buscar Persona al
/// crear un Usuario o una Ocupación".
///
/// Hipótesis bajo verificación: en
/// <c>src/SGV.Web/Pages/Shared/Partials/_PersonaCard.cshtml</c> línea 242,
/// Razor evalúa la expresión
/// <c>hidden="@(hasPersona || isEditableFallback ? "hidden" : null)"</c>.
/// En el caso 6 (editable + PersonaDto null + sin FallbackDisplay, típico del
/// flujo Create), el valor resuelto es <c>null</c>. Si Razor emitiera
/// <c>hidden=""</c> o <c>hidden="null"</c> como atributo HTML, el div quedaría
/// oculto vía CSS y el botón "Buscar Persona" no podría disparar el modal,
/// bloqueando la apertura del popup en Usuarios/Create y Ocupaciones/Create.
///
/// El test existente
/// <c>EditableWithPersonaNullAndNoFallback_EmitsEmptyStateWithBuscarPersona</c>
/// (PersonaCardPartialTests.cs líneas 457-475) sólo verifica que el atributo
/// <c>data-usuario-persona-empty</c> y el texto "Buscar Persona" estén
/// presentes — NO blinda contra la presencia del atributo <c>hidden</c>.
///
/// Esta suite cubre el contrato negativo explícito: el div debe emitirse SIN
/// atributo <c>hidden</c> en el caso 6, para garantizar visibilidad al usuario.
/// </summary>
[Collection("WebIntegration")]
public sealed class Issue226RegressionTests
{
    private readonly WebIntegrationFixture _fixture;

    public Issue226RegressionTests(WebIntegrationFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Caso 6 puro (editable + PersonaDto null + sin FallbackDisplay):
    /// el <c>&lt;div data-usuario-persona-empty&gt;</c> NO debe tener atributo
    /// <c>hidden</c>, en ninguna variante (sin atributo, vacío, "null", "true",
    /// "hidden", etc.). Si lo tiene, el botón "Buscar Persona" queda invisible
    /// y la issue #226 se reproduce.
    /// </summary>
    [Fact]
    public async Task EditableWithPersonaNullAndNoFallback_NoHiddenAttributeOnEmptyDiv()
    {
        var query = "mode=editable";

        await using var lease = await _fixture.CreateAuthOnlyLeaseAsync(adminRole: true);
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var emptyDivMatch = Regex.Match(
            content,
            @"<div\s+data-usuario-persona-empty\b[^>]*?>",
            RegexOptions.IgnoreCase);

        Assert.True(
            emptyDivMatch.Success,
            $"No se encontró el <div data-usuario-persona-empty> en el HTML renderizado.\n\nHTML:\n{content}");

        var emptyDivTag = emptyDivMatch.Value;

        // El atributo hidden puede aparecer como `hidden` (boolean),
        // `hidden=""`, `hidden="hidden"`, `hidden="true"`, o — caso bug —
        // `hidden="null"`. Blindamos contra TODA presencia de `hidden` en
        // el tag del div. Si Razor no filtra el null correctamente, aquí
        // fallará y revelará la regresión de la issue #226.
        Assert.False(
            Regex.IsMatch(emptyDivTag, @"\bhidden(\s*=\s*(""(?:[^""]*)""|'[^']*'|[^\s>]*))?", RegexOptions.IgnoreCase),
            $"El div data-usuario-persona-empty contiene un atributo 'hidden' en el caso 6 " +
            $"(editable + PersonaDto null + sin FallbackDisplay). Esto oculta el botón " +
            $"\"Buscar Persona\" y bloquea la apertura del modal en Usuarios/Create y " +
            $"Ocupaciones/Create (issue #226).\n\n" +
            $"Tag emitido por Razor:\n{emptyDivTag}\n\n" +
            $"HTML renderizado completo:\n{content}");
    }
}
