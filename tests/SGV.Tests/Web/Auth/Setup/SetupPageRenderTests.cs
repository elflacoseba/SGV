using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Setup;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Setup;
using Xunit;

namespace SGV.Tests.Web.Auth.Setup;

/// <summary>
/// Tests de integración de la Razor Page <c>/auth/setup</c> (issue #195
/// / WU-4). Cubren el render del formulario con los 9 campos y el
/// dropdown de <c>TipoDocumento</c>, la redirección a
/// <c>/auth/sign-in</c> cuando el setup ya fue completado, y el PRG
/// posterior al submit exitoso con <c>TempData["SetupSuccess"]</c>.
/// </summary>
[Collection("WebIntegration")]
public sealed class SetupPageRenderTests
{
    private readonly WebIntegrationFixture _fixture;

    public SetupPageRenderTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_Setup_Renderiza9CamposYDropdownConAntiforgery()
    {
        var fake = new FakeSetupApiClient
        {
            Status = new SetupStatusResponse(true),
            TiposDocumento = BuildTiposDocumento()
        };
        await using var lease = await _fixture.CreateSetupLeaseAsync(fake);

        var response = await lease.Client.GetAsync("/auth/setup");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Configuración Inicial", WebUtility.HtmlDecode(content));
        Assert.Contains("__RequestVerificationToken", content);
        Assert.True(InputIsPresent(content, "Input.Nombres"));
        Assert.True(InputIsPresent(content, "Input.Apellidos"));
        Assert.True(InputIsPresent(content, "Input.Legajo"));
        Assert.True(InputIsPresent(content, "Input.Email"));
        Assert.True(InputIsPresent(content, "Input.UserName"));
        Assert.True(InputIsPresent(content, "Input.Password"));
        Assert.True(SelectIsPresent(content, "Input.TipoDocumentoId"));
        Assert.True(InputIsPresent(content, "Input.NumeroDocumento"));
        Assert.True(InputIsPresent(content, "Input.Telefono"));
        Assert.Contains("DNI", WebUtility.HtmlDecode(content));
        Assert.Contains("PAS", WebUtility.HtmlDecode(content));
    }

    [Fact]
    public async Task Get_Setup_ConDbNoVacia_RedirigeASignIn()
    {
        // Spec REQ-SETUP-005 / escenario "Setup no disponible": si la
        // base ya tiene usuarios, /auth/setup NO debe renderizar.
        var fake = new FakeSetupApiClient
        {
            Status = new SetupStatusResponse(false)
        };
        await using var lease = await _fixture.CreateSetupLeaseAsync(fake);

        var response = await lease.Client.GetAsync("/auth/setup");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/auth/sign-in", response.Headers.Location!.OriginalString);
        Assert.Equal(1, fake.StatusCallCount);
    }

