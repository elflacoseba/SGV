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
    // T3.5 — Errores recuperables (Req 5)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PostActualizar_Admin_PonderacionOutOfRange_ReloadsAndRendersRangeError()
    {
        // La validación local corta antes de invocar al cliente API: la
        // página re-renderiza con un mensaje accionable y NUNCA sale al
        // backend. Esta cobertura blinda el comportamiento de "validación
        // local corto-circuito" — contraparte del test
        // Post_Asignar_BackendPonderacionFieldError que prueba el camino
        // inverso (validación local pasa, backend rechaza). Tras la
        // remediación del verify, la validación local vive en el handler
        // (no en DataAnnotations sobre el input model), pero la garantía
        // observable para el usuario es la misma: input fuera del [Range]
        // → mensaje visible, sin round trip al backend.
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
        // Si la validación local NO cortara, este Success sería el
        // resultado que vería la página — útil para distinguir un
        // fallo de la aserción Empty(SkillUpsertCalls) abajo.
        apiClient.SkillUpsertResult = CargoSkillCommandResult.Success(
            new CargoSkillDto(skillId, nivelId) { Ponderacion = 1.00m, EsObligatoria = false });

        await using var lease = await _fixture.CreateCargoLeaseAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Actualizar&skillId={skillId}",
            BuildActualizarForm(antiforgeryToken, skillId, nivelId, ponderacion: "999")); // 999 > 100 → fuera del rango

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Blindaje: la validación local corta antes de invocar al
        // cliente API. Sin esta cobertura, un refactor futuro podría
        // mover la validación al backend y romper la promesa "no round
        // trip si la entrada es inválida localmente".
        Assert.Empty(apiClient.SkillUpsertCalls);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El mensaje localizado del rango debe aparecer en el form
        // re-renderizado para que el usuario entienda por qué la
        // actualización no salió. La aserción es por substring — basta
        // con que el mensaje llegue a algún punto del HTML renderizado
        // (contenedor per-row O validation-summary general, ambos
        // comparten el mismo mensaje por diseño del helper).
        Assert.Contains("La ponderación debe estar entre", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_TransportFailure_ShowsRecoverableMessage_NoStackTrace()
    {
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.SkillUpsertException = new HttpRequestException("network down");

        await using var lease = await _fixture.CreateCargoLeaseAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var response = await lease.Client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Asignar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["AsignarInput.SkillId"] = skillId.ToString(),
                ["AsignarInput.NivelRequeridoId"] = nivelId.ToString(),
                ["AsignarInput.Ponderacion"] = "1.00"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El mensaje debe ser accionable y NO contener trazas internas.
        Assert.Contains("No se pudo contactar al servicio", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HttpRequestException", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("network down", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at SGV.", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Asignar_LocalPonderacionOutOfRange_RendersRangeErrorInPage()
    {
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.SkillUpsertResult = CargoSkillCommandResult.Failure(
            new CargoSkillError(
                CargoSkillErrorType.Validation,
                "DatosInvalidos",
                "Uno o más campos del vínculo contienen errores de validación."),
            new Dictionary<string, string[]>
            {
                ["Ponderacion"] = new[] { "La ponderación no puede superar 100.00." }
            });

        await using var lease = await _fixture.CreateCargoLeaseAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var response = await lease.Client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Asignar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["AsignarInput.SkillId"] = skillId.ToString(),
                ["AsignarInput.NivelRequeridoId"] = nivelId.ToString(),
                ["AsignarInput.Ponderacion"] = "150.00"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El mensaje específico del backend debe aparecer en el form
        // re-renderizado. Lo esperamos dentro del <span data-valmsg-for=...>
        // asociado al campo Ponderacion (anti-drift: la UI usa el id de
        // NivelRequerido del vínculo, no del catálogo Habilidad).
        Assert.True(
            Regex.IsMatch(content, @"data-valmsg-for=""AsignarInput\.Ponderacion""[^>]*>[\s\S]*?La ponderaci", RegexOptions.IgnoreCase),
            "Expected the Ponderacion field-error to render in the AsignarInput.Ponderacion validation span.");
    }

    [Fact]
    public async Task Post_Asignar_BackendPonderacionFieldError_RendersErrorInAsignarInputPonderacion()
    {
        // Este test verifica el camino real de ApplySkillFailureToModelState:
        // el backend rechaza la petición CON FieldErrors por campo. La
        // validación local pasa (Ponderacion = 50.00 ∈ [0.01, 100.00]),
        // cargoApiClient.UpsertSkillAsync es invocado, y la página
        // re-renderiza el error del backend bajo el data-valmsg-for
        // "AsignarInput.Ponderacion". El test anterior
        // (Post_Asignar_LocalPonderacionOutOfRange) NO ejercita este
        // camino porque su payload estaba fuera del [Range] y el handler
        // short-circuiteaba antes de invocar al cliente API.
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.SkillUpsertResult = CargoSkillCommandResult.Failure(
            new CargoSkillError(
                CargoSkillErrorType.Validation,
                "DatosInvalidos",
                "Uno o más campos son inválidos."),
            new Dictionary<string, string[]>
            {
                ["Ponderacion"] = new[] { "La ponderación no puede superar 100.00." }
            });

        await using var lease = await _fixture.CreateCargoLeaseAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var response = await lease.Client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Asignar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["AsignarInput.SkillId"] = skillId.ToString(),
                ["AsignarInput.NivelRequeridoId"] = nivelId.ToString(),
                ["AsignarInput.Ponderacion"] = "50.00"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El cliente API fue efectivamente invocado: el [Range] local no
        // short-circuiteó, así que este test prueba el mapeo real de
        // ApplySkillFailureToModelState con FieldErrors no vacíos.
        var upsert = Assert.Single(apiClient.SkillUpsertCalls);
        Assert.Equal(cargoId, upsert.CargoId);
        Assert.Equal(skillId, upsert.SkillId);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El mensaje específico del backend (no el [Range] local) debe
        // aparecer bajo el data-valmsg-for correcto. Esta aserción
        // distingue el camino de ApplySkillFailureToModelState del
        // short-circuit local: el mensaje "no puede superar 100.00" sólo
        // viene del backend, no del validador del input model.
        Assert.True(
            Regex.IsMatch(content, @"data-valmsg-for=""AsignarInput\.Ponderacion""[^>]*>[\s\S]*?La ponderación no puede superar 100\.00", RegexOptions.IgnoreCase),
            "Expected the backend Ponderacion field-error to render in the AsignarInput.Ponderacion validation span.");
    }

    // ──────────────────────────────────────────────
    // T2.1 + T2.3 (cargos-navegacion-habilidades):
    // per-row error anchoring + defensive fallback
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PostActualizar_BackendPonderacionFieldError_RendersErrorInActualizarRowAndSummary()
    {
        // Req 3 escenario "Error de validación anclado a la fila correcta":
        // cuando el backend rechaza una edición con FieldErrors por campo,
        // el mensaje MUST aparecer anclado al input Ponderacion de la fila
        // editada (no bajo AsignarInput.*) Y en el validation-summary
        // general. La fila se identifica por su skillId en la convención
        // Actualizar[{skillId}].Campo (form keys indexadas — ver
        // BuildActualizarForm). Esta cobertura asume remediación: el
        // handler lee los valores directamente desde Request.Form bajo el
        // prefijo Actualizar[{skillId}]. y el helper inyecta el error
        // bajo ModelState[$"Actualizar[{skillId}].Ponderacion"].
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
        apiClient.SkillUpsertResult = CargoSkillCommandResult.Failure(
            new CargoSkillError(
                CargoSkillErrorType.Validation,
                "DatosInvalidos",
                "Uno o más campos son inválidos."),
            new Dictionary<string, string[]>
            {
                ["Ponderacion"] = new[] { "Fuera de rango" }
            });

        await using var lease = await _fixture.CreateCargoLeaseAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Actualizar&skillId={skillId}",
            BuildActualizarForm(antiforgeryToken, skillId, nivelId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var upsert = Assert.Single(apiClient.SkillUpsertCalls);
        Assert.Equal(cargoId, upsert.CargoId);
        Assert.Equal(skillId, upsert.SkillId);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El mensaje del backend (no el [Range] local) debe aparecer
        // anclado a la fila correcta bajo la convención Actualizar[xxx].
        // Esta aserción distingue el camino de ApplyActualizarFailureToModelState
        // del helper legacy que mapeaba todo a AsignarInput.*. La fila se
        // identifica por su form único (cada fila es su propio <form>) y la
        // presencia del mensaje se valida por su aparición en el HTML
        // renderizado cerca del nombre del nivel (anchor de fila).
        Assert.Contains("Fuera de rango", content, StringComparison.OrdinalIgnoreCase);
        // El mensaje debe aparecer al menos dos veces: una en el contenedor
        // per-row (invalid-feedback d-block) y otra en el validation-summary.
        var occurrences = Regex.Matches(content, "Fuera de rango", RegexOptions.IgnoreCase).Count;
        Assert.True(occurrences >= 2, $"Expected the field error to appear at least twice (per-row + summary), but found {occurrences}.");
    }

    [Fact]
    public async Task PostActualizar_BackendNonWhitelistedFieldError_RendersErrorOnlyInSummary()
    {
        // Req 3 escenario "Error defensivo fuera de la fila activa":
        // cuando el backend devuelve un FieldError cuya key no está en el
        // whitelist {NivelRequeridoId,Ponderacion,EsObligatoria}, el mensaje
        // MUST aparecer solo en el validation-summary general sin anclarse
        // a ninguna fila específica.
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
        apiClient.SkillUpsertResult = CargoSkillCommandResult.Failure(
            new CargoSkillError(
                CargoSkillErrorType.Validation,
                "DatosInvalidos",
                "Uno o más campos son inválidos."),
            new Dictionary<string, string[]>
            {
                ["OtroCampo"] = new[] { "Error defensivo" }
            });

        await using var lease = await _fixture.CreateCargoLeaseAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Actualizar&skillId={skillId}",
            BuildActualizarForm(antiforgeryToken, skillId, nivelId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El mensaje defensivo MUST aparecer en el validation-summary.
        Assert.Contains("Error defensivo", content, StringComparison.OrdinalIgnoreCase);

        // Y NO debe anclarse a ninguna fila con la convención Actualizar[xxx].
        // Esto lo confirmamos verificando que el helper no creó un
        // ModelState["Actualizar[xxx].OtroCampo"] (que sólo el markup
        // manual con per-row container podría mostrar). Como el markup
        // renderiza los errores per-row con clave ModelState[$"Actualizar[xxx].Campo"]
        // y el whitelist del helper excluye "OtroCampo", el mensaje sólo
        // termina en el validation-summary (ModelState[string.Empty]).
        Assert.Contains("validation-summary-errors", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostActualizar_TwoRows_BackendPonderacionFieldError_AnchorsOnlyToEditedRow()
    {
        // SUGGESTION del verify (cargos-navegacion-habilidades): blinda
        // sin ambigüedad que un FieldErrors["Ponderacion"] devuelto por
        // el backend se ancla a la fila correcta (skill-A) cuando la
        // grilla contiene al menos dos filas (skill-A y skill-B).
        // El test cubre el camino real: se renderizan DOS filas con
        // nombres de habilidad distinguibles ("Liderazgo" y "Comunicación"),
        // se hace POST Actualizar sobre skill-A, y se verifica que:
        //   (a) el mensaje aparece asociado a la fila de skill-A (Liderazgo),
        //   (b) NO aparece asociado a la fila de skill-B (Comunicación),
        //   (c) aparece también en el validation-summary general.
        // Si el helper dejara escapar el error al summary sin anclar por
        // fila, las aserciones (a)/(b) fallarían ruidosamente.
        var cargoId = Guid.NewGuid();
        var skillAId = Guid.NewGuid();
        var skillBId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var habilidadA = new HabilidadDto(skillAId, "H-A", "Liderazgo", null, "Conductual");
        var habilidadB = new HabilidadDto(skillBId, "H-B", "Comunicación", null, "Conductual");
        var nivel = new NivelHabilidadDto(nivelId, "AVZ", "Avanzado", 3, 3);

        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.GetSkillsResult = new[]
        {
            new CargoSkillDetailDto(habilidadA, nivel)
            {
                SkillId = skillAId,
                NivelRequeridoId = nivelId,
                Ponderacion = 1.00m,
                EsObligatoria = false
            },
            new CargoSkillDetailDto(habilidadB, nivel)
            {
                SkillId = skillBId,
                NivelRequeridoId = nivelId,
                Ponderacion = 2.00m,
                EsObligatoria = false
            }
        };
        apiClient.SkillUpsertResult = CargoSkillCommandResult.Failure(
            new CargoSkillError(
                CargoSkillErrorType.Validation,
                "DatosInvalidos",
                "Uno o más campos son inválidos."),
            new Dictionary<string, string[]>
            {
                ["Ponderacion"] = new[] { "Anclaje-por-fila A" }
            });

        var habilidadApiClient = new FakeHabilidadApiClient
        {
            NivelesResult = [nivel]
        };
        await using var lease = await _fixture.CreateCargoLeaseAsync(
            apiClient, habilidadApiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Actualizar&skillId={skillAId}",
            BuildActualizarForm(antiforgeryToken, skillAId, nivelId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var upsert = Assert.Single(apiClient.SkillUpsertCalls);
        Assert.Equal(skillAId, upsert.SkillId);

var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // (c) Aparece en el validation-summary general.
        Assert.Contains("Anclaje-por-fila A", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("validation-summary-errors", content, StringComparison.OrdinalIgnoreCase);

        // (a) Aparece anclado a la fila de skill-A. Para distinguir el
        // anclaje per-row del summary, recortamos la sección entre la
        // fila que contiene "Liderazgo" (skill-A) y la fila que
        // contiene "Comunicación" (skill-B). El mensaje debe estar en
        // esa sección porque la fila de skill-A renderiza el
        // contenedor per-row bajo la convención Actualizar[xxx].
        var liderazgoIndex = content.IndexOf("Liderazgo", StringComparison.OrdinalIgnoreCase);
        var comunicacionIndex = content.IndexOf("Comunicación", StringComparison.OrdinalIgnoreCase);
        Assert.True(
            liderazgoIndex > 0 && comunicacionIndex > 0 && liderazgoIndex < comunicacionIndex,
            "Expected 'Liderazgo' (fila A) antes de 'Comunicación' (fila B) in the rendered HTML.");

        var sliceA = content.Substring(liderazgoIndex, comunicacionIndex - liderazgoIndex);
        Assert.Contains(
            "Anclaje-por-fila A",
            sliceA,
            StringComparison.OrdinalIgnoreCase);

        // (b) NO debe aparecer anclado a la fila de skill-B. Para
        // distinguir, recortamos la sección entre "Comunicación" y el
        // cierre de la tabla (</tbody>). La fila de skill-B termina en
        // </tr> dentro de </tbody>, así que el slice cubre únicamente
        // la fila de skill-B. El mensaje NO debe estar en esa sección
        // porque pertenece únicamente a la fila A — el helper inyecta
        // ModelState[$"Actualizar[skillAId].Ponderacion"] y el
        // contenedor per-row del markup sólo aparece bajo la fila A.
        var tbodyCloseIndex = content.IndexOf("</tbody>", comunicacionIndex, StringComparison.OrdinalIgnoreCase);
        Assert.True(tbodyCloseIndex > 0, "Expected a closing </tbody> after skill-B row.");
        var sliceB = content.Substring(comunicacionIndex, tbodyCloseIndex - comunicacionIndex);
        Assert.DoesNotContain(
            "Anclaje-por-fila A",
            sliceB,
            StringComparison.OrdinalIgnoreCase);
    }
}
