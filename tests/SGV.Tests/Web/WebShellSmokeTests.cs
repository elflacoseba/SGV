using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Common;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web;

/// <summary>
/// Smoke tests for the SGV.Web Razor Pages shell.
/// These tests verify anonymous users are redirected to sign-in,
/// authenticated users see the dashboard shell, and logout is exposed.
/// </summary>
[Collection("WebIntegration")]
public sealed class WebShellSmokeTests
{
    private readonly WebIntegrationFixture _fixture;

    public WebShellSmokeTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_Index_WhenAnonymous_RedirectsToSignIn()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        // Act
        var response = await lease.Client.GetAsync("/");

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenAuthenticated_ReturnsDashboardAndLogout()
    {
        await using var lease = await CreateAuthenticatedLeaseAsync();

        var response = await lease.Client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Dashboard", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Logout", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sign In", content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Issue #204 / T-8: el dropdown autenticado debe exponer el ítem
    /// "Cambiar Contraseña" antes del form de logout, y ese ancla debe
    /// apuntar a la Razor Page <c>/auth/cambiar-contrasena</c>.
    /// </summary>
    [Fact]
    public async Task Get_Index_WhenAuthenticated_TopbarExposesCambiarContrasenaItem()
    {
        await using var lease = await CreateAuthenticatedLeaseAsync();

        var response = await lease.Client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El ancla existe, apunta a la página nueva y tiene el texto visible.
        var anchorIndex = content.IndexOf("href=\"/auth/cambiar-contrasena\"", StringComparison.Ordinal);
        Assert.True(anchorIndex >= 0, "El dropdown no contiene el enlace /auth/cambiar-contrasena.");
        Assert.Contains("Cambiar Contraseña", content, StringComparison.Ordinal);

        // El ítem aparece antes del form de logout.
        var logoutIndex = content.IndexOf("Cerrar Sesión", StringComparison.Ordinal);
        Assert.True(logoutIndex > anchorIndex, "El ítem 'Cambiar Contraseña' debe aparecer antes de 'Cerrar Sesión' en el dropdown.");
    }

    /// <summary>
    /// Issue #204 / T-8: el topbar autenticado NO debe mostrar el ítem
    /// "Cambiar Contraseña" para usuarios anónimos (el dropdown autenticado
    /// sólo se renderiza cuando <c>User.Identity?.IsAuthenticated == true</c>).
    /// </summary>
    [Fact]
    public async Task Get_SignIn_WhenAnonymous_TopbarDoesNotExposeCambiarContrasenaItem()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync("/auth/sign-in");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("href=\"/auth/cambiar-contrasena\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Cambiar Contraseña", content, StringComparison.Ordinal);
    }

    private async Task<WebClientLease> CreateAuthenticatedLeaseAsync()
    {
        var fake = new NoOpAuthApiClient();
        return await _fixture.CreateLeaseWithBootstrapAsync(
            f => f.WithOverrides(
                configureServices: services =>
                {
                    services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test");
                    services.Configure<SGV.Contracts.Seguridad.JwtOptions>(o =>
                    {
                        o.SigningKey = AdminJwtTestHelper.SigningKey;
                        o.Issuer = AdminJwtTestHelper.Issuer;
                        o.Audience = AdminJwtTestHelper.Audience;
                    });
                    services.AddSingleton<SGV.Web.Integration.Auth.IAuthApiClient>(fake);
                }),
            WebIntegrationFixture.AuthenticateClientAsync);
    }

    private sealed class NoOpAuthApiClient : SGV.Web.Integration.Auth.IAuthApiClient
    {
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
            => Task.FromResult(ChangePasswordOutcome.Success);
    }
}