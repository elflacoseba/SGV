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

    [Fact]
    public async Task PersonaBuscadorModal_ConsultaSameOrigin_UsaClienteTipadoDePersonas()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var response = await lease.Client.GetAsync(
            "/api/v1/personas/consulta?search=garcia&soloSinUsuario=true&p=2&pageSize=25");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.Single(personaApiClient.QueryCalls);
        Assert.Equal("garcia", query.Search);
        Assert.Equal(2, query.Page);
        Assert.Equal(25, query.PageSize);
        Assert.True(query.SoloSinUsuario);
    }

    [Fact]
    public async Task BFF_BuscarConSearchDe200Caracteres_ReenviaAlClienteTipado()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var search = new string('a', 200);
        var response = await lease.Client.GetAsync(
            $"/api/v1/personas/consulta?p=1&pageSize=10&search={Uri.EscapeDataString(search)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(personaApiClient.QueryCalls);
        var query = personaApiClient.QueryCalls[0];
        Assert.NotNull(query.Search);
        Assert.Equal(200, query.Search!.Length);
    }

    [Fact]
    public async Task BFF_BuscarConSearchDe201Caracteres_Responde400YNoLlamaCliente()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var search = new string('a', 201);
        var response = await lease.Client.GetAsync(
            $"/api/v1/personas/consulta?p=1&pageSize=10&search={Uri.EscapeDataString(search)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(personaApiClient.QueryCalls);
        var detail = await response.Content.ReadAsStringAsync();
        Assert.Contains("200", detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BFF_BuscarConSortEmailDesc_PropagaAlClienteTipado()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var response = await lease.Client.GetAsync(
            "/api/v1/personas/consulta?p=1&pageSize=10&sort=email_desc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(personaApiClient.QueryCalls);
        Assert.Equal("email_desc", personaApiClient.QueryCalls[0].Sort);
    }

    [Fact]
    public async Task BFF_BuscarConSortDocumentoAsc_Responde400YNoLlamaCliente()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var response = await lease.Client.GetAsync(
            "/api/v1/personas/consulta?p=1&pageSize=10&sort=documento_asc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(personaApiClient.QueryCalls);
        var detail = await response.Content.ReadAsStringAsync();
        Assert.Contains("apellidos_asc", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("apellidos_desc", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nombres_asc", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nombres_desc", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("legajo_asc", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("legajo_desc", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("email_asc", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("email_desc", detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BFF_BuscarConSortInvalido_Responde400YNoLlamaCliente()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var response = await lease.Client.GetAsync(
            "/api/v1/personas/consulta?p=1&pageSize=10&sort=hack");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(personaApiClient.QueryCalls);
    }

    [Fact]
    public async Task BFF_BuscarConSegmentoEliminadas_PropagaAlClienteTipado()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var response = await lease.Client.GetAsync(
            "/api/v1/personas/consulta?p=1&pageSize=10&segmento=eliminadas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(personaApiClient.QueryCalls);
        Assert.Equal(
            SGV.Contracts.Personas.Consultas.Dtos.PersonaSegmentoListado.Eliminadas,
            personaApiClient.QueryCalls[0].Segmento);
    }

    [Fact]
    public async Task BFF_BuscarConSegmentoInvalido_Responde400YNoLlamaCliente()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var response = await lease.Client.GetAsync(
            "/api/v1/personas/consulta?p=1&pageSize=10&segmento=todas");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(personaApiClient.QueryCalls);
        var detail = await response.Content.ReadAsStringAsync();
        Assert.Contains("activas", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("eliminadas", detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BFF_BuscarSinSortNiSegmento_AplicaDefaultsBackCompat()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var response = await lease.Client.GetAsync(
            "/api/v1/personas/consulta?p=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(personaApiClient.QueryCalls);
        var query = personaApiClient.QueryCalls[0];
        Assert.Equal("apellidos_asc", query.Sort);
        Assert.Equal(
            SGV.Contracts.Personas.Consultas.Dtos.PersonaSegmentoListado.Activas,
            query.Segmento);
    }

    private Task<WebClientLease> CreateLeaseAsync(FakePersonaApiClient? personaApiClient = null)
        => _fixture.CreateUsuarioLeaseAsync(
            new FakeUsuarioApiClient(),
            personaApiClient ?? new FakePersonaApiClient(),
            adminRole: true);
}
