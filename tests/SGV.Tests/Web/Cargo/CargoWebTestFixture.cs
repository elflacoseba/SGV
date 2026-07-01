using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// Shared xUnit fixture (<see cref="IClassFixture{TFixture}"/>) for the
/// Cargo web tests. Encapsulates the recurring setup: a base
/// <see cref="SgvWebApplicationFactory"/>, an authenticated
/// <see cref="HttpClient"/> wired with a <see cref="FakeCargoApiClient"/>,
/// and a small set of seed-data builders (<see cref="BuildCargoDto"/>).
/// Test names and assertions in the consuming classes are not affected;
/// only the duplicated helpers move here.
/// </summary>
public sealed class CargoWebTestFixture : IDisposable
{
    private readonly SgvWebApplicationFactory _baseFactory;

    public CargoWebTestFixture()
    {
        _baseFactory = new SgvWebApplicationFactory();
    }

    /// <summary>
    /// Base factory without overrides. Tests that need no cargo override
    /// can call <c>BaseFactory.CreateClient(...)</c> directly.
    /// </summary>
    public SgvWebApplicationFactory BaseFactory => _baseFactory;

    /// <summary>
    /// Returns a new factory with <see cref="ICargoApiClient"/> swapped
    /// for the supplied <paramref name="fake"/>. The base factory is
    /// left untouched so multiple tests can run with different fakes.
    /// </summary>
    public SgvWebApplicationFactory WithCargoApiClient(FakeCargoApiClient fake)
        => _baseFactory.WithOverrides(cargoApiClient: fake);

    /// <summary>
    /// Seeds the fake catalog ids used by the Create page tests.
    /// </summary>
    public static readonly Guid JuniorNivelId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Seeds the fake catalog ids used by the Create page tests.
    /// </summary>
    public static readonly Guid SeniorNivelId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>
    /// Builds a fresh <see cref="CargoDto"/> with random id and nivel id,
    /// useful when a test only cares about the data shape.
    /// </summary>
    public static CargoDto BuildCargoDto(string codigo, string nombre, string? descripcion, string? nivelNombre)
        => new(Guid.NewGuid(), codigo, nombre, descripcion, Guid.NewGuid(), nivelNombre);

    /// <summary>
    /// Returns an authenticated <see cref="HttpClient"/> whose
    /// <see cref="ICargoApiClient"/> resolves to <paramref name="apiClient"/>.
    /// The auth API is stubbed to return a fixed bearer token.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(FakeCargoApiClient apiClient)
    {
        var authHandler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse("token-123", DateTimeOffset.UtcNow.AddHours(1)))
            });

        var factory = _baseFactory.WithOverrides(
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

    /// <summary>
    /// Extracts the antiforgery token rendered in a <c>__RequestVerificationToken</c>
    /// hidden input. Fails the test if the token is not present.
    /// </summary>
    public static async Task<string> ExtractAntiforgeryTokenAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(content, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        Assert.True(match.Success, "Antiforgery token was not rendered.");
        return match.Groups[1].Value;
    }

    public void Dispose() => _baseFactory?.Dispose();

    /// <summary>
    /// Minimal <see cref="HttpMessageHandler"/> that always returns a
    /// preconfigured <see cref="HttpResponseMessage"/>. Used to stub the
    /// SGV.Api auth endpoint during tests.
    /// </summary>
    public sealed class RecordingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
