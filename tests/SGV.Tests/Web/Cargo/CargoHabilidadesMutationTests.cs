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
    // T3.5 — Asignar / Actualizar / Quitar (Req 3, 4)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PostAsignar_Admin_CallsUpsertSkillAsync_AndPrgRedirectsWithSuccess()
    {
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);

        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        apiClient.SkillUpsertResult = CargoSkillCommandResult.Success(
            new CargoSkillDto(skillId, nivelId) { Ponderacion = 1.00m, EsObligatoria = false });

        await using var lease = await _fixture.CreateCargoLeaseAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Asignar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["AsignarInput.SkillId"] = skillId.ToString(),
                ["AsignarInput.NivelRequeridoId"] = nivelId.ToString(),
                ["AsignarInput.Ponderacion"] = "1.00",
                ["AsignarInput.EsObligatoria"] = "true"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains($"/organizacion/cargos/{cargoId}/habilidades", location, StringComparison.OrdinalIgnoreCase);

        var upsert = Assert.Single(apiClient.SkillUpsertCalls);
        Assert.Equal(cargoId, upsert.CargoId);
        Assert.Equal(skillId, upsert.SkillId);
        Assert.Equal(nivelId, upsert.Request.NivelRequeridoId);
        Assert.Equal(1.00m, upsert.Request.Ponderacion);
        Assert.True(upsert.Request.EsObligatoria);

        // El PRG debe propagar el TempData que el siguiente GET renderiza.
        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());
        Assert.Contains("se asign", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostActualizar_Admin_PropagatesPonderacionYEsObligatoria()
    {
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", null, "Conductual");
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
            new CargoSkillDto(skillId, nivelId) { Ponderacion = 2.50m, EsObligatoria = true });

        await using var lease = await _fixture.CreateCargoLeaseAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Actualizar&skillId={skillId}",
            BuildActualizarForm(antiforgeryToken, skillId, nivelId, ponderacion: "2.50"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var upsert = Assert.Single(apiClient.SkillUpsertCalls);
        Assert.Equal(cargoId, upsert.CargoId);
        Assert.Equal(skillId, upsert.SkillId);
        Assert.Equal(nivelId, upsert.Request.NivelRequeridoId);
        Assert.Equal(2.50m, upsert.Request.Ponderacion);
        Assert.True(upsert.Request.EsObligatoria);
    }

    [Fact]
    public async Task PostQuitar_Admin_CallsDeleteSkillAsync_AndPrgRedirectsWithSuccess()
    {
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.SkillDeleteResult = new CargoSkillDeleteResult(true, HttpStatusCode.NoContent, null, null);

        await using var lease = await _fixture.CreateCargoLeaseAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Quitar&skillId={skillId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var delete = Assert.Single(apiClient.SkillDeleteCalls);
        Assert.Equal(cargoId, delete.CargoId);
        Assert.Equal(skillId, delete.SkillId);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());
        Assert.Contains("quit", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Admin_QuitarButton_RendersConfirmPromptWithSkillName()
    {
        // Req 4 de cargo-skill-ui-tabla-editable exige que la interfaz MUST
        // confirmar la baja antes de quitar una asociación. El handler
        // nativo confirm() es la opción más simple y compatible con todos
        // los navegadores modernos, y mantiene el flujo HTML5 formaction
        // sin requerir un harness JS dedicado.
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");

        var nivel = new NivelHabilidadDto(Guid.NewGuid(), "BAS", "Básico", 1, 1);
        var skillId = Guid.NewGuid();
        const string skillNombre = "Liderazgo";
        var habilidad = new HabilidadDto(skillId, "H-001", skillNombre, "Desc", "Conductual");

        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.GetSkillsResult = new[]
        {
            new CargoSkillDetailDto(habilidad, nivel)
            {
                SkillId = skillId,
                NivelRequeridoId = nivel.Id,
                Ponderacion = 1.00m,
                EsObligatoria = false
            }
        };

        var habilidadApiClient = new FakeHabilidadApiClient
        {
            NivelesResult = new[] { nivel }
        };

        await using var lease = await _fixture.CreateCargoLeaseAsync(
            apiClient,
            habilidadApiClient,
            adminRole: true);

        var response = await lease.Client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // El botón Quitar debe invocar confirm() con return para cancelar
        // el submit cuando el usuario rechaza. El mensaje MUST identificar
        // la habilidad concreta (interpolando el nombre vía Razor) para que
        // el admin no quite una asociación por accidente.
        var quitarButtonMatch = Regex.Match(
            content,
            @"<button[^>]*formaction=""\?handler=Quitar[^>]*>[^<]*Quitar</button>",
            RegexOptions.IgnoreCase);
        Assert.True(quitarButtonMatch.Success, "Quitar button was not rendered.");
        var quitarButton = quitarButtonMatch.Value;
        var onclickMatch = Regex.Match(
            quitarButton,
            @"onclick\s*=\s*""([^""]*)""",
            RegexOptions.IgnoreCase);
        Assert.True(
            onclickMatch.Success,
            "Quitar button must declare an onclick attribute.");
        var onclickValue = onclickMatch.Groups[1].Value;
        Assert.Contains("return confirm(", onclickValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(skillNombre, onclickValue, StringComparison.OrdinalIgnoreCase);
    }
}
