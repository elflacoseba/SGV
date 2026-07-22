using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Habilidades;
using Xunit;
using RecordingHandler = SGV.Tests.Web._Shared.HttpClientExceptionScenarios.RecordingHandler;

namespace SGV.Tests.Web.Habilidad;

/// <summary>
/// Unit tests for <see cref="HabilidadApiClient.GetPersonasAsync"/> — the
/// typed client that calls <c>GET /api/v1/skills/{skillId}/personas</c>.
/// Mirrors the structure of the <c>GetCargosAsync</c> block in
/// <see cref="HabilidadApiClientTests"/>: URI building, query-param
/// ordering/escaping, JSON deserialization, and transport failure
/// propagation.
///
/// PR agrega-navegacion-personas-habilidades / PR C — frontend subreverso
/// (task C.1 / C.2). Coverage specs:
///   - REQ-HLD-NEW (button Persona in Habilidades/Index drives navigation)
///   - REQ-HM-NEW-PAGE (Habilidades/Personas page depends on the typed
///     client to feed the readonly grid)
/// </summary>
public sealed class HabilidadApiClientGetPersonasTests
{
    [Fact]
    public async Task GetPersonasAsync_Http200_BuildsExpectedUriAndReturnsPagedResult()
    {
        // Happy path: el cliente arma la URI
        //   /api/v1/skills/{skillId}/personas?page=1&pageSize=20
        // (status=activas se omite por convención del módulo — ver
        // HabilidadApiClient.GetCargosAsync), deserializa el envelope
        // PagedResult<SkillPersonaDetailDto> y devuelve los items esperados.
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var nivel = new NivelHabilidadDto(nivelId, "AVZ", "Avanzado", 3, 3);
        var personaId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId,
            Legajo: "L-100",
            Nombres: "Juan",
            Apellidos: "Pérez",
            Email: "juan@test",
            TipoDocumentoId: null,
            TipoDocumentoCodigo: null,
            TipoDocumentoNombre: null,
            NumeroDocumento: "12345678",
            Telefono: null,
            IsActive: true);
        var item = new SkillPersonaDetailDto(persona, nivel)
        {
            PersonaId = personaId,
            HabilidadId = skillId,
            NivelHabilidadId = nivelId,
        };
        var payload = new PersonaHabilidadesPageResult(
            new[] { item },
            Page: 1,
            PageSize: 20,
            Total: 1,
            Sort: "apellidos_asc",
            Segmento: PersonaSegmentoListado.Activas);

        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

        var result = await client.GetPersonasAsync(
            skillId,
            new HabilidadPersonasListQuery(1, 20, null, null, PersonaSegmentoListado.Activas));

        Assert.Single(result.Items);
        Assert.Equal(1, result.Total);
        Assert.Equal("Juan", result.Items[0].Persona.Nombres);
        Assert.Equal("Pérez", result.Items[0].Persona.Apellidos);
        Assert.Equal(skillId, result.Items[0].HabilidadId);
        Assert.Equal(nivelId, result.Items[0].NivelHabilidadId);

        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/skills/{skillId}/personas", handler.LastRequest?.RequestUri?.AbsolutePath);
        // status=activas se omite por convención del módulo.
        Assert.Equal("page=1&pageSize=20", handler.LastRequest?.RequestUri?.Query.TrimStart('?'));
    }

    [Fact]
    public async Task GetPersonasAsync_WithSearchSortAndStatus_AppendsAllQueryParamsInExpectedOrder()
    {
        // El URI building es StringBuilder con append en orden
        // page → pageSize → search → sort → status. Validar ese orden y el
        // escape de search/sort es crítico porque un cambio en el orden
        // podría romper contratos de cache downstream o WAFs.
        var skillId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK,
            new PersonaHabilidadesPageResult(
                Array.Empty<SkillPersonaDetailDto>(),
                Page: 2,
                PageSize: 5,
                Total: 0,
                Sort: "apellidos_desc",
                Segmento: PersonaSegmentoListado.Eliminadas)));
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

        await client.GetPersonasAsync(
            skillId,
            new HabilidadPersonasListQuery(
                Page: 2,
                PageSize: 5,
                Search: "pé & co",
                Sort: "apellidos_desc",
                Segmento: PersonaSegmentoListado.Eliminadas));

        var query = handler.LastRequest?.RequestUri?.Query.TrimStart('?');
        Assert.Equal(
            "page=2&pageSize=5&search=p%C3%A9%20%26%20co&sort=apellidos_desc&status=eliminadas",
            query);
    }

    [Fact]
    public async Task GetPersonasAsync_Http500_PropagatesHttpRequestException()
    {
        // EnsureSuccessStatusCode → cualquier 4xx/5xx no manejado
        // explícitamente se traduce a HttpRequestException. El PageModel
        // traduce esa excepción al estado recuperable
        // (HabilidadesPersonasModel.IsRecoverable = true).
        var skillId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("down", System.Text.Encoding.UTF8, "text/plain"),
        });
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetPersonasAsync(
                skillId,
                new HabilidadPersonasListQuery(1, 20, null, null, PersonaSegmentoListado.Activas)));
    }

    [Fact]
    public async Task GetPersonasAsync_Http404_PropagatesHttpRequestException()
    {
        // 404 → HttpRequestException (parity with GetCargosAsync). El
        // PageModel lo traduce al estado recuperable: la habilidad padre no
        // existe o ya no está disponible, así que la grilla no se renderiza
        // y se muestra "La habilidad solicitada no está disponible."
        var skillId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("not-found", System.Text.Encoding.UTF8, "text/plain"),
        });
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetPersonasAsync(
                skillId,
                new HabilidadPersonasListQuery(1, 20, null, null, PersonaSegmentoListado.Activas)));
    }

    [Fact]
    public async Task GetPersonasAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest()
    {
        // Cancelación cooperativa: pre-cancelar el token NO debe iniciar el
        // envío HTTP. Cobertura del contrato de transporte del cliente.
        var handler = new RecordingHandler();
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetPersonasAsync(
                Guid.NewGuid(),
                new HabilidadPersonasListQuery(1, 20, null, null, PersonaSegmentoListado.Activas),
                new CancellationToken(canceled: true)));

        Assert.Null(handler.LastRequest);
    }

    private static HttpClient NewHttpClient(HttpMessageHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };

    private static ILogger<HabilidadApiClient> NullLogger() =>
        Microsoft.Extensions.Logging.Abstractions.NullLogger<HabilidadApiClient>.Instance;

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T payload)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(payload)
        };
        return response;
    }
}