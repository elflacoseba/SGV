using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// Tests del detalle readonly de cargos (PR 3). Cubre los escenarios
/// "Apertura de detalle existente" y "Cargo no disponible en detalle"
/// de la especificación.
/// </summary>
public sealed class CargoDetailsPageTests
{
    // ──────────────────────────────────────────────
    // Task 3.1: detalle de cargo existente (readonly)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenAuthenticated_ShowsCargoReadOnly()
    {
        var cargo = CreateCargo("C-001", "Analista Funcional", "Descripción del cargo", "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);

        using var client = await CreateAuthenticatedClientAsync(apiClient);

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

        using var client = await CreateAuthenticatedClientAsync(apiClient);

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
    // Helpers de soporte
    // ──────────────────────────────────────────────

    private static CargoDto CreateCargo(string codigo, string nombre, string? descripcion, string? nivelNombre)
        => new(Guid.NewGuid(), codigo, nombre, descripcion, Guid.NewGuid(), nivelNombre);

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(FakeCargoApiClient apiClient)
    {
        var authHandler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse("token-123", DateTimeOffset.UtcNow.AddHours(1)))
            });

        var factory = new SgvWebApplicationFactory().WithOverrides(
            configureServices: services => services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test"),
            authApiHandler: authHandler,
            cargoApiClient: apiClient);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var signInResponse = await client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(signInResponse);

        var loginResponse = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "Password1!"
        }));

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        return client;
    }

    private static async Task<string> ExtractAntiforgeryTokenAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(content, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        Assert.True(match.Success, "Antiforgery token was not rendered.");
        return match.Groups[1].Value;
    }

    private sealed class RecordingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
