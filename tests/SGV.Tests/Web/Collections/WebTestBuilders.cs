using System.Net.Http;
using System.Text.RegularExpressions;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Web.Collections;

/// <summary>
/// Centraliza el estado de módulo duplicado entre los fixtures web
/// (Cargo/Puesto/Habilidad/UO). Diseño: design.md §"Migración de estado de módulo".
/// PR 2b-0 sólo CREA este archivo; PR 2b-1..4 migran los call sites.
/// </summary>
public static class WebTestBuilders
{
    public static readonly Guid JuniorNivelId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SeniorNivelId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid SampleUnidadOrganizativaId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SampleCargoId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid SamplePuestoSuperiorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static CargoDto BuildCargoDto(string codigo, string nombre, string? descripcion, string? nivelNombre)
        => new(Guid.NewGuid(), codigo, nombre, descripcion, Guid.NewGuid(), nivelNombre);

    public static PuestoDto BuildPuestoDto(string codigo, string nombre, string? descripcion = null, Guid? puestoSuperiorId = null)
        => new(Guid.NewGuid(), codigo, nombre, descripcion, SampleUnidadOrganizativaId, "Ventas", SampleCargoId, "Vendedor", puestoSuperiorId);

    public static HabilidadDto BuildHabilidadDto(string codigo, string nombre, string? descripcion, string? categoria)
        => new(Guid.NewGuid(), codigo, nombre, descripcion, null, categoria);

    /// <summary>Handler mínimo que devuelve una respuesta preconfigurada. Antes había 7 copias.</summary>
    public sealed class RecordingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    /// <summary>Extrae el token antiforgery. Antes había 8 copias.</summary>
    public static async Task<string> ExtractAntiforgeryTokenAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(content, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        Assert.True(match.Success, "Antiforgery token was not rendered.");
        return match.Groups[1].Value;
    }
}

/// <summary>Markup helpers para tests de Habilidad. Antes 5 sitios en HabilidadWebTestFixture.</summary>
public static class HabilidadMarkup
{
    public static bool HasInputNamed(string content, string inputName)
        => Regex.IsMatch(content, $@"<input\b[^>]*\bname=""{Regex.Escape(inputName)}""[^>]*\/?>", RegexOptions.IgnoreCase);

    public static bool InputHasAttribute(string content, string inputName, string attributeName)
    {
        var match = Regex.Match(content, $@"<input\b[^>]*\bname=""{Regex.Escape(inputName)}""[^>]*\/?>", RegexOptions.IgnoreCase);
        if (!match.Success) return false;
        var tag = content.Substring(match.Index, match.Length);
        return Regex.IsMatch(tag, $@"\b{Regex.Escape(attributeName)}\b(=""[^""]*"")?", RegexOptions.IgnoreCase);
    }
}