    [Fact]
    public async Task Get_Setup_ConDbVacia_NoRedirige()
    {
        var fake = new FakeSetupApiClient
        {
            Status = new SetupStatusResponse(true)
        };
        await using var lease = await _fixture.CreateSetupLeaseAsync(fake);

        var response = await lease.Client.GetAsync("/auth/setup");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_Setup_DatosValidos_RedirigeASignInConTempData()
    {
        var fake = new FakeSetupApiClient
        {
            Status = new SetupStatusResponse(true),
            TiposDocumento = BuildTiposDocumento(),
            CrearResult = SetupHttpResult.Success(
                new SetupResult(Guid.NewGuid(), "user-123", "admin"))
        };
        await using var lease = await _fixture.CreateSetupLeaseAsync(fake);

        var getResponse = await lease.Client.GetAsync("/auth/setup");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/auth/setup", new FormUrlEncodedContent(
            BuildValidFormData(antiforgery, "admin", "admin@setup.test", "Setup#12345")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/auth/sign-in", response.Headers.Location!.OriginalString);
        Assert.NotNull(fake.LastCreateRequest);
        Assert.Equal("admin", fake.LastCreateRequest!.UserName);
        Assert.Equal("admin@setup.test", fake.LastCreateRequest.Email);
        Assert.Equal("Setup#12345", fake.LastCreateRequest.Password);
    }

    [Fact]
    public async Task Post_Setup_ApiDevuelve400ConFieldErrors_MuestraErroresPorCampo()
    {
        var fake = new FakeSetupApiClient
        {
            Status = new SetupStatusResponse(true),
            TiposDocumento = BuildTiposDocumento(),
            CrearResult = SetupHttpResult.Failure(
                new SetupHttpError(SetupErrorCode.DatosInvalidos, "Datos inválidos", HttpStatusCode.BadRequest),
                new Dictionary<string, string[]>
                {
                    ["Password"] = new[] { "La contraseña debe tener al menos 6 caracteres." },
                    ["Email"] = new[] { "El email no tiene un formato válido." }
                })
        };
        await using var lease = await _fixture.CreateSetupLeaseAsync(fake);

        var getResponse = await lease.Client.GetAsync("/auth/setup");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/auth/setup", new FormUrlEncodedContent(
            BuildValidFormData(antiforgery, "admin", "admin@setup.test", "Setup#12345")));
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("La contraseña debe tener al menos 6 caracteres.", content);
        Assert.Contains("El email no tiene un formato válido.", content);
    }

    [Fact]
    public async Task Post_Setup_ApiCae_MuestraMensajeRecuperable()
    {
        var fake = new FakeSetupApiClient
        {
            Status = new SetupStatusResponse(true),
            TiposDocumento = BuildTiposDocumento(),
            CrearException = new HttpRequestException("connection refused")
        };
        await using var lease = await _fixture.CreateSetupLeaseAsync(fake);

        var getResponse = await lease.Client.GetAsync("/auth/setup");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/auth/setup", new FormUrlEncodedContent(
            BuildValidFormData(antiforgery, "admin", "admin@setup.test", "Setup#12345")));
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // El mensaje debe ser recuperable y NO pedir reintento ciego.
        Assert.Contains("No se pudo conectar con el servidor", content);
    }

    [Fact]
    public async Task Post_Setup_ApiTimeOut_MuestraMensajeRecuperable()
    {
        var fake = new FakeSetupApiClient
        {
            Status = new SetupStatusResponse(true),
            TiposDocumento = BuildTiposDocumento(),
            CrearException = new TaskCanceledException("timeout")
        };
        await using var lease = await _fixture.CreateSetupLeaseAsync(fake);

        var getResponse = await lease.Client.GetAsync("/auth/setup");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/auth/setup", new FormUrlEncodedContent(
            BuildValidFormData(antiforgery, "admin", "admin@setup.test", "Setup#12345")));
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("tardó demasiado", content);
    }

    [Fact]
    public async Task Post_Setup_ModelStateInvalido_NoLlamaApi()
    {
        var fake = new FakeSetupApiClient
        {
            Status = new SetupStatusResponse(true),
            TiposDocumento = BuildTiposDocumento()
        };
        await using var lease = await _fixture.CreateSetupLeaseAsync(fake);

        var getResponse = await lease.Client.GetAsync("/auth/setup");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/auth/setup", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgery,
                ["Input.Nombres"] = "",
                ["Input.Apellidos"] = "",
                ["Input.Email"] = "",
                ["Input.UserName"] = "",
                ["Input.Password"] = ""
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(fake.LastCreateRequest);
    }

    private static IReadOnlyList<TipoDocumentoDto> BuildTiposDocumento() => new[]
    {
        new TipoDocumentoDto(Guid.Parse("71000000-0000-0000-0000-000000000001"), "DNI", "Documento Nacional", "^\\d{7,8}$", 7, 8),
        new TipoDocumentoDto(Guid.Parse("71000000-0000-0000-0000-000000000002"), "PAS", "Pasaporte", null, null, null)
    };

    private static Dictionary<string, string> BuildValidFormData(
        string antiforgery, string userName, string email, string password) =>
        new()
        {
            ["__RequestVerificationToken"] = antiforgery,
            ["Input.Nombres"] = "Operador",
            ["Input.Apellidos"] = "Inicial",
            ["Input.Legajo"] = "LEG-001",
            ["Input.Email"] = email,
            ["Input.UserName"] = userName,
            ["Input.Password"] = password,
            ["Input.TipoDocumentoId"] = "",
            ["Input.NumeroDocumento"] = "12345678",
            ["Input.Telefono"] = "+5491100000000"
        };

    private static bool InputIsPresent(string content, string name)
        => Regex.IsMatch(content, $@"<input\b[^>]*\bname=""{Regex.Escape(name)}""", RegexOptions.IgnoreCase);

    private static bool SelectIsPresent(string content, string name)
        => Regex.IsMatch(content, $@"<select\b[^>]*\bname=""{Regex.Escape(name)}""", RegexOptions.IgnoreCase);
}
