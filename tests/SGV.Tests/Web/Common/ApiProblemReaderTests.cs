using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Common;
using Xunit;
using RecordingHandler = SGV.Tests.Web._Shared.HttpClientExceptionScenarios.RecordingHandler;

namespace SGV.Tests.Web.Common;

/// <summary>
/// Unit tests for <see cref="ApiProblemReader"/>. Centralizes the
/// safe-with-fallback parsing of <see cref="ProblemDetails"/> and
/// <see cref="ValidationProblemDetails"/> from an
/// <see cref="HttpResponseMessage"/> body.
///
/// Pre-issue-#102 each typed HTTP client (CargoApiClient,
/// HabilidadApiClient, PuestosApiClient, UnidadOrganizativaApiClient) had
/// its own near-identical copy of this logic with slightly different
/// defaults. These tests fix the matrix for the central reader; the
/// per-client mapping tests already in the repo (e.g. CargoApiClientTests,
/// HabilidadApiClientTests, PuestosApiClientTests) act as the regression
/// net once the clients start consuming the helper.
/// </summary>
public class ApiProblemReaderTests
{
    [Fact]
    public async Task ReadAsync_ResponseWithValidationProblemDetails_ReturnsStatusTitleDetailAndFieldErrors()
    {
        var validation = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["codigo"] = new[] { "El código es obligatorio." },
            ["nombre"] = new[] { "El nombre es obligatorio." }
        })
        {
            Status = 400,
            Title = "DatosInvalidos",
            Detail = "Uno o más campos son inválidos."
        };
        var response = Json(HttpStatusCode.BadRequest, validation);

        var result = await ApiProblemReader.ReadAsync(response, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("DatosInvalidos", result.Title);
        Assert.Equal("Uno o más campos son inválidos.", result.Detail);
        Assert.NotNull(result.FieldErrors);
        Assert.Equal(2, result.FieldErrors!.Count);
        Assert.Equal("El código es obligatorio.", result.FieldErrors["codigo"].Single());
        Assert.Equal("El nombre es obligatorio.", result.FieldErrors["nombre"].Single());
    }

    [Fact]
    public async Task ReadAsync_ResponseWithProblemDetails_ReturnsStatusTitleAndDetailWithoutFieldErrors()
    {
        var problem = new ProblemDetails
        {
            Status = 409,
            Title = "CodigoDuplicado",
            Detail = "Ya existe un cargo activo con el mismo código."
        };
        var response = Json(HttpStatusCode.Conflict, problem);

        var result = await ApiProblemReader.ReadAsync(response, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal("CodigoDuplicado", result.Title);
        Assert.Equal("Ya existe un cargo activo con el mismo código.", result.Detail);
        Assert.Null(result.FieldErrors);
    }

    [Fact]
    public async Task ReadAsync_ResponseWithNonJsonBody_ReturnsNullTitleAndDetail()
    {
        // 5xx con HTML/body no JSON. No debe tirar JsonException; el caller
        // decide qué hacer con Title/Detail null (típicamente: defaults
        // locales). Pre-centralización, cada cliente envolvía su propio
        // try/catch alrededor de ReadFromJsonAsync con ligeras variaciones;
        // aquí se valida la matriz común.
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("<html>boom</html>", Encoding.UTF8, "text/html")
        };

        var result = await ApiProblemReader.ReadAsync(response, CancellationToken.None);

        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Null(result.Title);
        Assert.Null(result.Detail);
        Assert.Null(result.FieldErrors);
    }

    [Fact]
    public async Task ReadAsync_ResponseWithEmptyBody_ReturnsNullTitleAndDetail()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        };

        var result = await ApiProblemReader.ReadAsync(response, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Null(result.Title);
        Assert.Null(result.Detail);
        Assert.Null(result.FieldErrors);
    }

    [Fact]
    public async Task ReadAsync_ValidationProblemDetailsWithoutErrors_ReturnsEmptyFieldErrors()
    {
        // ValidationProblemDetails con errors={} sigue siendo
        // ValidationProblemDetails; el reader debe distinguir "no trae
        // errores por campo" de "no es un ValidationProblemDetails".
        var validation = new ValidationProblemDetails
        {
            Status = 400,
            Title = "DatosInvalidos",
            Detail = "Sin errores por campo."
        };
        var response = Json(HttpStatusCode.BadRequest, validation);

        var result = await ApiProblemReader.ReadAsync(response, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("DatosInvalidos", result.Title);
        Assert.NotNull(result.FieldErrors);
        Assert.Empty(result.FieldErrors!);
    }

    [Fact]
    public async Task ReadAsync_ValidationProblemDetailsFallsBackToProblemDetailsTitle()
    {
        // Cuando el backend emite un ValidationProblemDetails sin Title pero
        // con Detail y errors, el reader debe seguir exponiendo Detail y
        // FieldErrors (no enmascarar el detalle porque falta el title).
        var validation = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["x"] = new[] { "y" }
        })
        {
            Status = 400,
            Title = null,
            Detail = "Mensaje de validación"
        };
        var response = Json(HttpStatusCode.BadRequest, validation);

        var result = await ApiProblemReader.ReadAsync(response, CancellationToken.None);

        Assert.Equal("Mensaje de validación", result.Detail);
        Assert.NotNull(result.FieldErrors);
        Assert.Equal("y", result.FieldErrors!["x"].Single());
    }

    [Fact]
    public async Task ReadAsync_PropagatesCancellation()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
        var request = new HttpRequestMessage(HttpMethod.Get, "/x");
        var response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ApiProblemReader.ReadAsync(response, cts.Token));
    }

    [Fact]
    public async Task ReadAsync_RespectsCancellationTokenBeforeBodyRead()
    {
        // El helper debe chequear el token ANTES de leer el body. Si un
        // cliente web arma un CancellationToken pre-cancelado y llama al
        // reader, no debe tocar el stream de la respuesta.
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.BadRequest,
            new ProblemDetails { Title = "X", Detail = "Y" }));
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };
        var response = await http.GetAsync("/probe");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ApiProblemReader.ReadAsync(response, cts.Token));
    }

    [Fact]
    public async Task ReadAsync_ResponseWithNullContent_ReturnsSafeFallbackWithoutThrowing()
    {
        // HttpResponseMessage.Content es non-null por defecto, pero un caller
        // o un doble de test puede asignarlo null. El reader debe degradar a
        // un fallback seguro (Title/Detail/FieldErrors null) en vez de tirar
        // NullReferenceException al intentar leer el body.
        var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = null!
        };

        var result = await ApiProblemReader.ReadAsync(response, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadGateway, result.StatusCode);
        Assert.Null(result.Title);
        Assert.Null(result.Detail);
        Assert.Null(result.FieldErrors);
    }

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T payload)
    {
        return new HttpResponseMessage(status) { Content = JsonContent.Create(payload) };
    }
}