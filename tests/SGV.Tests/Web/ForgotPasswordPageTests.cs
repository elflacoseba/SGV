using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Common;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web;

[Collection("WebIntegration")]
public sealed class ForgotPasswordPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public ForgotPasswordPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_ForgotPassword_RendersPublicEmailFormWithoutShellChrome()
    {
        await using var factory = CreateFactory(new FakeAuthApiClient());
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/auth/forgot-password");
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Recuperar contraseña", content);
        Assert.Contains("Input_Email", content);
        Assert.Contains("Enviar enlace", content);
        Assert.DoesNotContain("sidebar", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_ForgotPassword_WithValidEmail_ShowsGenericConfirmation()
    {
        var fake = new FakeAuthApiClient();
        await using var factory = CreateFactory(fake);
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var getResponse = await client.GetAsync("/auth/forgot-password");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/auth/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Email"] = "person@example.com"
        }));
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Si el email existe, recibirás un enlace para restablecer tu contraseña", content);
        Assert.Equal("person@example.com", fake.LastForgotRequest?.UserNameOrEmail);
    }

    [Fact]
    public async Task Post_ForgotPassword_WithEmptyEmail_ShowsValidationError()
    {
        await using var factory = CreateFactory(new FakeAuthApiClient());
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var getResponse = await client.GetAsync("/auth/forgot-password");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/auth/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Email"] = string.Empty
        }));
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("El correo electrónico es obligatorio", content);
    }

    [Fact]
    public async Task Post_ForgotPassword_WhenApiReturns429_ShowsRateLimitMessage()
    {
        var fake = new FakeAuthApiClient
        {
            ForgotException = new HttpRequestException(
                "Too many requests",
                inner: null,
                statusCode: HttpStatusCode.TooManyRequests)
        };
        await using var factory = CreateFactory(fake);
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var getResponse = await client.GetAsync("/auth/forgot-password");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/auth/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Email"] = "person@example.com"
        }));
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Hiciste demasiados intentos. Esperá unos minutos antes de volver a intentarlo.", content);
        Assert.Contains("value=\"person@example.com\"", content);
    }

    [Fact]
    public async Task Post_ForgotPassword_WhenApiIsUnavailable_ShowsTransportMessage()
    {
        var fake = new FakeAuthApiClient
        {
            ForgotException = new HttpRequestException("Connection refused")
        };
        await using var factory = CreateFactory(fake);
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var getResponse = await client.GetAsync("/auth/forgot-password");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/auth/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Email"] = "person@example.com"
        }));
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo conectar con el servidor. Verificá tu conexión y volvé a intentar.", content);
    }

    private SgvWebApplicationFactory CreateFactory(FakeAuthApiClient fake)
        => _fixture.RootFactory.WithOverrides(
            configureServices: services =>
            {
                services.RemoveAll<IAuthApiClient>();
                services.AddSingleton<IAuthApiClient>(fake);
            });

    private sealed class FakeAuthApiClient : IAuthApiClient
    {
        public HttpRequestException? ForgotException { get; init; }

        public ForgotPasswordRequest? LastForgotRequest { get; private set; }

        public Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<LoginResponse?>(null);

        public Task<PasswordResetOutcome> ForgotPasswordAsync(
            ForgotPasswordRequest request,
            CancellationToken cancellationToken = default)
        {
            LastForgotRequest = request;
            if (ForgotException is not null)
            {
                return Task.FromException<PasswordResetOutcome>(ForgotException);
            }

            return Task.FromResult(PasswordResetOutcome.Success);
        }

        public Task<PasswordResetOutcome> ResetPasswordAsync(
            ResetPasswordRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(PasswordResetOutcome.Success);

        public Task<PasswordResetOutcome> ValidateResetTokenAsync(
            ValidateResetTokenRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(PasswordResetOutcome.Success);
    }
}
