using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// Unit tests for the typed <see cref="CargoApiClient"/>.
/// Covers HTTP translation, request paths, and the mapping of status codes
/// (including ProblemDetails parsing) to <see cref="CargoDeleteResult"/>.
/// </summary>
public class CargoApiClientTests
{
    [Fact]
    public async Task GetAllAsync_Http200WithPayload_ReturnsParsedDtosAndHitsListRoute()
    {
        var id = Guid.NewGuid();
        var payload = new[] { new CargoDto(id, "C-001", "Analista", null, Guid.NewGuid()) };
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.GetAllAsync();

        Assert.Single(result);
        Assert.Equal(id, result[0].Id);
        Assert.Equal("Analista", result[0].Nombre);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/cargos", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_Http200_ReturnsDtoAndHitsDetailRoute()
    {
        var id = Guid.NewGuid();
        var payload = new CargoDto(id, "C-002", "Líder", "Desc", Guid.NewGuid());
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal("Líder", result!.Nombre);
        Assert.Equal($"/api/v1/cargos/{id}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_Http404_ReturnsNullWithoutThrowing()
    {
        var handler = new StubHandler(_ => Json<object?>(HttpStatusCode.NotFound, null));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_Http204_ReturnsSuccessAndHitsDeleteRoute()
    {
        var id = Guid.NewGuid();
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteAsync(id);

        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Null(result.Code);
        Assert.Null(result.Message);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/cargos/{id}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task DeleteAsync_Http404WithProblemDetails_ReturnsFailedResultWithTitleAndDetail()
    {
        var id = Guid.NewGuid();
        var problem = new ProblemDetails { Title = "NotFound", Detail = "Cargo no disponible", Status = 404 };
        var handler = new StubHandler(_ => Json(HttpStatusCode.NotFound, problem));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteAsync(id);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal("NotFound", result.Code);
        Assert.Equal("Cargo no disponible", result.Message);
    }

    [Fact]
    public async Task DeleteAsync_Http409WithProblemDetails_ReturnsFailedResultWithConflictDetail()
    {
        var id = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Title = "CargoConPuestosActivos",
            Detail = "El cargo tiene puestos activos",
            Status = 409
        };
        var handler = new StubHandler(_ => Json(HttpStatusCode.Conflict, problem));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteAsync(id);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal("CargoConPuestosActivos", result.Code);
        Assert.Equal("El cargo tiene puestos activos", result.Message);
    }

    [Fact]
    public async Task DeleteAsync_Http500WithNonJsonBody_ReturnsFailedResultWithoutCrashing()
    {
        var id = Guid.NewGuid();
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("not-json", System.Text.Encoding.UTF8, "text/plain")
        };
        var handler = new StubHandler(_ => response);
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteAsync(id);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Null(result.Code);
        Assert.Null(result.Message);
    }

    private static HttpClient NewHttpClient(StubHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T payload)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(payload)
        };
        return response;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_responder(request));
        }
    }
}
