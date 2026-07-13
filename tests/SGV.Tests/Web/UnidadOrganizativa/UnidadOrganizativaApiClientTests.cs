using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Organizacion;
using Xunit;
using RecordingHandler = SGV.Tests.Web._Shared.HttpClientExceptionScenarios.RecordingHandler;

namespace SGV.Tests.Web.UnidadOrganizativa;

/// <summary>
/// Unit tests for the typed <see cref="UnidadOrganizativaApiClient"/> focused
/// on the graceful-degradation contract of <c>ToCommandResultAsync</c>.
///
/// Regression net for issue #102: the unexpected/unmapped-status branch must
/// return a typed <see cref="UnidadOrganizativaCommandResult"/> failure
/// instead of throwing via <c>EnsureSuccessStatusCode()</c>. A thrown
/// exception here would bubble up to the Razor Page and break the elegant
/// error surface the other typed clients (Cargo, Puesto, Habilidad) already
/// guarantee.
/// </summary>
public class UnidadOrganizativaApiClientTests
{
    [Fact]
    public async Task CreateAsync_UnexpectedStatusWithNonJsonBody_ReturnsTypedFailureWithoutThrowing()
    {
        // 500 con body HTML: ni éxito ni un status mapeado (400/404/409).
        // Antes de la corrección esto pasaba por EnsureSuccessStatusCode y
        // tiraba HttpRequestException. Tras Slice 2 (#125) la matriz
        // REQ-2 sitúa 5xx en Categoria.Transport (no en Unexpected/Validation
        // como el helper local anterior). El cliente delega en
        // CommandResultMapper.Map por lo que el fallback message "El
        // servicio no respondió correctamente. Intentá nuevamente." sustituye
        // al antiguo "Unexpected" / "Respuesta inesperada del servidor."
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("<html>boom</html>", Encoding.UTF8, "text/html")
        });
        var client = new UnidadOrganizativaApiClient(NewHttpClient(handler));

        var result = await client.CreateAsync(NewRequest());

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorCategoria.Transport, result.Error!.Categoria);
        Assert.Equal("TransportError", result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_UnauthorizedStatusWithProblemDetails_PreservesTitleAndDetail()
    {
        // Slice 2 (#125): 401 ahora se bifurca como ErrorCategoria.Unauthorized
        // (era Validation/Unexpected antes del mapper). El backend envió
        // title/detail ProblemDetails; el cliente preserva ambos verbatim.
        var problem = new ProblemDetails
        {
            Status = 401,
            Title = "NoAutorizado",
            Detail = "El token expiró."
        };
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = JsonContent.Create(problem)
        });
        var client = new UnidadOrganizativaApiClient(NewHttpClient(handler));

        var result = await client.UpdateAsync(Guid.NewGuid(), NewUpdateRequest());

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorCategoria.Unauthorized, result.Error!.Categoria);
        Assert.Equal("NoAutorizado", result.Error.Code);
        Assert.Equal("El token expiró.", result.Error.Message);
    }

    [Fact]
    public async Task GetAllActivasAsync_WhenCatalogSpansMultiplePages_ReturnsAllItemsUntilTotalCount()
    {
        var first = NewDto("UO-001", "Rectorado");
        var second = NewDto("UO-002", "Talento");
        var third = NewDto("UO-003", "Finanzas");
        var requests = new List<Uri>();
        var handler = new RecordingHandler(request =>
        {
            requests.Add(request.RequestUri!);
            var page = int.Parse(System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["page"]!);
            var payload = page switch
            {
                1 => new PagedResult<UnidadOrganizativaDto>([first, second], 3, 1, 2),
                2 => new PagedResult<UnidadOrganizativaDto>([third], 3, 2, 2),
                _ => new PagedResult<UnidadOrganizativaDto>([], 3, page, 2)
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(payload)
            };
        });
        var client = new UnidadOrganizativaApiClient(NewHttpClient(handler));

        var result = await client.GetAllActivasAsync(pageSize: 2);

        Assert.Equal([first.Id, second.Id, third.Id], result.Select(item => item.Id).ToArray());
        Assert.Equal(2, requests.Count);
        Assert.All(requests, uri => Assert.Equal("/api/v1/unidades-organizativas/consulta", uri.AbsolutePath));
        Assert.Contains("page=1", requests[0].Query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pageSize=2", requests[0].Query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status=activas", requests[0].Query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("page=2", requests[1].Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAllActivasAsync_WhenServerReturnsEmptyPageBeforeTotalCount_StopsToAvoidInfiniteLoop()
    {
        var requests = new List<Uri>();
        var handler = new RecordingHandler(request =>
        {
            requests.Add(request.RequestUri!);
            var payload = new PagedResult<UnidadOrganizativaDto>([], 10, 1, 50);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(payload)
            };
        });
        var client = new UnidadOrganizativaApiClient(NewHttpClient(handler));

        var result = await client.GetAllActivasAsync(pageSize: 50);

        Assert.Empty(result);
        Assert.Single(requests);
    }

    private static UnidadOrganizativaDto NewDto(string codigo, string nombre) =>
        new(Guid.NewGuid(), codigo, nombre, Guid.NewGuid(), "Dirección", null, null, null, null, null, null);

    private static CrearUnidadOrganizativaRequest NewRequest() =>
        new("UO-001", "Dirección General", Guid.NewGuid());

    private static ActualizarUnidadOrganizativaRequest NewUpdateRequest() =>
        new("Dirección General", Guid.NewGuid());

    private static HttpClient NewHttpClient(HttpMessageHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T payload) =>
        new(status) { Content = JsonContent.Create(payload) };

    // ──────────────────────────────────────────────
    // Slice 2 (#125) — matriz REQ-2 + propagation en UnidadOrganizativaApiClient.
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ErrorCategoria.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, ErrorCategoria.Forbidden)]
    [InlineData(HttpStatusCode.RequestTimeout, ErrorCategoria.Transport)]
    [InlineData(HttpStatusCode.InternalServerError, ErrorCategoria.Transport)]
    [InlineData(HttpStatusCode.BadGateway, ErrorCategoria.Transport)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ErrorCategoria.Transport)]
    public async Task CreateAsync_NonSuccessStatus_ReturnsFailureWithCorrectCategoria(
        HttpStatusCode status, ErrorCategoria expectedCategoria)
    {
        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = $"Err{status}",
            Detail = $"Detalle del status {status}."
        };
        var handler = new RecordingHandler(_ => Json(status, problem));
        var client = new UnidadOrganizativaApiClient(NewHttpClient(handler));

        var result = await client.CreateAsync(NewRequest());

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(expectedCategoria, result.Error!.Categoria);
    }

    [Fact]
    public async Task CreateAsync_PreCanceledToken_PropagatesOperationCanceledException()
    {
        var handler = new RecordingHandler();
        var client = new UnidadOrganizativaApiClient(NewHttpClient(handler));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.CreateAsync(NewRequest(), new CancellationToken(canceled: true)));

        Assert.Null(handler.LastRequest);
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task CreateAsync_TransportFails_PropagatesNativeException_NotCategoriaTransport(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        HttpMessageHandler handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new UnidadOrganizativaApiClient(NewHttpClient(handler));

        await Assert.ThrowsAsync(
            expectedExceptionType,
            async () => await client.CreateAsync(NewRequest()));
    }

    [Fact]
    public async Task DeleteAsync_Http409WithProblemDetails_PopulatesCategoriaConflict()
    {
        var problem = new ProblemDetails
        {
            Status = 409,
            Title = "UnidadConDependientes",
            Detail = "La unidad tiene subunidades activas"
        };
        var id = Guid.NewGuid();
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, problem));
        var client = new UnidadOrganizativaApiClient(NewHttpClient(handler));

        var result = await client.DeleteAsync(id);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal(ErrorCategoria.Conflict, result.Categoria);
    }
}
