using System.Net;
using System.Net.Http.Json;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Vacantes;
using Xunit;
using RecordingHandler = SGV.Tests.Web._Shared.HttpClientExceptionScenarios.RecordingHandler;

namespace SGV.Tests.Web.Vacantes;

/// <summary>
/// Unit tests for the typed <see cref="VacanteApiClient.ListarPuestosAsync"/>
/// surface added in issue #235. Cubre la ruta HTTP, el contrato de cancelación
/// y la propagación nativa de excepciones de transporte. Espejo minimalista de
/// <c>PuestosApiClientTests</c> ajustado al cliente de Vacantes.
/// </summary>
public class VacanteApiClientListarPuestosTests
{
    [Fact]
    public async Task ListarPuestosAsync_Http200WithArray_ReturnsDtosAndHitsGetRoute()
    {
        var id = Guid.NewGuid();
        var payload = new[] { BuildDto(id, "P-001", "Analista") };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new VacanteApiClient(NewHttpClient(handler));

        var result = await client.ListarPuestosAsync();

        Assert.Single(result);
        Assert.Equal(id, result[0].Id);
        Assert.Equal("Analista", result[0].Nombre);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/puestos", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task ListarPuestosAsync_Http500WithNonJsonBody_PropagatesHttpRequestException()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("not-json", System.Text.Encoding.UTF8, "text/plain")
        };
        var handler = new RecordingHandler(_ => response);
        var client = new VacanteApiClient(NewHttpClient(handler));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.ListarPuestosAsync());
    }

    [Fact]
    public async Task ListarPuestosAsync_PreCanceledToken_PropagatesOperationCanceledException()
    {
        var handler = new RecordingHandler();
        var client = new VacanteApiClient(NewHttpClient(handler));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.ListarPuestosAsync(new CancellationToken(canceled: true)));

        Assert.Null(handler.LastRequest);
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task ListarPuestosAsync_TransportFails_PropagatesNativeException(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        HttpMessageHandler handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new VacanteApiClient(NewHttpClient(handler));

        await Assert.ThrowsAsync(
            expectedExceptionType,
            async () => await client.ListarPuestosAsync());
    }

    private static PuestoDto BuildDto(Guid id, string codigo, string nombre) =>
        new(id, codigo, nombre, null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null);

    private static HttpClient NewHttpClient(HttpMessageHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T payload) =>
        new(status) { Content = JsonContent.Create(payload) };
}
