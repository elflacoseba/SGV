using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web.Habilidad;
using Xunit;

namespace SGV.Tests.Web.Cargo;

public sealed partial class CargoHabilidadesPageTests
{
    // ──────────────────────────────────────────────
    // T3.5 — Carga inicial (Req 2)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Admin_EmptySkills_RendersEmptyState()
    {
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.GetSkillsResult = Array.Empty<CargoSkillDetailDto>();

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var response = await client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Estado vacío explícito y visible para que el usuario sepa que el
        // cargo existe pero no tiene habilidades.
        Assert.Contains("no tiene habilidades", content, StringComparison.OrdinalIgnoreCase);
        // El form de "Asignar nueva habilidad" sigue presente aunque la
        // tabla esté vacía (Req 2 escenario "Cargo sin habilidades").
        Assert.Contains("Asignar", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Admin_WithSkills_RendersRowWithNivelRequeridoId()
    {
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");

        var nivelBasico = new NivelHabilidadDto(Guid.NewGuid(), "BAS", "Básico", 1, 1);
        var nivelAvanzado = new NivelHabilidadDto(Guid.NewGuid(), "AVZ", "Avanzado", 3, 3);
        var skillId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", "Desc", "Conductual");

        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.GetSkillsResult = new[]
        {
            new CargoSkillDetailDto(habilidad, nivelBasico)
            {
                SkillId = skillId,
                NivelRequeridoId = nivelAvanzado.Id,
                Ponderacion = 2.50m,
                EsObligatoria = true
            }
        };

        // La grilla re-hidrata el dropdown de niveles a partir del catálogo
        // de habilidades (no del catálogo del vínculo). Sin catálogo, el
        // select de la fila queda vacío y el NivelRequeridoId no aparece
        // en el HTML.
        var habilidadApiClient = new FakeHabilidadApiClient
        {
            NivelesResult = new[] { nivelBasico, nivelAvanzado }
        };

        using var client = await _fixture.CreateAuthenticatedClientAsync(
            apiClient,
            habilidadApiClient,
            adminRole: true);

        var response = await client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // La columna "NivelRequerido" expone el NivelRequeridoId del vínculo
        // (memoria #569), nunca un Habilidad.NivelId que no existe.
        Assert.Contains("NivelRequerido", content, StringComparison.OrdinalIgnoreCase);
        // El id del nivel requerido del vínculo viaja como value del select
        // de actualización (anti-drift: NO se usa Habilidad.NivelId).
        // La aserción usa Contains en minúsculas porque Razor no modifica
        // los GUID pero los option tags pueden contener el id con casing
        // variable según la serialización del Guid.
        var guidString = nivelAvanzado.Id.ToString().ToLowerInvariant();
        Assert.Contains(guidString, content, StringComparison.OrdinalIgnoreCase);
        // La ponderación persistida se rehidrata en el input.
        Assert.Contains($@"value=""2.50", content, StringComparison.OrdinalIgnoreCase);
        // La fila expone el nombre del nivel seleccionado para que el
        // usuario entienda qué opción está aplicada sin tener que abrir
        // el dropdown.
        Assert.Contains("Avanzado", content, StringComparison.OrdinalIgnoreCase);
    }
}
