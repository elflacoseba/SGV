using System.Net;
using System.Web;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Vacantes.Consultas;
using SGV.Contracts.Vacantes.Enums;
using SGV.Tests.Web.Collections;
using Xunit;

namespace SGV.Tests.Web.Vacantes;

[Collection("WebIntegration")]
public sealed class VacantesIndexSmokeTests
{
    private readonly WebIntegrationFixture _fixture;

    public VacantesIndexSmokeTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_Index_WhenAuthenticated_Returns200AndDefaultsToAbiertas()
    {
        var open = FakeVacanteApiClient.BuildDto(puestoNombre: "OPEN-ROW");
        var apiClient = new FakeVacanteApiClient
        {
            ListarResult = new PagedResult<SGV.Contracts.Vacantes.Consultas.Dtos.VacanteDto>([open], 1, 1, 20)
        };

        await using var lease = await _fixture.CreateVacanteLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/organizacion/vacantes");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Vacantes", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OPEN-ROW", content, StringComparison.OrdinalIgnoreCase);
        var query = Assert.Single(apiClient.ListarCalls);
        Assert.Equal(VacanteSegmentoListado.Abiertas, query.Segmento);
    }

    [Theory]
    [InlineData("abiertas", VacanteSegmentoListado.Abiertas, "OPEN-ROW", "CLOSED-ROW")]
    [InlineData("cerradas", VacanteSegmentoListado.Cerradas, "CLOSED-ROW", "OPEN-ROW")]
    [InlineData("todas", VacanteSegmentoListado.Todas, "OPEN-ROW", "CLOSED-ROW")]
    [InlineData("invalido", VacanteSegmentoListado.Abiertas, "OPEN-ROW", "CLOSED-ROW")]
    public async Task Get_Index_SegmentsNeverMixRows(
        string status,
        VacanteSegmentoListado expectedSegment,
        string expectedRow,
        string excludedRow)
    {
        var open = FakeVacanteApiClient.BuildDto(puestoNombre: "OPEN-ROW", estadoVacanteNombre: "Abierta");
        var closed = FakeVacanteApiClient.BuildDto(
            puestoNombre: "CLOSED-ROW",
            estadoVacanteNombre: "Cubierta",
            fechaCierre: new DateTime(2026, 2, 10));
        var apiClient = new FakeVacanteApiClient
        {
            ListarHandler = query => query.Segmento switch
            {
                VacanteSegmentoListado.Abiertas => new PagedResult<SGV.Contracts.Vacantes.Consultas.Dtos.VacanteDto>([open], 1, query.Page, query.PageSize),
                VacanteSegmentoListado.Cerradas => new PagedResult<SGV.Contracts.Vacantes.Consultas.Dtos.VacanteDto>([closed], 1, query.Page, query.PageSize),
                _ => new PagedResult<SGV.Contracts.Vacantes.Consultas.Dtos.VacanteDto>([open, closed], 2, query.Page, query.PageSize)
            }
        };

        await using var lease = await _fixture.CreateVacanteLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync($"/organizacion/vacantes?status={status}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedSegment, Assert.Single(apiClient.ListarCalls).Segmento);
        Assert.Contains(expectedRow, content, StringComparison.OrdinalIgnoreCase);
        if (expectedSegment != VacanteSegmentoListado.Todas)
        {
            Assert.DoesNotContain(excludedRow, content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Get_Index_WhenApiReturns5xx_ShowsRecoverableError()
    {
        var apiClient = new FakeVacanteApiClient
        {
            ListarException = new HttpRequestException("upstream returned 503")
        };

        await using var lease = await _fixture.CreateVacanteLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/organizacion/vacantes");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo cargar el listado de vacantes", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Buscar", content, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(apiClient.ListarCalls);
    }

    [Fact]
    public async Task Get_Index_WhenAnonymous_RedirectsToSignIn()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync("/organizacion/vacantes");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
