using System.Net;
using System.Web;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// Tests del detalle readonly de cargos (PR 3). Cubre los escenarios
/// "Apertura de detalle existente" y "Cargo no disponible en detalle"
/// de la especificación.
/// </summary>
public sealed class CargoDetailsPageTests : IClassFixture<CargoWebTestFixture>
{
    private readonly CargoWebTestFixture _fixture;

    public CargoDetailsPageTests(CargoWebTestFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────
    // Task 3.1: detalle de cargo existente (readonly)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenAuthenticated_ShowsCargoReadOnly()
    {
        var cargo = CargoWebTestFixture.BuildCargoDto("C-001", "Analista Funcional", "Descripción del cargo", "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/cargos/detalles/{cargo.Id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Debe mostrar los campos del cargo en modo solo lectura
        Assert.Contains(cargo.Codigo, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(cargo.Nombre, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(cargo.Descripcion!, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(cargo.NivelNombre!, content, StringComparison.OrdinalIgnoreCase);

        // Debe ofrecer "Volver al listado" con link al listado
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/organizacion/cargos", content, StringComparison.OrdinalIgnoreCase);

        // Debe ofrecer "Editar" con link a la página de edición preservando el contexto
        // (p/search/sort) del listado de origen.
        Assert.Contains("Editar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"href=\"/organizacion/cargos/editar/{cargo.Id}", content, StringComparison.OrdinalIgnoreCase);

        // No debe exponer acciones fuera del alcance. "Habilidades" sí aparece en
        // el sidenav (PR 3A lo agregó), pero NO debe figurar como contenido
        // del detalle del cargo ni como acción.
        Assert.DoesNotContain(">Crear<", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Reactivar", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-cargo-reactivate-button", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenAuthenticated_PreservesQueryStringInEditLink()
    {
        var cargo = CargoWebTestFixture.BuildCargoDto("C-001", "Analista Funcional", "Descripción del cargo", "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync(
            $"/organizacion/cargos/detalles/{cargo.Id}?p=2&search=func&sort=nombre_desc");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El href de Editar debe preservar p/search/sort para que el back-to-list
        // del Edit page mantenga el contexto del listado filtrado.
        var editHref = $"/organizacion/cargos/editar/{cargo.Id}";
        Assert.Contains(editHref, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p=2", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=func", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=nombre_desc", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Task 3.2: cargo no disponible
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenCargoNotFound_ShowsNotAvailableState()
    {
        var apiClient = FakeCargoApiClient.WithCargoList();
        var missingId = Guid.NewGuid();

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/cargos/detalles/{missingId}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Debe mostrar estado recuperable de no disponible
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);

        // Debe ofrecer camino de retorno al listado
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/organizacion/cargos", content, StringComparison.OrdinalIgnoreCase);

        // No debe exponer reactivación ni acciones fuera del alcance
        Assert.DoesNotContain(">Crear<", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Editar<", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Reactivar", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T1.3 + T1.4 (cargos-navegacion-habilidades): botón Habilidades en Details
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenCargoExists_FooterExposesHabilidadesButton()
    {
        // Req 7 escenario "Detalle existente muestra botón de habilidades":
        // la barra inferior MUST exponer un botón con texto "Habilidades"
        // y un href al detalle de Habilidades del cargo, ubicado entre
        // Editar y Volver al listado.
        var cargo = CargoWebTestFixture.BuildCargoDto("DET-001", "Cargo Detalle", "Desc", "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/cargos/detalles/{cargo.Id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            $"href=\"/organizacion/cargos/{cargo.Id}/habilidades\"",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            ">Habilidades</a>",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ti ti-stars", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenCargoNotFound_HabilidadesButtonNotRendered()
    {
        // Req 7 escenario "Detalle inexistente no muestra botón": el botón
        // Habilidades sólo aparece cuando el cargo existe; en estado
        // recuperable MUST NOT renderizarse.
        var apiClient = FakeCargoApiClient.WithCargoList();
        var missingId = Guid.NewGuid();

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/cargos/detalles/{missingId}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            ">Habilidades</a>",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "/organizacion/cargos/",
            content.Replace("/organizacion/cargos", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("/organizacion/cargos/detalles", string.Empty, StringComparison.OrdinalIgnoreCase),
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            $"/organizacion/cargos/{missingId}/habilidades",
            content,
            StringComparison.OrdinalIgnoreCase);
    }
}
