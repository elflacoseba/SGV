using System.Net;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Ocupaciones;
using Xunit;

namespace SGV.Tests.Web.Ocupaciones;

/// <summary>
/// Cobertura fina de fallos de transporte y errores HTTP tipados para el
/// cliente HTTP de Ocupaciones. Espejo de los escenarios
/// <c>web-apiclient-transport-contract</c> (REQ-OCC-LST-004): los errores 4xx
/// no controlables (401, 403, 409) se propagan como
/// <see cref="HttpRequestException"/> desde <see cref="OcupacionApiClient"/>
/// para que el <c>PageModel</c> los traduzca vía
/// <see cref="SGV.Web.Integration.Common.TransportFailureClassifier"/> a
/// feedback recuperable.
/// </summary>
/// <remarks>
/// Slice 2 sólo cubre los métodos de lectura (Listar/ObtenerPorId). Las
/// mutaciones Crear/Actualizar/Finalizar/Eliminar/Reactivar llegan en
/// Slice 3a junto con la cobertura fina de <c>OcupacionCommandResult</c> +
/// <see cref="System.Threading.Tasks.TaskCanceledException"/>
/// discriminada por <c>PageFeedback</c>.
/// </remarks>
public sealed class OcupacionApiClientErrorCoverageTests
{
    private const string BaseUrl = "https://api.test";

    private static OcupacionApiClient BuildClient(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri(BaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(10)
        };
        return new OcupacionApiClient(http);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_WhenUnauthorized_PropagatesAsHttpRequestException()
    {
        var id = Guid.NewGuid();
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = BuildClient(handler);

        var ex = await Record.ExceptionAsync(() => client.ObtenerPorIdAsync(id, CancellationToken.None));
        Assert.NotNull(ex);
        Assert.IsType<HttpRequestException>(ex);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_WhenForbidden_PropagatesAsHttpRequestException()
    {
        var id = Guid.NewGuid();
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var client = BuildClient(handler);

        var ex = await Record.ExceptionAsync(() => client.ObtenerPorIdAsync(id, CancellationToken.None));
        Assert.NotNull(ex);
        Assert.IsType<HttpRequestException>(ex);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_WhenServerError_PropagatesAsHttpRequestException()
    {
        var id = Guid.NewGuid();
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = BuildClient(handler);

        var ex = await Record.ExceptionAsync(() => client.ObtenerPorIdAsync(id, CancellationToken.None));
        Assert.NotNull(ex);
        Assert.IsType<HttpRequestException>(ex);
    }

    [Fact]
    public async Task ListarAsync_WhenValidationError_PropagatesAsHttpRequestException()
    {
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var client = BuildClient(handler);

        var query = new SGV.Contracts.Ocupaciones.Consultas.OcupacionListQuery(
            Page: 1,
            PageSize: 20,
            Search: null,
            Sort: null,
            Segmento: OcupacionSegmentoListado.Activas);

        var ex = await Record.ExceptionAsync(() => client.ListarAsync(query, CancellationToken.None));
        Assert.NotNull(ex);
        Assert.IsType<HttpRequestException>(ex);
    }

    [Fact]
    public async Task ListarAsync_WhenConflict_PropagatesAsHttpRequestException()
    {
        // 409 puede ocurrir si el backend detecta una colisión no-esperada
        // incluso en una operación de lectura (e.g. inconsistencia
        // interna); el cliente propaga y el PageModel lo categoriza via
        // CommandResultMapper + TransportFailureClassifier.
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.Conflict));
        var client = BuildClient(handler);

        var query = new SGV.Contracts.Ocupaciones.Consultas.OcupacionListQuery(
            Page: 1,
            PageSize: 20,
            Search: null,
            Sort: null,
            Segmento: OcupacionSegmentoListado.Activas);

        var ex = await Record.ExceptionAsync(() => client.ListarAsync(query, CancellationToken.None));
        Assert.NotNull(ex);
        Assert.IsType<HttpRequestException>(ex);
    }
}