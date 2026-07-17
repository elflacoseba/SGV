using System.Net;
using System.Web;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Persona;
using Xunit;

namespace SGV.Tests.Web.Usuario;

[Collection("WebIntegration")]
public sealed class PersonaBuscadorModalTests
{
    private readonly WebIntegrationFixture _fixture;

    public PersonaBuscadorModalTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PersonaBuscadorModal_TieneRoleDialogYAriaModal()
    {
        await using var lease = await CreateLeaseAsync();

        var response = await lease.Client.GetAsync("/seguridad/usuarios/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches(
            @"<div(?=[^>]*id=""usuario-persona-buscador-modal"")(?=[^>]*role=""dialog"")(?=[^>]*aria-modal=""true"")(?=[^>]*aria-labelledby=""usuario-persona-buscador-modal-label"")[^>]*>",
            content);
        Assert.Contains("id=\"usuario-persona-buscador-modal-label\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PersonaBuscadorModal_EstadoInicial_MuestraMensajeGuia()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var response = await lease.Client.GetAsync("/seguridad/usuarios/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ingresá un texto para buscar personas.", content, StringComparison.Ordinal);
        var availabilityQuery = Assert.Single(personaApiClient.QueryCalls);
        Assert.Equal(1, availabilityQuery.PageSize);
    }

    [Fact]
    public async Task PersonaBuscadorModal_EstadoEmpty_MuestraMensajeSinResultados()
    {
        await using var lease = await CreateLeaseAsync();

        var response = await lease.Client.GetAsync("/seguridad/usuarios/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se encontraron personas con ese criterio.", content, StringComparison.Ordinal);
        Assert.Contains("data-usuario-persona-estado-empty", content, StringComparison.OrdinalIgnoreCase);
    }

    private Task<WebClientLease> CreateLeaseAsync(FakePersonaApiClient? personaApiClient = null)
        => _fixture.CreateUsuarioLeaseAsync(
            new FakeUsuarioApiClient(),
            personaApiClient ?? new FakePersonaApiClient(),
            FakePersonaOptionsProvider.Empty(),
            adminRole: true);
}
