using SGV.Contracts.Personas.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Web.Helpers;

/// <summary>
/// Unit tests for <c>SGV.Web.Helpers.PersonaFormatHelper.FormatDocumento</c>.
/// Slice 1 / PR 1 of change <c>reusable-persona-card</c> (issue #219).
/// Helper centraliza la composición del texto de Documento
/// (<c>"TipoDocumento NumeroDocumento"</c>, separador espacio) que estaba
/// duplicada como <c>@functions FormatDocumento</c> en <c>Usuarios/Details</c>
/// y <c>Usuarios/_Form</c>, y como <c>@functions FormatearDocumento</c> en
/// <c>Ocupaciones/_Form</c>. Cubre PERFMT-01, PERFMT-02 y PERFMT-04.
/// </summary>
/// <remarks>
/// El separador es **espacio** (no colon) por enmienda al spec PERFMT-01
/// aplicada en design.md §Open Questions: el <c>&lt;dd&gt;Documento&lt;/dd&gt;</c>
/// server-side venía usando espacio y el cambio a colon introduce
/// regresión visual (PER-CARD-09). El colon que usa el JS
/// (<c>personaDisplay</c>) es otra display distinta y no se toca.
/// </remarks>
public sealed class PersonaFormatHelperTests
{
    // ──────────────────────────────────────────────
    // PERFMT-01 / Scenario: Documento completo
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatDocumento_BothTipoAndNumero_ReturnsJoinedBySpace()
    {
        var persona = BuildPersona(tipoCodigo: "DNI", numeroDocumento: "12345678");

        var result = SGV.Web.Helpers.PersonaFormatHelper.FormatDocumento(persona);

        Assert.Equal("DNI 12345678", result);
    }

    // ──────────────────────────────────────────────
    // PERFMT-01 / Scenario: Tipo ausente
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatDocumento_TipoAusente_RetornaSoloNumeroSinEspacioLider()
    {
        var persona = BuildPersona(tipoCodigo: null, numeroDocumento: "12345678");

        var result = SGV.Web.Helpers.PersonaFormatHelper.FormatDocumento(persona);

        Assert.Equal("12345678", result);
    }

    // ──────────────────────────────────────────────
    // PERFMT-01 / Scenario: Número ausente
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatDocumento_NumeroAusente_RetornaSoloTipoSinEspacioCola()
    {
        var persona = BuildPersona(tipoCodigo: "DNI", numeroDocumento: null);

        var result = SGV.Web.Helpers.PersonaFormatHelper.FormatDocumento(persona);

        Assert.Equal("DNI", result);
    }

    // ──────────────────────────────────────────────
    // PERFMT-01 / Scenario: PersonaDto nulo
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatDocumento_PersonaNula_RetornaEmptySinExcepcion()
    {
        var result = SGV.Web.Helpers.PersonaFormatHelper.FormatDocumento(null);

        Assert.Equal(string.Empty, result);
    }

