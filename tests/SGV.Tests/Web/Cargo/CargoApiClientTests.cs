using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Organizacion;
using Xunit;
using CargoListQuery = SGV.Web.Integration.Organizacion.CargoListQuery;
using RecordingHandler = SGV.Tests.Web._Shared.HttpClientExceptionScenarios.RecordingHandler;

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
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
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
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal("Líder", result!.Nombre);
        Assert.Equal($"/api/v1/cargos/{id}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_Http404_ReturnsNullWithoutThrowing()
    {
        var handler = new RecordingHandler(_ => Json<object?>(HttpStatusCode.NotFound, null));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_Http204_ReturnsSuccessAndHitsDeleteRoute()
    {
        var id = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
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
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.NotFound, problem));
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
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, problem));
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
        var handler = new RecordingHandler(_ => response);
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteAsync(id);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Null(result.Code);
        Assert.Null(result.Message);
    }

    // ──────────────────────────────────────────────
    // PR2A Task 12: CreateAsync / GetNivelesAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_Http201WithPayload_ReturnsDtoAndHitsPostRoute()
    {
        var nivelId = Guid.NewGuid();
        var dto = new CargoDto(Guid.NewGuid(), "C-001", "Analista", "Desc", nivelId, "Junior");
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Created, dto));
        var client = new CargoApiClient(NewHttpClient(handler));

        var request = new CrearCargoRequest("C-001", "Analista", nivelId, "Desc");
        var result = await client.CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("C-001", result.Value!.Codigo);
        Assert.Equal("Analista", result.Value.Nombre);
        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/cargos", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task CreateAsync_Http400WithValidationProblemDetails_ReturnsFailureWithFieldErrors()
    {
        var nivelId = Guid.NewGuid();
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
        var client = new CargoApiClient(NewHttpClient(handler));

        var request = new CrearCargoRequest("", "Analista", nivelId);
        var result = await client.CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(CargoErrorType.Validation, result.Error!.Type);
        Assert.NotNull(result.FieldErrors);
        Assert.Contains("codigo", result.FieldErrors!.Keys);
        Assert.Equal("El código es obligatorio.", result.FieldErrors!["codigo"][0]);
    }

    [Fact]
    public async Task CreateAsync_Http409WithProblemDetails_ReturnsFailureWithConflict()
    {
        var nivelId = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Status = 409,
            Title = "CodigoDuplicado",
            Detail = "Ya existe un cargo activo con ese código."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, problem));
        var client = new CargoApiClient(NewHttpClient(handler));

        var request = new CrearCargoRequest("C-DUP", "Analista", nivelId);
        var result = await client.CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(CargoErrorType.Conflict, result.Error!.Type);
        Assert.Equal("CodigoDuplicado", result.Error.Code);
        Assert.Equal("Ya existe un cargo activo con ese código.", result.Error.Message);
    }

    [Fact]
    public async Task GetNivelesAsync_Http200WithArray_ReturnsDtosAndHitsCatalogRoute()
    {
        var nivelId = Guid.NewGuid();
        var payload = new[]
        {
            new NivelCargoDto(nivelId, "JR", "Junior", 1, 1),
            new NivelCargoDto(Guid.NewGuid(), "SR", "Senior", 2, 2)
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.GetNivelesAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Junior", result[0].Nombre);
        Assert.Equal("Senior", result[1].Nombre);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/niveles-cargo", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    // ──────────────────────────────────────────────
    // PR2B Task 1: UpdateAsync (PUT /api/v1/cargos/{id})
    // ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Http200WithPayload_ReturnsDtoAndHitsPutRoute()
    {
        var id = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var dto = new CargoDto(id, "C-001", "Analista Senior", "Desc actualizada", nivelId, "Senior");
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, dto));
        var client = new CargoApiClient(NewHttpClient(handler));

        var request = new ActualizarCargoRequest("C-001", "Analista Senior", nivelId, "Desc actualizada");
        var result = await client.UpdateAsync(id, request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(id, result.Value!.Id);
        Assert.Equal("C-001", result.Value.Codigo);
        Assert.Equal("Analista Senior", result.Value.Nombre);
        Assert.Equal("Senior", result.Value.NivelNombre);
        Assert.Equal(HttpMethod.Put, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/cargos/{id}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task UpdateAsync_Http400WithValidationProblemDetails_ReturnsFailureWithFieldErrors()
    {
        var id = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var validation = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["codigo"] = new[] { "El código no puede superar los 50 caracteres." },
            ["nombre"] = new[] { "El nombre es obligatorio." }
        })
        {
            Status = 400,
            Title = "ValidationError",
            Detail = "Datos inválidos."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.BadRequest, validation));
        var client = new CargoApiClient(NewHttpClient(handler));

        var request = new ActualizarCargoRequest(
            new string('x', 51),
            string.Empty,
            nivelId);
        var result = await client.UpdateAsync(id, request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(CargoErrorType.Validation, result.Error!.Type);
        Assert.NotNull(result.FieldErrors);
        Assert.Contains("codigo", result.FieldErrors!.Keys);
        Assert.Contains("nombre", result.FieldErrors!.Keys);
        Assert.Equal("El código no puede superar los 50 caracteres.", result.FieldErrors!["codigo"][0]);
        Assert.Equal("El nombre es obligatorio.", result.FieldErrors!["nombre"][0]);
    }

    [Fact]
    public async Task UpdateAsync_Http409WithProblemDetails_ReturnsFailureWithConflict()
    {
        var id = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Status = 409,
            Title = "CodigoDuplicado",
            Detail = "Ya existe un cargo activo con el código C-DUP."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, problem));
        var client = new CargoApiClient(NewHttpClient(handler));

        var request = new ActualizarCargoRequest("C-DUP", "Cargo Duplicado", nivelId);
        var result = await client.UpdateAsync(id, request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(CargoErrorType.Conflict, result.Error!.Type);
        Assert.Equal("CodigoDuplicado", result.Error.Code);
        Assert.Equal("Ya existe un cargo activo con el código C-DUP.", result.Error.Message);
        Assert.Null(result.FieldErrors);
    }

    // ──────────────────────────────────────────────
    // PR3 Task 5: QueryAsync (segmented) + ReactivateAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_WithStatusEliminadas_SerializesStatusInUri()
    {
        var id = Guid.NewGuid();
        var payload = new PagedResult<CargoDto>(
            [new CargoDto(id, "DEL-001", "Eliminado", null, Guid.NewGuid())],
            TotalCount: 1,
            Page: 1,
            PageSize: 20);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.QueryAsync(new CargoListQuery(1, 20, null, null, "eliminadas"));

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/cargos/consulta", handler.LastRequest?.RequestUri?.AbsolutePath);
        Assert.Contains("status=eliminadas", handler.LastRequest?.RequestUri?.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_WithoutStatus_DoesNotIncludeStatusParameter()
    {
        var payload = new PagedResult<CargoDto>([], 0, 1, 20);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new CargoApiClient(NewHttpClient(handler));

        _ = await client.QueryAsync(new CargoListQuery(1, 20, "ana", "nombre_asc"));

        Assert.Equal("/api/v1/cargos/consulta", handler.LastRequest?.RequestUri?.AbsolutePath);
        var query = handler.LastRequest?.RequestUri?.Query ?? string.Empty;
        Assert.Contains("search=ana", query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("status=", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_WithSort_SerializesSortInUri()
    {
        // REQ-CM-01: el sort debe viajar en query string para que el
        // backend lo aplique ANTES del Skip/Take. Si no se serializa,
        // la paginación con orden se rompe entre páginas.
        var payload = new PagedResult<CargoDto>(
            [new CargoDto(Guid.NewGuid(), "C-001", "Zeta", null, Guid.NewGuid())],
            TotalCount: 1,
            Page: 1,
            PageSize: 10);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new CargoApiClient(NewHttpClient(handler));

        _ = await client.QueryAsync(new CargoListQuery(1, 10, null, "nombre_desc", null));

        Assert.Equal("/api/v1/cargos/consulta", handler.LastRequest?.RequestUri?.AbsolutePath);
        Assert.Contains("sort=nombre_desc", handler.LastRequest?.RequestUri?.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_WithoutSort_DoesNotIncludeSortParameter()
    {
        // Triangulación: cuando no hay sort, NO debe aparecer `sort=` en la URL
        // para no ensuciar el contrato con strings vacíos.
        var payload = new PagedResult<CargoDto>([], 0, 1, 10);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new CargoApiClient(NewHttpClient(handler));

        _ = await client.QueryAsync(new CargoListQuery(1, 10, null, null, null));

        Assert.Equal("/api/v1/cargos/consulta", handler.LastRequest?.RequestUri?.AbsolutePath);
        Assert.DoesNotContain("sort=", handler.LastRequest?.RequestUri?.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReactivateAsync_Http200_ReturnsDtoAndHitsReactivarRoute()
    {
        var id = Guid.NewGuid();
        var dto = new CargoDto(id, "DIRECTOR", "Director", null, Guid.NewGuid(), "Directivo");
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, dto));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.ReactivateAsync(id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(id, result.Value!.Id);
        Assert.Equal(HttpMethod.Patch, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/cargos/{id}/reactivar", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task ReactivateAsync_OnConflict_ReturnsConflictResult()
    {
        var id = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Status = 409,
            Title = "CodigoDuplicado",
            Detail = "Ya existe un cargo activo con el mismo código."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, problem));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.ReactivateAsync(id);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(CargoErrorType.Conflict, result.Error!.Type);
        Assert.Equal("CodigoDuplicado", result.Error.Code);
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
        var client = new CargoApiClient(NewHttpClient(handler));

        await Assert.ThrowsAsync(
            expectedExceptionType,
            async () => await client.QueryAsync(new CargoListQuery(1, 20, null, null, null)));
    }

    [Fact]
    public async Task QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest()
    {
        var handler = new RecordingHandler();
        var client = new CargoApiClient(NewHttpClient(handler));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.QueryAsync(new CargoListQuery(1, 20, null, null, null), new CancellationToken(canceled: true)));

        Assert.Null(handler.LastRequest);
    }

    private static HttpClient NewHttpClient(HttpMessageHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T payload)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(payload)
        };
        return response;
    }
}