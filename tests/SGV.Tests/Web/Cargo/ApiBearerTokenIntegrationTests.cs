using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// End-to-end test that proves the SGV.Web -> SGV.Api bridge forwards the
/// JWT bearer token on downstream HTTP calls. PR #65 demands an authenticated
/// principal on every cargo endpoint; without bearer propagation, the API
/// rejects every request with 401 and the cargo listing page renders
/// "No se pudo cargar el listado de cargos".
/// </summary>
public sealed class ApiBearerTokenIntegrationTests
{
    [Fact]
    public async Task Get_CargosIndex_WhenAuthenticated_ForwardsBearerTokenToApi()
    {
        // Arrange: a stub auth handler that issues a known JWT, plus a recording
        // handler for the cargo API so we can inspect the outbound request.
        const string expectedJwt = "test-jwt-xyz";
        var authHandler = new StubAuthHandler(expectedJwt);
        var cargoHandler = new RecordingCargoHandler();

        using var factory = new SgvWebApplicationFactory().WithOverrides(
            configureServices: services => services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test"),
            authApiHandler: authHandler,
            cargoApiHandler: cargoHandler);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        // Act: sign in through the Web so the cookie ticket carries the JWT.
        var signInPage = await client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(signInPage);

        var loginResponse = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "Password1!"
        }));

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        // Hit the page that exercises the cargo API client.
        var indexResponse = await client.GetAsync("/organizacion/cargos");
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);

        // Assert: the cargo API call carried the JWT bearer header.
        var cargoRequest = Assert.Single(cargoHandler.Requests);
        Assert.NotNull(cargoRequest.Headers.Authorization);
        Assert.Equal("Bearer", cargoRequest.Headers.Authorization!.Scheme);
        Assert.Equal(expectedJwt, cargoRequest.Headers.Authorization.Parameter);
    }

    private static async Task<string> ExtractAntiforgeryTokenAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(content, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        Assert.True(match.Success, "Antiforgery token was not rendered.");
        return match.Groups[1].Value;
    }

    /// <summary>
    /// Always responds with a successful login payload carrying the test JWT
    /// so the Web issues a cookie ticket that stores it under "access_token".
    /// </summary>
    private sealed class StubAuthHandler(string accessToken) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse(accessToken, new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero)))
            });
    }

    /// <summary>
    /// Captures every outgoing cargo API request and answers with an empty
    /// array so the page renders successfully.
    /// </summary>
    private sealed class RecordingCargoHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<CargoDto>())
            });
        }
    }
}