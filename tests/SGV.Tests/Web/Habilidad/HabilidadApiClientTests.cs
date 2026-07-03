using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Habilidades.Comandos;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Habilidades;
using Xunit;
using HabilidadListQuery = SGV.Web.Integration.Habilidades.HabilidadListQuery;

namespace SGV.Tests.Web.Habilidad;

/// <summary>
/// Unit tests for the typed <see cref="HabilidadApiClient"/>.
/// Mirrors the pattern of <c>CargoApiClientTests</c>.
/// </summary>
public class HabilidadApiClientTests
{
    [Fact]
    public async Task GetAllAsync_Http200WithPayload_ReturnsParsedDtosAndHitsListRoute()
    {
        var id = Guid.NewGuid();
        var payload = new[] { new HabilidadDto(id, "H-001", "Liderazgo", "Desc", "Conductual") };
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new HabilidadApiClient(NewHttpClient(handler));

        var result = await client.GetAllAsync();

        Assert.Single(result);
        Assert.Equal(id, result[0].Id);
        Assert.Equal("Liderazgo", result[0].Nombre);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/skills", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_Http200_ReturnsDtoAndHitsDetailRoute()
    {
        var id = Guid.NewGuid();
        var payload = new HabilidadDto(id, "H-002", "Programación", null, "Técnica");
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new HabilidadApiClient(NewHttpClient(handler));

        var result = await client.GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal("Programación", result!.Nombre);
        Assert.Equal($"/api/v1/skills/{id}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_Http404_ReturnsNullWithoutThrowing()
    {
        var handler = new StubHandler(_ => Json<object?>(HttpStatusCode.NotFound, null));
        var client = new HabilidadApiClient(NewHttpClient(handler));

        var result = await client.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_Http204_ReturnsSuccessAndHitsDeleteRoute()
    {
        var id = Guid.NewGuid();
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = new HabilidadApiClient(NewHttpClient(handler));

        var result = await client.DeleteAsync(id);

        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Null(result.Code);
        Assert.Null(result.Message);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/skills/{id}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task DeleteAsync_Http409WithProblemDetails_ReturnsFailedResultWithConflictDetail()
    {
        var id = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Title = "CodigoDuplicado",
            Detail = "Ya existe una habilidad activa con ese código.",
            Status = 409
        };
        var handler = new StubHandler(_ => Json(HttpStatusCode.Conflict, problem));
        var client = new HabilidadApiClient(NewHttpClient(handler));

        var result = await client.DeleteAsync(id);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal("CodigoDuplicado", result.Code);
        Assert.Equal("Ya existe una habilidad activa con ese código.", result.Message);
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
        var client = new HabilidadApiClient(NewHttpClient(handler));

        var result = await client.DeleteAsync(id);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Null(result.Code);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task CreateAsync_Http400WithValidationProblemDetails_ReturnsFailureWithFieldErrors()
    {
        var validation = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["codigo"] = new[] { "El código es obligatorio." }
        })
        {
            Status = 400,
            Title = "ValidationError",
            Detail = "Datos inválidos."
        };
        var handler = new StubHandler(_ => Json(HttpStatusCode.BadRequest, validation));
        var client = new HabilidadApiClient(NewHttpClient(handler));

        var request = new CrearHabilidadRequest("", "Liderazgo");
        var result = await client.CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(HabilidadErrorType.Validation, result.Error!.Type);
        Assert.NotNull(result.FieldErrors);
        Assert.Contains("codigo", result.FieldErrors!.Keys);
    }

    [Fact]
    public async Task ReactivarAsync_Http200_ReturnsDtoAndHitsReactivarRoute()
    {
        var id = Guid.NewGuid();
        var dto = new HabilidadDto(id, "H-001", "Liderazgo", null, "Conductual");
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, dto));
        var client = new HabilidadApiClient(NewHttpClient(handler));

        var result = await client.ReactivarAsync(id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(id, result.Value!.Id);
        Assert.Equal(HttpMethod.Patch, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/skills/{id}/reactivar", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task QueryAsync_PasaQueryString_AlServicio()
    {
        var id = Guid.NewGuid();
        var payload = new PagedResult<HabilidadDto>(
            [new HabilidadDto(id, "H-001", "Liderazgo", null, "Conductual")],
            TotalCount: 1,
            Page: 1,
            PageSize: 20);
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new HabilidadApiClient(NewHttpClient(handler));

        var result = await client.QueryAsync(new HabilidadListQuery(1, 20, "lid", "nombre_desc", "eliminadas"));

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/skills/consulta", handler.LastRequest?.RequestUri?.AbsolutePath);
        var query = handler.LastRequest?.RequestUri?.Query ?? string.Empty;
        Assert.Contains("status=eliminadas", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=lid", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=nombre_desc", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetNivelesHabilidadAsync_Http200_ReturnsDtosAndHitsCatalogRoute()
    {
        var payload = new[]
        {
            new NivelHabilidadDto(Guid.NewGuid(), "BASICO", "Básico", 1, 1),
            new NivelHabilidadDto(Guid.NewGuid(), "AVANZADO", "Avanzado", 3, 3)
        };
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new HabilidadApiClient(NewHttpClient(handler));

        var result = await client.GetNivelesHabilidadAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Básico", result[0].Nombre);
        Assert.Equal("/api/v1/niveles-habilidad", handler.LastRequest?.RequestUri?.AbsolutePath);
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