using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Common;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web;

[Collection("WebIntegration")]
public sealed class ResetPasswordPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public ResetPasswordPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_ResetPasswordWithoutQuery_RendersFormAndPostShowsMissingLinkError()
    {
        await using var factory = CreateFactory(new FakeAuthApiClient());
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var getResponse = await client.GetAsync("/auth/reset-password");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);
        var response = await client.PostAsync("/auth/reset-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["NewPassword"] = "Password1!",
            ["ConfirmPassword"] = "Password1!"
        }));
        var content = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains("Restablecer contraseña", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("El enlace de recuperación es inválido o está incompleto", content);
    }

    [Fact]
    public async Task Get_ResetPasswordWithEncodedQuery_RendersDecodedHiddenValues()
    {
        await using var factory = CreateFactory(new FakeAuthApiClient());
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/auth/reset-password?userId=abc&token=%2Ba%2Fb%3D");
        var content = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"UserId\" value=\"abc\"", content);
        Assert.Contains("name=\"Token\" value=\"+a/b=\"", content);
        Assert.Contains("data-password=\"bar\"", content);
        Assert.Contains("/js/pages/auth-password.js", content);
    }

    [Fact]
    public async Task Post_ResetPasswordWithMismatchedPasswords_ShowsValidationError()
    {
        await using var factory = CreateFactory(new FakeAuthApiClient());
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var getResponse = await client.GetAsync("/auth/reset-password?userId=abc&token=token");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/auth/reset-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["UserId"] = "abc",
            ["Token"] = "token",
            ["Input.NewPassword"] = "Password1!",
            ["Input.ConfirmPassword"] = "Password2!"
        }));
        var content = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Las contraseñas no coinciden", content);
    }

    [Fact]
    public async Task Post_ResetPasswordWithInvalidToken_ShowsControlledError()
    {
        var fake = new FakeAuthApiClient
        {
            ResetException = new HttpRequestException(
                "Invalid token",
                inner: null,
                statusCode: HttpStatusCode.BadRequest)
        };
        await using var factory = CreateFactory(fake);
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var getResponse = await client.GetAsync("/auth/reset-password?userId=abc&token=token");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/auth/reset-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["UserId"] = "abc",
            ["Token"] = "token",
            ["Input.NewPassword"] = "Password1!",
            ["Input.ConfirmPassword"] = "Password1!"
        }));
        var content = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("El link es inválido o expiró. Solicitá uno nuevo.", content);
    }

    [Fact]
    public async Task Post_ResetPasswordWithValidToken_RedirectsToSignIn()
    {
        var fake = new FakeAuthApiClient();
        await using var factory = CreateFactory(fake);
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var getResponse = await client.GetAsync("/auth/reset-password?userId=abc&token=token");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/auth/reset-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["UserId"] = "abc",
            ["Token"] = "token",
            ["Input.NewPassword"] = "Password1!",
            ["Input.ConfirmPassword"] = "Password1!"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/auth/sign-in", response.Headers.Location?.OriginalString);
        Assert.Equal("abc", fake.LastResetRequest?.UserId);
        Assert.Equal("token", fake.LastResetRequest?.Token);
        Assert.Equal("Password1!", fake.LastResetRequest?.NewPassword);
    }

    [Fact]
    public async Task Post_ResetPasswordWhenApiReturns429_ShowsRateLimitMessage()
    {
        var fake = new FakeAuthApiClient
        {
            ResetException = new HttpRequestException(
                "Too many requests",
                inner: null,
                statusCode: HttpStatusCode.TooManyRequests)
        };
        await using var factory = CreateFactory(fake);
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var getResponse = await client.GetAsync("/auth/reset-password?userId=abc&token=token");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/auth/reset-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["UserId"] = "abc",
            ["Token"] = "token",
            ["Input.NewPassword"] = "Password1!",
            ["Input.ConfirmPassword"] = "Password1!"
        }));
        var content = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Hiciste demasiados intentos. Esperá unos minutos antes de volver a intentarlo.", content);
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
        public HttpRequestException? ResetException { get; init; }

        /// <summary>When set, overrides the default valid-token response.</summary>
        public PasswordResetOutcome? ValidateTokenOutcome { get; init; }

        public ResetPasswordRequest? LastResetRequest { get; private set; }

        public ValidateResetTokenRequest? LastValidateRequest { get; private set; }

        public Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<LoginResponse?>(null);

        public Task<PasswordResetOutcome> ForgotPasswordAsync(
            ForgotPasswordRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(PasswordResetOutcome.Success);

        public Task<PasswordResetOutcome> ResetPasswordAsync(
            ResetPasswordRequest request,
            CancellationToken cancellationToken = default)
        {
            LastResetRequest = request;
            if (ResetException is not null)
            {
                return Task.FromException<PasswordResetOutcome>(ResetException);
            }

            return Task.FromResult(PasswordResetOutcome.Success);
        }

        public Task<PasswordResetOutcome> ValidateResetTokenAsync(
            ValidateResetTokenRequest request,
            CancellationToken cancellationToken = default)
        {
            LastValidateRequest = request;
            return Task.FromResult(ValidateTokenOutcome ?? PasswordResetOutcome.Success);
        }
    }
}
