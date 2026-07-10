using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGV.Aplicacion.Habilidades.Comandos;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Habilidades;
using Xunit;
using HabilidadListQuery = SGV.Web.Integration.Habilidades.HabilidadListQuery;
using RecordingHandler = SGV.Tests.Web._Shared.HttpClientExceptionScenarios.RecordingHandler;

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
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

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
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

        var result = await client.GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal("Programación", result!.Nombre);
        Assert.Equal($"/api/v1/skills/{id}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_Http404_ReturnsNullWithoutThrowing()
    {
        var handler = new RecordingHandler(_ => Json<object?>(HttpStatusCode.NotFound, null));
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

        var result = await client.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_Http204_ReturnsSuccessAndHitsDeleteRoute()
    {
        var id = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

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
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, problem));
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

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
        var handler = new RecordingHandler(_ => response);
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

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
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.BadRequest, validation));
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

        var request = new CrearHabilidadRequest("", "Liderazgo");
        var result = await client.CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(HabilidadErrorType.Validation, result.Error!.Type);
        Assert.NotNull(result.FieldErrors);
        Assert.Contains("codigo", result.FieldErrors!.Keys);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public async Task UpdateAsync_UnexpectedStatusCode_ReturnsInfrastructureFailureWithStatusPreserved(
        HttpStatusCode unexpectedStatus)
    {
        // Status inesperado (5xx / RequestTimeout / etc.) NO debe caer en
        // Validation: preservamos el status code y devolvemos Infrastructure
        // para que la página muestre un error de servidor (no de input).
        var handler = new RecordingHandler(_ => new HttpResponseMessage(unexpectedStatus)
        {
            Content = new StringContent("server boom", System.Text.Encoding.UTF8, "text/plain")
        });
        var logger = new TestLogger<HabilidadApiClient>();
        var client = new HabilidadApiClient(NewHttpClient(handler), logger);

        var id = Guid.NewGuid();
        var request = new ActualizarHabilidadRequest("H-001", "Liderazgo");
        var result = await client.UpdateAsync(id, request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(HabilidadErrorType.Infrastructure, result.Error!.Type);
        Assert.Equal((int)unexpectedStatus, result.Error.StatusCode);
        Assert.NotEmpty(logger.Entries);
    }

    [Fact]
    public async Task UpdateAsync_UnexpectedStatusCode_StillMaps404And409ToKnownTypes()
    {
        // Sanity: 404 y 409 SIGUEN mapeándose a NotFound / Conflict aunque
        // entren a la rama inesperada sin un cuerpo ProblemDetails legible.
        var handler404 = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("not-json", System.Text.Encoding.UTF8, "text/plain")
        });
        var logger = new TestLogger<HabilidadApiClient>();
        var client404 = new HabilidadApiClient(NewHttpClient(handler404), logger);
        var result404 = await client404.UpdateAsync(Guid.NewGuid(), new ActualizarHabilidadRequest("H", "n"));
        Assert.False(result404.IsSuccess);
        Assert.Equal(HabilidadErrorType.NotFound, result404.Error!.Type);

        var handler409 = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent("not-json", System.Text.Encoding.UTF8, "text/plain")
        });
        var client409 = new HabilidadApiClient(NewHttpClient(handler409), logger);
        var result409 = await client409.UpdateAsync(Guid.NewGuid(), new ActualizarHabilidadRequest("H", "n"));
        Assert.False(result409.IsSuccess);
        Assert.Equal(HabilidadErrorType.Conflict, result409.Error!.Type);
    }

    [Fact]
    public async Task ReactivarAsync_Http200_ReturnsDtoAndHitsReactivarRoute()
    {
        var id = Guid.NewGuid();
        var dto = new HabilidadDto(id, "H-001", "Liderazgo", null, "Conductual");
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, dto));
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

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
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

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
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

        var result = await client.GetNivelesHabilidadAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Básico", result[0].Nombre);
        Assert.Equal("/api/v1/niveles-habilidad", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    // ──────────────────────────────────────────────
    // PR #88 (habilidades-navegacion-cargos WU-B): cobertura directa
    // del cliente real del subrecurso GET /api/v1/skills/{id}/cargos.
    // Cubre URI building (orden de query params, escape de search, mapeo
    // de segmento) y propagación de HttpRequestException ante 5xx —
    // complementa los tests del PageModel que ejercitan el flujo end-to-end
    // contra el fake.
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetCargosAsync_Http200_BuildsExpectedUriAndReturnsPagedResult()
    {
        var skillId = Guid.NewGuid();
        var nivel = new NivelHabilidadDto(Guid.NewGuid(), "AVZ", "Avanzado", 3, 3);
        var cargo = new CargoDto(Guid.NewGuid(), "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var item = new SkillCargoDetailDto(cargo, nivel)
        {
            CargoId = cargo.Id,
            NivelRequeridoId = nivel.Id,
            Ponderacion = 1.00m,
            EsObligatoria = false,
            CargoEliminado = false,
        };
        var payload = new PagedResult<SkillCargoDetailDto>(new[] { item }, 1, 1, 20);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

        var result = await client.GetCargosAsync(
            skillId,
            new HabilidadCargosListQuery(1, 20, null, null, HabilidadSegmentoListado.Activas));

        Assert.Single(result.Items);
        Assert.Equal($"/api/v1/skills/{skillId}/cargos", handler.LastRequest?.RequestUri?.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        // Defaults normalizados: page=1&pageSize=20 sin status (activas se omite).
        Assert.Equal("page=1&pageSize=20", handler.LastRequest?.RequestUri?.Query.TrimStart('?'));
    }

    [Fact]
    public async Task GetCargosAsync_WithSearchSortAndStatus_AppendsAllQueryParamsInExpectedOrder()
    {
        // El URI building es StringBuilder con append en orden
        // page → pageSize → search → sort → status. Validar ese orden y el
        // escape de search/sort es crítico porque un cambio en el orden
        // podría romper contratos de cache downstream o WAFs.
        var skillId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, new PagedResult<SkillCargoDetailDto>(Array.Empty<SkillCargoDetailDto>(), 0, 1, 20)));
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

        await client.GetCargosAsync(
            skillId,
            new HabilidadCargosListQuery(2, 5, "lid & co", "codigo_desc", HabilidadSegmentoListado.Eliminadas));

        var query = handler.LastRequest?.RequestUri?.Query.TrimStart('?');
        Assert.Equal(
            "page=2&pageSize=5&search=lid%20%26%20co&sort=codigo_desc&status=eliminadas",
            query);
    }

    [Fact]
    public async Task GetCargosAsync_Http500_PropagatesHttpRequestException()
    {
        // EnsureSuccessStatusCode → cualquier 4xx/5xx no manejado
        // explícitamente se traduce a HttpRequestException. El PageModel
        // traduce esa excepción al estado recuperable.
        var skillId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("down", System.Text.Encoding.UTF8, "text/plain"),
        });
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetCargosAsync(
                skillId,
                new HabilidadCargosListQuery(1, 20, null, null, HabilidadSegmentoListado.Activas)));
    }

    // ──────────────────────────────────────────────
    // Cobertura de contrato de transporte (issue #78):
    // fija que QueryAsync propaga excepciones nativas del pipeline HTTP
    // y respeta un CancellationToken pre-cancelado sin iniciar el envío.
    // Si el cliente capturara la excepción o disparara el handler con el
    // token ya cancelado, estos tests fallan.
    // ──────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task QueryAsync_TransportFails_PropagatesNativeException(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        HttpMessageHandler handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

        await Assert.ThrowsAsync(
            expectedExceptionType,
            async () => await client.QueryAsync(new HabilidadListQuery(1, 20, null, null, null)));
    }

    [Fact]
    public async Task QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest()
    {
        var handler = new RecordingHandler();
        var client = new HabilidadApiClient(NewHttpClient(handler), NullLogger());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.QueryAsync(new HabilidadListQuery(1, 20, null, null, null), new CancellationToken(canceled: true)));

        Assert.Null(handler.LastRequest);
    }

    private static HttpClient NewHttpClient(HttpMessageHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };

    private static ILogger<HabilidadApiClient> NullLogger() => Microsoft.Extensions.Logging.Abstractions.NullLogger<HabilidadApiClient>.Instance;

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T payload)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(payload)
        };
        return response;
    }
}

/// <summary>
/// Minimal <see cref="ILogger{T}"/> stub que captura entradas para
/// assertions. Suficiente para verificar que el cliente loggea cuando
/// recibe un status inesperado.
/// </summary>
internal sealed class TestLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception), exception));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}