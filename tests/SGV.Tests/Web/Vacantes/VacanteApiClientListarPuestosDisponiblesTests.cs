using System.Net;
using System.Net.Http.Json;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Vacantes;
using Xunit;
using RecordingHandler = SGV.Tests.Web._Shared.HttpClientExceptionScenarios.RecordingHandler;

namespace SGV.Tests.Web.Vacantes;

/// <summary>
/// Unit tests for the typed <see cref="VacanteApiClient.ListarPuestosDisponiblesAsync"/>
/// surface added in change <c>vacante-crear-puestos-libres</c> (WU-4 / T-12).
/// Espejo estructural de <see cref="VacanteApiClientListarPuestosTests"/>:
/// misma ruta → "/api/v1/puestos/disponibles", misma política de cancelación
/// cooperativa, misma propagación nativa de excepciones de transporte.
/// </summary>
public class VacanteApiClientListarPuestosDisponiblesTests
{
    [Fact]
    public async Task ListarPuestosDisponiblesAsync_WhenApiReturnsOk_ReturnsDtoArray()
    {
        var id = Guid.NewGuid();
        var payload = new[] { BuildDto(id, "P-001", "Analista") };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new VacanteApiClient(NewHttpClient(handler));

        var result = await client.ListarPuestosDisponiblesAsync();

        Assert.Single(result);
        Assert.Equal(id, result[0].Id);
        Assert.Equal("Analista", result[0].Nombre);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/puestos/disponibles", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task ListarPuestosDisponiblesAsync_WhenApiReturns500_ThrowsHttpRequestException()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("not-json", System.Text.Encoding.UTF8, "text/plain")
        };
        var handler = new RecordingHandler(_ => response);
        var client = new VacanteApiClient(NewHttpClient(handler));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.ListarPuestosDisponiblesAsync());
    }

    [Fact]
    public async Task ListarPuestosDisponiblesAsync_WhenTokenPreCanceled_ThrowsOperationCanceledException()
    {
        var handler = new RecordingHandler();
        var client = new VacanteApiClient(NewHttpClient(handler));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.ListarPuestosDisponiblesAsync(new CancellationToken(canceled: true)));

        Assert.Null(handler.LastRequest);
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task ListarPuestosDisponiblesAsync_WhenHttpRequestFails_PropagatesTransportFailure(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        HttpMessageHandler handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new VacanteApiClient(NewHttpClient(handler));

        await Assert.ThrowsAsync(
            expectedExceptionType,
            async () => await client.ListarPuestosDisponiblesAsync());
    }

    private static PuestoDto BuildDto(Guid id, string codigo, string nombre) =>
        new(id, codigo, nombre, null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null);

    private static HttpClient NewHttpClient(HttpMessageHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T payload) =>
        new(status) { Content = JsonContent.Create(payload) };
}