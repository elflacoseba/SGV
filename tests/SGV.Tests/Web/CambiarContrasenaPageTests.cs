using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Common;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web;

[Collection("WebIntegration")]
public sealed class CambiarContrasenaPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public CambiarContrasenaPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_CambiarContrasenaAnonymous_RedirectsToSignIn()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync("/auth/cambiar-contrasena");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_CambiarContrasenaAuthenticated_RendersFormWithPasswordBar()
    {
        await using var lease = await CreateAuthenticatedLeaseAsync(new FakeAuthApiClient());

        var response = await lease.Client.GetAsync("/auth/cambiar-contrasena");
        var content = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Cambiar contraseña", content);
        Assert.Contains("name=\"Input.CurrentPassword\"", content);
        Assert.Contains("name=\"Input.NewPassword\"", content);
        Assert.Contains("name=\"Input.ConfirmPassword\"", content);
        Assert.Contains("data-password=\"bar\"", content);
        Assert.Contains("/js/pages/auth-password.js", content);
    }

    [Fact]
    public async Task Post_CambiarContrasenaWithValidPassword_SignsOutAndRedirectsToSignIn()
    {
        var fake = new FakeAuthApiClient
        {
            ChangePasswordOutcome = ChangePasswordOutcome.Success
        };
        await using var lease = await CreateAuthenticatedLeaseAsync(fake);

        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(
            await lease.Client.GetAsync("/auth/cambiar-contrasena"));

        var response = await lease.Client.PostAsync("/auth/cambiar-contrasena", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.CurrentPassword"] = "Old1Pass!",
            ["Input.NewPassword"] = "New2Pass!",
            ["Input.ConfirmPassword"] = "New2Pass!"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/auth/sign-in", response.Headers.Location?.OriginalString);
        Assert.NotNull(fake.LastChangePasswordRequest);
        Assert.Equal("Old1Pass!", fake.LastChangePasswordRequest!.CurrentPassword);
        Assert.Equal("New2Pass!", fake.LastChangePasswordRequest.NewPassword);
        Assert.Equal("New2Pass!", fake.LastChangePasswordRequest.ConfirmPassword);
    }

    [Fact]
    public async Task Post_CambiarContrasenaWithInvalidCurrentPassword_ShowsError()
    {
        var fake = new FakeAuthApiClient
        {
            ChangePasswordOutcome = ChangePasswordOutcome.InvalidCurrentPassword
        };
        await using var lease = await CreateAuthenticatedLeaseAsync(fake);

        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(
            await lease.Client.GetAsync("/auth/cambiar-contrasena"));

        var response = await lease.Client.PostAsync("/auth/cambiar-contrasena", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.CurrentPassword"] = "WrongOld!",
            ["Input.NewPassword"] = "New2Pass!",
            ["Input.ConfirmPassword"] = "New2Pass!"
        }));
        var content = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200 OK but got {(int)response.StatusCode}. Body: {content}");
        Assert.Contains("La contraseña actual no es correcta", content);
        Assert.NotNull(fake.LastChangePasswordRequest);
    }

    [Fact]
    public async Task Post_CambiarContrasenaWhenApiReturns429_ShowsRateLimitMessage()
    {
        var fake = new FakeAuthApiClient
        {
            ChangePasswordException = new HttpRequestException(
                "Too many requests",
                inner: null,
                statusCode: HttpStatusCode.TooManyRequests)
        };
        await using var lease = await CreateAuthenticatedLeaseAsync(fake);

        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(
            await lease.Client.GetAsync("/auth/cambiar-contrasena"));

        var response = await lease.Client.PostAsync("/auth/cambiar-contrasena", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.CurrentPassword"] = "Old1Pass!",
            ["Input.NewPassword"] = "New2Pass!",
            ["Input.ConfirmPassword"] = "New2Pass!"
        }));
        var content = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200 OK but got {(int)response.StatusCode}. Body: {content}");
        Assert.Contains("Hiciste demasiados intentos", content);
    }

    [Fact]
    public async Task Post_CambiarContrasenaWhenApiReturns401_RedirectsToSignIn()
    {
        var fake = new FakeAuthApiClient
        {
            ChangePasswordException = new HttpRequestException(
                "Unauthorized",
                inner: null,
                statusCode: HttpStatusCode.Unauthorized)
        };
        await using var lease = await CreateAuthenticatedLeaseAsync(fake);

        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(
            await lease.Client.GetAsync("/auth/cambiar-contrasena"));

        var response = await lease.Client.PostAsync("/auth/cambiar-contrasena", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.CurrentPassword"] = "Old1Pass!",
            ["Input.NewPassword"] = "New2Pass!",
            ["Input.ConfirmPassword"] = "New2Pass!"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/auth/sign-in", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Post_CambiarContrasenaWhenApiThrowsHttpRequestException_ShowsTransportError()
    {
        var fake = new FakeAuthApiClient
        {
            ChangePasswordException = new HttpRequestException("API unavailable")
        };
        await using var lease = await CreateAuthenticatedLeaseAsync(fake);

        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(
            await lease.Client.GetAsync("/auth/cambiar-contrasena"));

        var response = await lease.Client.PostAsync("/auth/cambiar-contrasena", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.CurrentPassword"] = "Old1Pass!",
            ["Input.NewPassword"] = "New2Pass!",
            ["Input.ConfirmPassword"] = "New2Pass!"
        }));
        var content = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "No se pudo conectar con el servidor. Verificá tu conexión y volvé a intentar.",
            content);
        Assert.NotNull(fake.LastChangePasswordRequest);
    }

    [Fact]
    public async Task Post_CambiarContrasenaWhenApiTimesOut_ShowsTimeoutMessage()
    {
        var fake = new FakeAuthApiClient
        {
            ChangePasswordException = new TaskCanceledException("Request timed out")
        };
        await using var lease = await CreateAuthenticatedLeaseAsync(fake);

        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(
            await lease.Client.GetAsync("/auth/cambiar-contrasena"));

        var response = await lease.Client.PostAsync("/auth/cambiar-contrasena", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.CurrentPassword"] = "Old1Pass!",
            ["Input.NewPassword"] = "New2Pass!",
            ["Input.ConfirmPassword"] = "New2Pass!"
        }));
        var content = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "El servidor tardó demasiado en responder. Volvé a intentar en unos segundos.",
            content);
        Assert.NotNull(fake.LastChangePasswordRequest);
    }

    [Fact]
    public async Task Post_CambiarContrasenaWithMismatchedPasswords_ShowsValidationError()
    {
        var fake = new FakeAuthApiClient();
        await using var lease = await CreateAuthenticatedLeaseAsync(fake);

        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(
            await lease.Client.GetAsync("/auth/cambiar-contrasena"));

        var response = await lease.Client.PostAsync("/auth/cambiar-contrasena", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.CurrentPassword"] = "Old1Pass!",
            ["Input.NewPassword"] = "New2Pass!",
            ["Input.ConfirmPassword"] = "New3Pass!"
        }));
        var content = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Las contraseñas no coinciden", content);
        Assert.Null(fake.LastChangePasswordRequest);
    }

    private Task<WebClientLease> CreateAuthenticatedLeaseAsync(FakeAuthApiClient fake)
        => _fixture.CreateLeaseWithBootstrapAsync(
            f => f.WithOverrides(
                configureServices: services =>
                {
                    services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test");
                    services.Configure<JwtOptions>(o =>
                    {
                        o.SigningKey = AdminJwtTestHelper.SigningKey;
                        o.Issuer = AdminJwtTestHelper.Issuer;
                        o.Audience = AdminJwtTestHelper.Audience;
                    });
                    services.RemoveAll<IAuthApiClient>();
                    services.AddSingleton<IAuthApiClient>(fake);
                }),
            WebIntegrationFixture.AuthenticateClientAsync);

    /// <summary>
    /// FakeAuthApiClient simula un login válido para que el bootstrap de
    /// autenticación pueda emitir la cookie, y además controla los outcomes
    /// de <c>ChangePasswordAsync</c> por test. Maneja todos los métodos de
    /// la interfaz para satisfacer el contrato completo y permitir aislar
    /// el comportamiento del flujo de cambio de contraseña.
    /// </summary>
    private sealed class FakeAuthApiClient : IAuthApiClient
    {
        public ChangePasswordOutcome ChangePasswordOutcome { get; init; } = ChangePasswordOutcome.Success;

        public Exception? ChangePasswordException { get; init; }

        public ChangePasswordRequest? LastChangePasswordRequest { get; private set; }

        public Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<LoginResponse?>(new LoginResponse(
                AdminJwtTestHelper.BuildUserJwt(),
                DateTimeOffset.UtcNow.AddHours(1)));

        public Task<PasswordResetOutcome> ForgotPasswordAsync(
            ForgotPasswordRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(PasswordResetOutcome.Success);

        public Task<PasswordResetOutcome> ResetPasswordAsync(
            ResetPasswordRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(PasswordResetOutcome.Success);

        public Task<PasswordResetOutcome> ValidateResetTokenAsync(
            ValidateResetTokenRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(PasswordResetOutcome.Success);

        public Task<ChangePasswordOutcome> ChangePasswordAsync(
            ChangePasswordRequest request,
            CancellationToken cancellationToken = default)
        {
            LastChangePasswordRequest = request;
            if (ChangePasswordException is not null)
            {
                return Task.FromException<ChangePasswordOutcome>(ChangePasswordException);
            }

            return Task.FromResult(ChangePasswordOutcome);
        }

        public Task<RefreshResponse?> RefreshAsync(
            RefreshRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<RefreshResponse?>(null);

        public Task<bool> LogoutAsync(
            LogoutRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}