using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Habilidades;
using Xunit;

namespace SGV.Tests.Web.Habilidad;

/// <summary>
/// Fixture compartida (<see cref="IClassFixture{TFixture}"/>) para los tests
/// web de Habilidades. Configura el fake auth handler y un
/// <see cref="FakeHabilidadApiClient"/> por test, devolviendo un cliente
/// autenticado.
/// </summary>
public sealed class HabilidadWebTestFixture : IDisposable
{
    private readonly SgvWebApplicationFactory _baseFactory;

    public HabilidadWebTestFixture()
    {
        _baseFactory = new SgvWebApplicationFactory();
    }

    public SgvWebApplicationFactory BaseFactory => _baseFactory;

    public SgvWebApplicationFactory WithHabilidadApiClient(FakeHabilidadApiClient fake)
        => _baseFactory.WithOverrides(habilidadApiClient: fake);

    /// <summary>
    /// Construye un <see cref="HabilidadDto"/> con ids aleatorios.
    /// </summary>
    public static HabilidadDto BuildHabilidadDto(string codigo, string nombre, string? descripcion, string? categoria)
        => new(Guid.NewGuid(), codigo, nombre, descripcion, categoria);

    public async Task<HttpClient> CreateAuthenticatedClientAsync(FakeHabilidadApiClient apiClient)
    {
        var authHandler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse("token-123", DateTimeOffset.UtcNow.AddHours(1)))
            });

        var factory = _baseFactory.WithOverrides(
            configureServices: services => services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test"),
            authApiHandler: authHandler,
            habilidadApiClient: apiClient);

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

    public static async Task<string> ExtractAntiforgeryTokenAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(content, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        Assert.True(match.Success, "Antiforgery token was not rendered.");
        return match.Groups[1].Value;
    }

    /// <summary>
    /// Indica si el HTML contiene un <c>&lt;input&gt;</c> con
    /// <c>name="{inputName}"</c> (selector puntual, evita falsos positivos
    /// por aparición textual del nombre en otro lugar del documento).
    /// </summary>
    public static bool HasInputNamed(string content, string inputName)
    {
        var pattern = $@"<input\b[^>]*\bname=""{Regex.Escape(inputName)}""[^>]*\/?>";
        return Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Indica si el <c>&lt;input&gt;</c> con <c>name="{inputName}"</c> tiene
    /// el atributo <paramref name="attributeName"/>. El chequeo se hace sobre
    /// el MISMO tag para no confundir con un input posterior.
    /// </summary>
    public static bool InputHasAttribute(string content, string inputName, string attributeName)
    {
        var pattern = $@"<input\b[^>]*\bname=""{Regex.Escape(inputName)}""[^>]*\/?>";
        var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        var inputTag = content.Substring(match.Index, match.Length);
        return Regex.IsMatch(inputTag, $@"\b{Regex.Escape(attributeName)}\b(=""[^""]*"")?", RegexOptions.IgnoreCase);
    }

    public void Dispose() => _baseFactory?.Dispose();

    /// <summary>
    /// Minimal <see cref="HttpMessageHandler"/> que siempre devuelve un
    /// <see cref="HttpResponseMessage"/> preconfigurado. Stub del auth.
    /// </summary>
    public sealed class RecordingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}