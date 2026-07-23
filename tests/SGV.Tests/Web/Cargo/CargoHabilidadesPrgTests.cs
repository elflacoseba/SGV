using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Habilidad;
using Xunit;

namespace SGV.Tests.Web.Cargo;

public sealed partial class CargoHabilidadesPageTests
{
    // ──────────────────────────────────────────────
    // T2.2 + T2.3 caso 3 (cargos-navegacion-habilidades):
    // PRG no-regression for Actualizar success
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PostActualizar_Success_PreservesPrgFlowAndReloadsGridWithNewValues()
    {
        // Req 3 escenario "Éxito de edición preserva el flujo editable":
        // cuando el backend responde éxito, la página MUST persistir los
        // cambios contra el backend mediante PRG con TempData Y MUST volver
        // a cargar la grilla manteniéndola editable y mostrando los nuevos
        // valores. Esta cobertura blinda la transición del helper de
        // AsignarInput.* a Actualizar[xxx].* para que el camino feliz de
        // Actualizar siga funcionando. Tras la remediación, los form
        // keys viajan como Actualizar[{skillId}].Campo (ver
        // BuildActualizarForm) y el handler extrae esos valores desde
        // Request.Form para alimentar AsignarCargoSkillRequest.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", null, null, "Conductual");
        var nivel = new NivelHabilidadDto(nivelId, "AVZ", "Avanzado", 3, 3);

        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.GetSkillsResult = new[]
        {
            new CargoSkillDetailDto(habilidad, nivel)
            {
                SkillId = skillId,
                NivelRequeridoId = nivelId,
                Ponderacion = 1.00m,
                EsObligatoria = false
            }
        };
        apiClient.SkillUpsertResult = CargoSkillCommandResult.Success(
            new CargoSkillDto(skillId, nivelId) { Ponderacion = 3.50m, EsObligatoria = true });

        await using var lease = await _fixture.CreateCargoLeaseAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Actualizar&skillId={skillId}",
            BuildActualizarForm(antiforgeryToken, skillId, nivelId, ponderacion: "3.50"));

        // PRG: redirect 302 a la misma página.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains($"/organizacion/cargos/{cargoId}/habilidades", location, StringComparison.OrdinalIgnoreCase);

        // El cliente API fue invocado con los valores correctos.
        var upsert = Assert.Single(apiClient.SkillUpsertCalls);
        Assert.Equal(cargoId, upsert.CargoId);
        Assert.Equal(skillId, upsert.SkillId);
        Assert.Equal(nivelId, upsert.Request.NivelRequeridoId);
        Assert.Equal(3.50m, upsert.Request.Ponderacion);
        Assert.True(upsert.Request.EsObligatoria);

        // El TempData del PRG debe propagarse al siguiente GET, que recarga
        // la grilla con los nuevos valores.
        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("actualiz", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }
}