    // ──────────────────────────────────────────────
    // PERFMT-01 / Triangulación: parametrizado cubre
    // todas las combinaciones null/empty/whitespace de
    // TipoDocumento y NumeroDocumento, más un caso
    // happy-path con valores atípicos (PAS + LC-9).
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(null, "12345678", "12345678")]                 // sin tipo → sólo número
    [InlineData("DNI", null, "DNI")]                           // sin número → sólo tipo
    [InlineData(null, null, "")]                               // ambos null → empty
    [InlineData("", "", "")]                                   // ambos empty → empty
    [InlineData("   ", "   ", "")]                             // ambos whitespace → empty
    [InlineData("DNI", "", "DNI")]                             // tipo + número vacío → sólo tipo
    [InlineData("", "12345678", "12345678")]                   // tipo vacío + número → sólo número
    [InlineData("DNI", "12345678", "DNI 12345678")]            // happy path → espacio
    [InlineData("PAS", "LC-9", "PAS LC-9")]                    // happy path atípico → espacio
    public void FormatDocumento_CombinacionesNullVacio_ReturnsEsperado(
        string? tipoCodigo,
        string? numeroDocumento,
        string expected)
    {
        var persona = BuildPersona(tipoCodigo: tipoCodigo, numeroDocumento: numeroDocumento);

        var result = SGV.Web.Helpers.PersonaFormatHelper.FormatDocumento(persona);

        Assert.Equal(expected, result);
    }

    // ──────────────────────────────────────────────
    // PERFMT-02 / Scenario: Sólo Legajo
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatDocumento_SinDocumentoConLegajo_RetornaLegajo()
    {
        var persona = BuildPersona(tipoCodigo: null, numeroDocumento: null, legajo: "0042");

        var result = SGV.Web.Helpers.PersonaFormatHelper.FormatDocumento(persona);

        Assert.Equal("0042", result);
    }

    // ──────────────────────────────────────────────
    // PERFMT-02 / Scenario: Sin documento ni Legajo
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatDocumento_SinDocumentoNiLegajo_RetornaEmpty()
    {
        var persona = BuildPersona(tipoCodigo: null, numeroDocumento: null, legajo: null);

        var result = SGV.Web.Helpers.PersonaFormatHelper.FormatDocumento(persona);

        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData(null, null, null, "")]                          // todo null → empty
    [InlineData("", "", "", "")]                                // todo vacío → empty
    [InlineData("   ", "   ", "   ", "")]                       // todo whitespace → empty
    [InlineData(null, null, "L-1", "L-1")]                      // sólo Legajo
    [InlineData("DNI", "12345678", null, "DNI 12345678")]       // documento gana sobre legajo
    [InlineData("DNI", null, "L-1", "DNI")]                     // sólo tipo gana sobre legajo
    [InlineData(null, "12345678", "L-1", "12345678")]           // sólo número gana sobre legajo
    public void FormatDocumento_LegajoVsDocumento_DocumentoGanaSiEstaPresente(
        string? tipoCodigo,
        string? numeroDocumento,
        string? legajo,
        string expected)
    {
        var persona = BuildPersona(tipoCodigo: tipoCodigo, numeroDocumento: numeroDocumento, legajo: legajo);

        var result = SGV.Web.Helpers.PersonaFormatHelper.FormatDocumento(persona);

        Assert.Equal(expected, result);
    }

    // ──────────────────────────────────────────────
    // PERFMT-04 / Scenario: namespace + ubicación
    // ──────────────────────────────────────────────

    [Fact]
    public void FormatDocumento_HelperEsPublicStaticEnNamespaceCorrecto()
    {
        // PERFMT-04: el método debe ser invocable desde Razor
        // (public static, namespace SGV.Web.Helpers).
        // Verificable vía reflection — sirve también de regresión
        // contra un futuro "move" del helper a SGV.Contracts.
        var method = typeof(SGV.Web.Helpers.PersonaFormatHelper).GetMethod(
            "FormatDocumento",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            binder: null,
            types: new[] { typeof(SGV.Contracts.Personas.Consultas.Dtos.PersonaDto) },
            modifiers: null);

        Assert.NotNull(method);
        Assert.Equal(typeof(string), method!.ReturnType);
    }

    private static PersonaDto BuildPersona(
        string? tipoCodigo,
        string? numeroDocumento,
        string? legajo = null) =>
        new(
            Id: Guid.NewGuid(),
            Legajo: legajo,
            Nombres: "Ana",
            Apellidos: "García",
            Email: null,
            TipoDocumentoId: tipoCodigo is null ? null : Guid.NewGuid(),
            TipoDocumentoCodigo: tipoCodigo,
            TipoDocumentoNombre: tipoCodigo is null ? null : "Documento Nacional de Identidad",
            NumeroDocumento: numeroDocumento,
            Telefono: null,
            IsActive: true);
}