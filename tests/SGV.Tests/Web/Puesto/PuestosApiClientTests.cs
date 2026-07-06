using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Organizacion;
using Xunit;
using RecordingHandler = SGV.Tests.Web._Shared.HttpClientExceptionScenarios.RecordingHandler;

namespace SGV.Tests.Web.Puesto;

/// <summary>
/// Unit tests for the typed <see cref="PuestosApiClient"/>.
/// Covers HTTP translation, request paths, ProblemDetails parsing and the
/// transport contract (native exception propagation + cooperative
/// cancellation). Espejo de <c>CargoApiClientTests</c> ajustado al backend
/// de Puestos (sin subrecurso skills, sin niveles, sin <c>/consulta</c>).
/// </summary>
public class PuestosApiClientTests
{
    // ──────────────────────────────────────────────
    // GetAll / GetById
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_Http200WithArray_ReturnsDtosAndHitsGetRoute()
    {
        var id = Guid.NewGuid();
        var payload = new[] { BuildDto(id, "P-001", "Analista de datos") };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new PuestosApiClient(NewHttpClient(handler));

        var result = await client.GetAllAsync();

        Assert.Single(result);
        Assert.Equal(id, result[0].Id);
        Assert.Equal("Analista de datos", result[0].Nombre);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/puestos", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_Http200_ReturnsDtoAndHitsDetailRoute()
    {
        var id = Guid.NewGuid();
        var payload = BuildDto(id, "P-002", "Líder técnico");
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new PuestosApiClient(NewHttpClient(handler));

        var result = await client.GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal("Líder técnico", result!.Nombre);
        Assert.Equal($"/api/v1/puestos/{id}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_Http404_ReturnsNullWithoutThrowing()
    {
        var handler = new RecordingHandler(_ => Json<object?>(HttpStatusCode.NotFound, null));
        var client = new PuestosApiClient(NewHttpClient(handler));

        var result = await client.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // ──────────────────────────────────────────────
    // Create
    // ──────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_Http201WithPayload_ReturnsDtoAndHitsPostRoute()
    {
        var uoId = Guid.NewGuid();
        var cargoId = Guid.NewGuid();
        var dto = BuildDto(Guid.NewGuid(), "P-001", "Analista");
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Created, dto));
        var client = new PuestosApiClient(NewHttpClient(handler));

        var request = new CrearPuestoRequest("P-001", "Analista", uoId, cargoId);
        var result = await client.CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("P-001", result.Value!.Codigo);
        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/puestos", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task CreateAsync_Http400WithValidationProblemDetails_ReturnsFailureWithFieldErrors()
    {
        var validation = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["codigo"] = ["El código es obligatorio."],
            ["unidadOrganizativaId"] = ["Debe escoger una unidad organizativa."]
        })
        {
            Status = 400,
            Title = "DatosInvalidos",
            Detail = "Datos inválidos."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.BadRequest, validation));
        var client = new PuestosApiClient(NewHttpClient(handler));

        var request = new CrearPuestoRequest("", "Analista", Guid.Empty, Guid.NewGuid());
        var result = await client.CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(PuestoErrorType.Validation, result.Error!.Type);
        Assert.NotNull(result.FieldErrors);
        Assert.Contains("codigo", result.FieldErrors!.Keys);
        Assert.Contains("unidadOrganizativaId", result.FieldErrors!.Keys);
        Assert.Equal("El código es obligatorio.", result.FieldErrors!["codigo"][0]);
    }

    [Fact]
    public async Task CreateAsync_Http409WithProblemDetails_ReturnsFailureWithConflict()
    {
        var problem = new ProblemDetails
        {
            Status = 409,
            Title = "CodigoDuplicado",
            Detail = "Ya existe un puesto activo con ese código."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, problem));
        var client = new PuestosApiClient(NewHttpClient(handler));

