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
    public async Task Get_Admin_RowStartsLockedAndExposesEditSaveAndSweetAlertDeleteControls()
    {
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var nivel = new NivelHabilidadDto(Guid.NewGuid(), "BAS", "Básico", 1, 1);
        var skillId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", "Desc", null, "Conductual");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.GetSkillsResult =
        [
            new CargoSkillDetailDto(habilidad, nivel)
            {
                SkillId = skillId,
                NivelRequeridoId = nivel.Id,
                Ponderacion = 1.00m,
                EsObligatoria = false
            }
        ];
        var habilidadApiClient = new FakeHabilidadApiClient
        {
            NivelesResult = [nivel]
        };

        await using var lease = await _fixture.CreateCargoLeaseAsync(
            apiClient, habilidadApiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-skill-management-row", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-skill-editable", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("disabled", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-skill-edit-button", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-skill-save-button", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-skill-delete-button", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sweetalert2.all.min.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("skill-management.js", content, StringComparison.OrdinalIgnoreCase);

        // Contrato DOM endurezido (cambio fix Quitar wiring): el botón
        // Quitar debe ser type="submit" (no type="button") para que
        // form.requestSubmit(submitter) del JS funcione con un submitter
        // real. La aserción regex rechaza markup con type="button" o sin
        // type explícito en el botón marcado con data-skill-delete-button.
        Assert.Matches(
            new Regex(@"<button[^>]*type\s*=\s*""submit""[^>]*data-skill-delete-button",
                RegexOptions.IgnoreCase),
            content);

        // Contrato DOM endurezido (cambio fix Quitar wiring): Quitar vive
        // en un <form data-skill-delete-form> APARTE del Actualizar
        // <form data-skill-update-form>. Ambos forms deben tener su
        // propia action apuntando al handler correspondiente. La regex
        // exige que el form con data-skill-delete-form tenga action
        // terminando en ?handler=Quitar y que el form con
        // data-skill-update-form tenga action terminando en
        // ?handler=Actualizar — la dirección de cada formulario es lo
        // que blinda contra el bug previo del fallback
        // deleteForm.submit() que caía en Actualizar.
        Assert.Matches(
            new Regex(@"<form[^>]*data-skill-delete-form[^>]*action\s*=\s*""[^""]*\?handler=Quitar""",
                RegexOptions.IgnoreCase),
            content);
        Assert.Matches(
            new Regex(@"<form[^>]*data-skill-update-form[^>]*action\s*=\s*""[^""]*\?handler=Actualizar""",
                RegexOptions.IgnoreCase),
            content);

        // Anti-regresión estructural: el form Quitar debe ser hermano del
        // form Actualizar dentro del mismo row, NO anidado dentro. Si el
        // form Quitar quedara nested dentro del Actualizar (caso previo
        // del bug), el navegador ignora el form anidado y el submit del
        // botón caería al form padre — resucitando el misroute. La regex
        // exige que entre el cierre </form> del Actualizar y la apertura
        // <form data-skill-delete-form> NO haya otro <form> que las
        // separe — probando que ambos forms conviven como pares
        // abrir/cerrar independientes en la fila.
        Assert.Matches(
            new Regex(
                @"<form[^>]*data-skill-update-form[^>]*>.*?</form>.*?<form[^>]*data-skill-delete-form[^>]*>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline),
            content);
    }
}
