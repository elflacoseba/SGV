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

        // No debe exponer acciones fuera del alcance
        Assert.DoesNotContain(">Crear<", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Editar<", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Habilidades", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Reactivar", content, StringComparison.OrdinalIgnoreCase);
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
}