        var request = new CrearPuestoRequest("P-DUP", "Analista", Guid.NewGuid(), Guid.NewGuid());
        var result = await client.CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(PuestoErrorType.Conflict, result.Error!.Type);
        Assert.Equal("CodigoDuplicado", result.Error.Code);
        Assert.Equal("Ya existe un puesto activo con ese código.", result.Error.Message);
    }

    // ──────────────────────────────────────────────
    // Update
    // ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Http200WithPayload_ReturnsDtoAndHitsPutRoute()
    {
        var id = Guid.NewGuid();
        var dto = BuildDto(id, "P-001", "Analista Senior");
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, dto));
        var client = new PuestosApiClient(NewHttpClient(handler));

        var request = new ActualizarPuestoRequest("Analista Senior", "Desc actualizada");
        var result = await client.UpdateAsync(id, request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(id, result.Value!.Id);
        Assert.Equal("Analista Senior", result.Value.Nombre);
        Assert.Equal(HttpMethod.Put, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/puestos/{id}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task UpdateAsync_Http409WithProblemDetails_ReturnsFailureWithConflict()
    {
        var id = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Status = 409,
            Title = "PuestoSuperiorInvalido",
            Detail = "El puesto no puede ser su propio superior."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, problem));
        var client = new PuestosApiClient(NewHttpClient(handler));

        var request = new ActualizarPuestoRequest("Analista", null, id);
        var result = await client.UpdateAsync(id, request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(PuestoErrorType.Conflict, result.Error!.Type);
        Assert.Equal("PuestoSuperiorInvalido", result.Error.Code);
        Assert.Null(result.FieldErrors);
    }

    // ──────────────────────────────────────────────
    // Delete → PuestoDeleteResult
    // ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_Http204_ReturnsSuccessAndHitsDeleteRoute()
    {
        var id = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = new PuestosApiClient(NewHttpClient(handler));

        var result = await client.DeleteAsync(id);

        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Null(result.Code);
        Assert.Null(result.Message);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/puestos/{id}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task DeleteAsync_Http404WithProblemDetails_ReturnsFailureWithNotFound()
    {
        var id = Guid.NewGuid();
        var problem = new ProblemDetails { Title = "PuestoNoEncontrado", Detail = "Puesto no disponible", Status = 404 };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.NotFound, problem));
        var client = new PuestosApiClient(NewHttpClient(handler));

        var result = await client.DeleteAsync(id);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal("PuestoNoEncontrado", result.Code);
        Assert.Equal("Puesto no disponible", result.Message);
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
        var client = new PuestosApiClient(NewHttpClient(handler));

        var result = await client.DeleteAsync(id);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Null(result.Code);
        Assert.Null(result.Message);
    }

    // ──────────────────────────────────────────────
    // Reactivate
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ReactivateAsync_Http200_ReturnsDtoAndHitsReactivarRoute()
    {
        var id = Guid.NewGuid();
        var dto = BuildDto(id, "P-010", "Director");
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, dto));
        var client = new PuestosApiClient(NewHttpClient(handler));

        var result = await client.ReactivateAsync(id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(id, result.Value!.Id);
        Assert.Equal(HttpMethod.Patch, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/puestos/{id}/reactivar", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task ReactivateAsync_OnConflict_ReturnsConflictResult()
    {
        var id = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Status = 409,
            Title = "CodigoDuplicado",
            Detail = "Ya existe un puesto activo con el mismo código."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, problem));
        var client = new PuestosApiClient(NewHttpClient(handler));

        var result = await client.ReactivateAsync(id);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(PuestoErrorType.Conflict, result.Error!.Type);
        Assert.Equal("CodigoDuplicado", result.Error.Code);
    }

    // ──────────────────────────────────────────────
    // Transport contract (delta web-apiclient-transport-contract):
    // los 6 métodos propagan TaskCanceledException / HttpRequestException
    // nativas y respetan un CancellationToken pre-cancelado sin enviar HTTP.
    // ──────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task GetAllAsync_TransportFails_PropagatesNativeException(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        var handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new PuestosApiClient(NewHttpClient(handler));

        await Assert.ThrowsAsync(expectedExceptionType, async () => await client.GetAllAsync());
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task GetByIdAsync_TransportFails_PropagatesNativeException(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        var handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new PuestosApiClient(NewHttpClient(handler));

        await Assert.ThrowsAsync(expectedExceptionType, async () => await client.GetByIdAsync(Guid.NewGuid()));
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task CreateAsync_TransportFails_PropagatesNativeException(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        var handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new PuestosApiClient(NewHttpClient(handler));
        var request = new CrearPuestoRequest("P-001", "Analista", Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync(expectedExceptionType, async () => await client.CreateAsync(request));
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task UpdateAsync_TransportFails_PropagatesNativeException(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        var handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new PuestosApiClient(NewHttpClient(handler));
        var request = new ActualizarPuestoRequest("Analista");

        await Assert.ThrowsAsync(expectedExceptionType, async () => await client.UpdateAsync(Guid.NewGuid(), request));
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task DeleteAsync_TransportFails_PropagatesNativeException(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        var handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new PuestosApiClient(NewHttpClient(handler));

        await Assert.ThrowsAsync(expectedExceptionType, async () => await client.DeleteAsync(Guid.NewGuid()));
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task ReactivateAsync_TransportFails_PropagatesNativeException(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        var handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new PuestosApiClient(NewHttpClient(handler));

        await Assert.ThrowsAsync(expectedExceptionType, async () => await client.ReactivateAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetAllAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest()
        => await AssertCancellationDoesNotSend(client => client.GetAllAsync(new CancellationToken(canceled: true)));

    [Fact]
    public async Task GetByIdAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest()
        => await AssertCancellationDoesNotSend(client => client.GetByIdAsync(Guid.NewGuid(), new CancellationToken(canceled: true)));

    [Fact]
    public async Task CreateAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest()
        => await AssertCancellationDoesNotSend(client => client.CreateAsync(
            new CrearPuestoRequest("P-001", "Analista", Guid.NewGuid(), Guid.NewGuid()), new CancellationToken(canceled: true)));

    [Fact]
    public async Task UpdateAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest()
        => await AssertCancellationDoesNotSend(client => client.UpdateAsync(
            Guid.NewGuid(), new ActualizarPuestoRequest("Analista"), new CancellationToken(canceled: true)));

    [Fact]
    public async Task DeleteAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest()
        => await AssertCancellationDoesNotSend(client => client.DeleteAsync(Guid.NewGuid(), new CancellationToken(canceled: true)));

    [Fact]
    public async Task ReactivateAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest()
        => await AssertCancellationDoesNotSend(client => client.ReactivateAsync(Guid.NewGuid(), new CancellationToken(canceled: true)));

    private static async Task AssertCancellationDoesNotSend(Func<PuestosApiClient, Task> operation)
    {
        var handler = new RecordingHandler();
        var client = new PuestosApiClient(NewHttpClient(handler));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation(client));

        Assert.Null(handler.LastRequest);
    }

    private static PuestoDto BuildDto(Guid id, string codigo, string nombre) =>
        new(id, codigo, nombre, null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null);

    private static HttpClient NewHttpClient(HttpMessageHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T payload) =>
        new(status) { Content = JsonContent.Create(payload) };
}
