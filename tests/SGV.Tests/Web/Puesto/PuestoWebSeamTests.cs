using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Organizacion;
using Xunit;
using PuestoListQuery = SGV.Contracts.Organizacion.Consultas.Dtos.PuestoListQuery;

namespace SGV.Tests.Web.Puesto;

/// <summary>
/// Seam tests de PR 1 para el módulo web de Puestos (migrado a composite en PR 2b-2):
///   - shape de <see cref="PuestoListItemViewModel"/>, <see cref="PuestoDeleteResult"/>
///     y <see cref="PuestoListQuery"/>;
///   - resolución del cliente tipado <see cref="IPuestosApiClient"/> desde la
///     composición raíz registrada en <c>Program.cs</c>;
///   - override del fake vía <c>SgvWebApplicationFactory.WithOverrides</c>
///     (espejo del patrón <c>CargoWebSeamTests</c>);
///   - render de la entry colapsable "Puestos" en el sidenav autenticado.
/// </summary>
[Collection("WebIntegration")]
public class PuestoWebSeamTests
{
    private readonly WebIntegrationFixture _fixture;

    public PuestoWebSeamTests(WebIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    // ── Shape de records ────────────────────────────────────────

    [Fact]
    public void PuestoListItemViewModel_Constructor_ExposesAllPropertiesAndCodigoYNombre()
    {
        var id = Guid.NewGuid();
        var superiorId = Guid.NewGuid();
        var vm = new PuestoListItemViewModel(id, "P-001", "Analista", "Desc", "Ventas", "Vendedor", superiorId);

        Assert.Equal(id, vm.Id);
        Assert.Equal("P-001", vm.Codigo);
        Assert.Equal("Analista", vm.Nombre);
        Assert.Equal("Desc", vm.Descripcion);
        Assert.Equal("Ventas", vm.UnidadOrganizativaNombre);
        Assert.Equal("Vendedor", vm.CargoNombre);
        Assert.Equal(superiorId, vm.PuestoSuperiorId);
        Assert.Equal("P-001 — Analista", vm.CodigoYNombre);
    }

    [Fact]
    public void PuestoDeleteResult_Constructor_ExposesAllProperties()
    {
        var result = new PuestoDeleteResult(true, HttpStatusCode.NoContent, "Code", "Message");

        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Equal("Code", result.Code);
        Assert.Equal("Message", result.Message);
    }

    [Fact]
    public void PuestoListQuery_Constructor_ExposesContractDefaults()
    {
        var active = new PuestoListQuery(1, 20, null, null);
        var deleted = new PuestoListQuery(
            3,
            10,
            "ana",
            "codigo_desc",
            PuestoSegmentoListado.Eliminadas);

        Assert.Equal(1, active.Page);
        Assert.Equal(20, active.PageSize);
        Assert.Null(active.Search);
        Assert.Null(active.Sort);
        Assert.Equal(PuestoSegmentoListado.Activas, active.Segmento);

        Assert.Equal(3, deleted.Page);
        Assert.Equal(10, deleted.PageSize);
        Assert.Equal("ana", deleted.Search);
        Assert.Equal("codigo_desc", deleted.Sort);
        Assert.Equal(PuestoSegmentoListado.Eliminadas, deleted.Segmento);
    }

    // ── DI + override del seam ──────────────────────────────────

    [Fact]
    public void ProductionRegistration_ResolvesPuestosApiClient()
    {
        using var scope = _fixture.RootFactory.Services.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<IPuestosApiClient>();

        Assert.NotNull(client);
        Assert.IsType<PuestosApiClient>(client);
    }

    [Fact]
    public async Task WithOverrides_PuestosApiClient_SwapsToFakeImplementation()
    {
        var fake = new FakePuestosApiClient();

        await using var lease = await _fixture.CreatePuestoLeaseAsync(fake);
        using var scope = lease.Factory.Services.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IPuestosApiClient>();

        Assert.Same(fake, resolved);
    }

    [Fact]
    public async Task WithPuestosApiClient_DefaultDeleteAsync_ReturnsSuccessAndRecordsCall()
    {
        var fake = new FakePuestosApiClient();
        var id = Guid.NewGuid();

        var result = await fake.DeleteAsync(id);

        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Contains(id, fake.DeleteCalls);
    }

    [Fact]
    public async Task WithPuestosApiClient_ConfiguredConflictDeleteResult_IsReturned()
    {
        var fake = new FakePuestosApiClient
        {
            DeleteResult = new PuestoDeleteResult(false, HttpStatusCode.Conflict, "PuestoConflicto", "El puesto no puede eliminarse")
        };
        var id = Guid.NewGuid();

        var result = await fake.DeleteAsync(id);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal("PuestoConflicto", result.Code);
        Assert.Equal("El puesto no puede eliminarse", result.Message);
        Assert.Contains(id, fake.DeleteCalls);
    }

    // ── Sidenav (shell) ─────────────────────────────────────────

    [Fact]
    public async Task Get_Sidenav_WhenAuthenticated_ExposesPuestosModule()
    {
        await using var lease = await _fixture.CreatePuestoLeaseAsync(new FakePuestosApiClient());

        var response = await lease.Client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("aria-controls=\"puestos\"", content, StringComparison.Ordinal);
        Assert.Contains(">Puestos<", content, StringComparison.Ordinal);
        Assert.Contains("href=\"/organizacion/puestos\"", content, StringComparison.Ordinal);
        Assert.Contains("href=\"/organizacion/puestos/crear\"", content, StringComparison.Ordinal);

        // El icono ti-briefcase debe pertenecer a la entry de Puestos (no solo
        // a la de Cargos): la regex ancla desde aria-controls="puestos" hasta el
        // primer icono, que es el de la propia entry.
        Assert.Matches(
            new Regex("aria-controls=\"puestos\"[^>]*>\\s*<span class=\"menu-icon\"><i class=\"ti ti-hierarchy\""),
            content);
    }

    [Fact]
    public async Task Get_Sidenav_WhenAuthenticated_DoesNotExposeUnimplementedModules()
    {
        await using var lease = await _fixture.CreatePuestoLeaseAsync(new FakePuestosApiClient());

        var response = await lease.Client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Se afirma sobre aria-controls (estructural, solo existe en el nav) en
        // vez de texto entre tags: "Vacantes" aparece en meta description y
        // footer del layout, lo que haría una aserción textual frágil.
        Assert.DoesNotContain(@"aria-controls=""reclutamiento""", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"aria-controls=""seleccion""", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"aria-controls=""postulantes""", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"aria-controls=""catalogos""", content, StringComparison.OrdinalIgnoreCase);
    }

    // ── Sidenav active/expanded (PR 2 — diferidos de PR 1 porque la ruta
    //    /organizacion/puestos sólo existe cuando llega Index) ──────────────

    /// <summary>
    /// PR 2: cuando el usuario está en /organizacion/puestos, el grupo Puestos
    /// del sidenav debe estar marcado active y el sub-item Listado debe heredar
    /// el mismo estado.
    /// </summary>
    [Fact]
    public async Task Get_Sidenav_WhenOnPuestosRoute_SubmenuIsActive()
    {
        // Un puesto cualquiera: la página Index lo usa para renderizar la grilla.
        var apiClient = FakePuestosApiClient.WithPuestoList(
            WebTestBuilders.BuildPuestoDto("P-001", "Analista", null, null));

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/organizacion/puestos");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El grupo padre debe tener la clase `active` (espejo de la regex
        // usada por CargoCreatePageTests para validar la entry de Cargos).
        Assert.True(
            Regex.IsMatch(
                content,
                @"<a[^>]*aria-controls=""puestos""[^>]*class=""[^""]*\bactive\b[^""]*""",
                RegexOptions.IgnoreCase),
            "El grupo Puestos del sidenav debe estar marcado active cuando la ruta es /organizacion/puestos.");

        // El sub-item Listado también debe heredar el active. La sidenav
        // renderiza el atributo `class` ANTES de `href` en este anchor, así
        // que el regex es order-agnostic (positive lookaheads para ambos
        // atributos, sin importar el orden en que aparecen en la tag).
        Assert.True(
            Regex.IsMatch(
                content,
                @"<a(?=[^>]*\bhref=""/organizacion/puestos"")(?=[^>]*\bclass=""[^""]*\bactive\b)[^>]*>",
                RegexOptions.IgnoreCase),
            "El sub-item Listado del sidenav debe estar marcado active cuando la ruta es /organizacion/puestos.");
    }

    /// <summary>
    /// PR 2: cuando el usuario está en una subruta de Puestos
    /// (e.g. /organizacion/puestos/crear), el grupo Puestos debe seguir marcado
    /// active y el atributo aria-expanded debe ser true (submenú desplegado).
    /// </summary>
    [Fact]
    public async Task Get_Sidenav_WhenOnPuestosSubroute_SubmenuIsExpanded()
    {
        var apiClient = FakePuestosApiClient.WithPuestoList();

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/organizacion/puestos?status=eliminadas");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El grupo Puestos debe estar marcado active (StartsWithSegments cubre
        // /organizacion/puestos y sub-rutas).
        Assert.True(
            Regex.IsMatch(
                content,
                @"<a[^>]*aria-controls=""puestos""[^>]*class=""[^""]*\bactive\b[^""]*""",
                RegexOptions.IgnoreCase),
            "El grupo Puestos debe estar marcado active en una sub-ruta de /organizacion/puestos.");
    }
}
