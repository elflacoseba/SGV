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
    // Hallazgo #2 — Cobertura de OnPostQuitarAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PostQuitar_NonAdmin_RedirectsToAccessDenied()
    {
        // El handler chequea `EsAdministrador` antes de invocar al
        // cliente API. Un usuario autenticado sin el rol Administrador
        // recibe Forbid() → 302 a /error/403, y el cliente API nunca se
        // invoca. Esto blinda la frontera admin-only frente a un refactor
        // que mueva el chequeo detrás de la llamada de red.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        // Configurar un resultado exitoso NO debería importar: si el
        // handler cortara después de invocar al cliente, esto se
        // consumiría. La aserción SkillDeleteCalls.Empty abajo prueba
        // que NUNCA se invoca.
        apiClient.SkillDeleteResult = new CargoSkillDeleteResult(true, HttpStatusCode.NoContent, null, null);

        await using var lease = await _fixture.CreateCargoLeaseAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: false);

        // La página Habilidades emite Forbid() antes de hidratar la
        // grilla (no hay un GET exitoso del cual extraer el token
        // antiforgery). Usamos /auth/sign-in — accesible para
        // cualquier usuario autenticado — que sí renderiza un form
        // con @AntiForgeryToken implícito y contra el cual podemos
        // validar el POST contra la cookie antiforgery ya presente en
        // el jar (seteada durante el flujo de sign-in del fixture).
        var signInGet = await lease.Client.GetAsync("/auth/sign-in");
        Assert.Equal(HttpStatusCode.OK, signInGet.StatusCode);
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(signInGet);

        var response = await lease.Client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Quitar&skillId={skillId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        // Blindaje: el chequeo de rol corta ANTES de salir al backend.
        Assert.Empty(apiClient.SkillDeleteCalls);
    }

    [Fact]
    public async Task PostQuitar_TransportFailure_RedirectsWithDangerMessage()
    {
        // Falla de transporte desde DeleteSkillAsync debe traducirse en
        // un PRG con TempData danger (mensaje accionable, sin filtrar
        // stack trace). Esta es la contraparte del test
        // Post_TransportFailure_ShowsRecoverableMessage_NoStackTrace
        // aplicado al path de Quitar: Asignar/Actualizar re-renderizan
        // la página (200 OK con mensaje en la respuesta) pero Quitar
        // no puede re-renderizar porque ya eliminó la fila, así que
        // usa PRG + TempData.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.SkillDeleteException = new HttpRequestException("network down");

        await using var lease = await _fixture.CreateCargoLeaseAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        // El GET inicial sirve además para obtener el token
        // antiforgery de un form que sí se renderiza (Asignar está
        // siempre presente cuando el usuario es admin).
        var getResponse = await lease.Client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Quitar&skillId={skillId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            $"/organizacion/cargos/{cargoId}/habilidades",
            response.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

        // Seguimos el PRG y verificamos que el TempData danger llega al
        // GET renderizado. La aserción es contra el span del alert y el
        // substring del mensaje para no acoplarse al orden de clases.
        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Contains("No se pudo contactar", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("class=\"alert alert-danger\"", refreshedContent, StringComparison.Ordinal);
        // El stack trace / tipo de excepción NO debe filtrarse al HTML.
        Assert.DoesNotContain("HttpRequestException", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("network down", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at SGV.", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostQuitar_NotFound_RedirectsWithWarningMessage()
    {
        // 404 al quitar no es un error fatal: refleja una race condition
        // real (otra pestaña quitó la asociación). PRG con TempData
        // warning permite que el siguiente GET refresque la grilla sin
        // asustar al usuario con un modal de error.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.SkillDeleteResult = new CargoSkillDeleteResult(
            false,
            HttpStatusCode.NotFound,
            "AsociacionNoEncontrada",
            "La asociación ya no existe.");

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

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Contains("ya no existe", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("class=\"alert alert-warning\"", refreshedContent, StringComparison.Ordinal);
    }

    // ──────────────────────────────────────────────
    // Hallazgo #5 — ApplySkillFailureToModelState branches
    // (result.Error.Type con FieldErrors == null)
    // ──────────────────────────────────────────────
}
