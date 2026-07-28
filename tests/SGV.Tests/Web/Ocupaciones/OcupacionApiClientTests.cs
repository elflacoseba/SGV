using System.Net;
using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Ocupaciones;
using Xunit;

namespace SGV.Tests.Web.Ocupaciones;

/// <summary>
/// Tests RED→GREEN del cliente HTTP tipado <see cref="OcupacionApiClient"/>
/// para el módulo web de Ocupaciones. Espejo de <c>PuestosApiClientTests</c>:
/// ejercita <c>BuildQueryUri</c> vía <see cref="RecordingHandler.LastRequest"/>,
/// valida cancelación cooperativa y propagación nativa de fallos de transporte.
/// </summary>
public sealed class OcupacionApiClientTests
{
    private const string BaseUrl = "https://api.test";
    private const string BaseRoute = "/api/v1/ocupaciones";

    private static OcupacionApiClient BuildClient(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri(BaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(10)
        };
        return new OcupacionApiClient(http);
    }

    // ──────────────────────────────────────────────────
    // T-008 / RED: la firma expuesta por IOcupacionApiClient
    // y los helpers de query.
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task ListarAsync_WithActiveSegmentAndNoFilters_OmitsStatusAndOptionalParameters()
    {
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {"items":[],"totalCount":0,"page":1,"pageSize":20}
                """, System.Text.Encoding.UTF8, "application/json")
            });
        var client = BuildClient(handler);

        var query = new OcupacionListQuery(
            Page: 1,
            PageSize: 20,
            Search: null,
            Sort: null,
            Segmento: OcupacionSegmentoListado.Activas);

        var result = await client.ListarAsync(query, CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        var uri = handler.LastRequest!.RequestUri!.PathAndQuery;
        Assert.Equal($"{BaseRoute}?page=1&pageSize=20", uri);
        Assert.DoesNotContain("status=", uri, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task ListarAsync_WithDeletedSegment_AppendsStatusEliminadas()
    {
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {"items":[],"totalCount":0,"page":1,"pageSize":20}
                """, System.Text.Encoding.UTF8, "application/json")
            });
        var client = BuildClient(handler);

        var query = new OcupacionListQuery(
            Page: 1,
            PageSize: 20,
            Search: null,
            Sort: null,
            Segmento: OcupacionSegmentoListado.Eliminadas);

        _ = await client.ListarAsync(query, CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        var uri = handler.LastRequest!.RequestUri!.PathAndQuery;
        Assert.Contains("status=eliminadas", uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListarAsync_WithContextFilters_AppendsPersonaIdAndPuestoId()
    {
        var personaId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {"items":[],"totalCount":0,"page":1,"pageSize":20}
                """, System.Text.Encoding.UTF8, "application/json")
            });
        var client = BuildClient(handler);

        var query = new OcupacionListQuery(
            Page: 1,
            PageSize: 20,
            Search: null,
            Sort: null,
            Segmento: OcupacionSegmentoListado.Activas,
            PersonaId: personaId,
            PuestoId: puestoId);

        _ = await client.ListarAsync(query, CancellationToken.None);

        var uri = handler.LastRequest!.RequestUri!.PathAndQuery;
        Assert.Contains($"personaId={personaId:D}", uri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"puestoId={puestoId:D}", uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListarAsync_WithSearchAndSort_EscapesAndAppendsBoth()
    {
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {"items":[],"totalCount":0,"page":1,"pageSize":20}
                """, System.Text.Encoding.UTF8, "application/json")
            });
        var client = BuildClient(handler);

        var query = new OcupacionListQuery(
            Page: 2,
            PageSize: 50,
            Search: "ana & co",
            Sort: "persona_asc",
            Segmento: OcupacionSegmentoListado.Activas);

        _ = await client.ListarAsync(query, CancellationToken.None);

        var uri = handler.LastRequest!.RequestUri!.PathAndQuery;
        Assert.Contains("page=2", uri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pageSize=50", uri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=ana%20%26%20co", uri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=persona_asc", uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListarAsync_CancellationAlreadyRequested_DoesNotSendRequest()
    {
        var handler = HttpClientExceptionScenarios.NewRecordingHandler();
        var client = BuildClient(handler);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var query = new OcupacionListQuery(
            Page: 1,
            PageSize: 20,
            Search: null,
            Sort: null,
            Segmento: OcupacionSegmentoListado.Activas);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.ListarAsync(query, cts.Token));

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_When404_ReturnsNull()
    {
        var id = Guid.NewGuid();
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = BuildClient(handler);

        var result = await client.ObtenerPorIdAsync(id, CancellationToken.None);

        Assert.Null(result);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal($"{BaseRoute}/{id:D}", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_WhenOk_ReturnsDto()
    {
        var id = Guid.NewGuid();
        var payload = $$"""
        {
          "id":"{{id:D}}",
          "personaId":"11111111-1111-1111-1111-111111111111",
          "personaNombre":"Juan Perez",
          "puestoId":"22222222-2222-2222-2222-222222222222",
          "puestoNombre":"Analista",
          "fechaInicio":"2026-01-01",
          "fechaFin":null,
          "tipoAsignacion":"Permanente",
          "observaciones":null,
          "estado":"Vigente"
        }
        """;
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
            });
        var client = BuildClient(handler);

        var result = await client.ObtenerPorIdAsync(id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(id, result!.Id);
        Assert.Equal("Vigente", result.Estado.ToString());
        Assert.Equal("Permanente", result.TipoAsignacion.ToString());
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task ListarAsync_TransportFails_PropagatesNativeException(
        string scenario, Func<Exception> factory, Type expectedExceptionType)
    {
        _ = scenario;
        var handler = HttpClientExceptionScenarios.NewHandlerThrowing(factory);
        var client = BuildClient(handler);

        var query = new OcupacionListQuery(
            Page: 1,
            PageSize: 20,
            Search: null,
            Sort: null,
            Segmento: OcupacionSegmentoListado.Activas);

        var actual = await Record.ExceptionAsync(() => client.ListarAsync(query, CancellationToken.None));
        Assert.NotNull(actual);
        Assert.IsType(expectedExceptionType, actual);
    }
}