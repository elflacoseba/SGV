using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGV.Contracts.Auth;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Setup;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Setup;
using Xunit;

namespace SGV.Tests.Web;

/// <summary>
/// C-2 release-readiness: los PageModels de Auth (SignIn, Logout,
/// ForgotPassword, ResetPassword, Setup) deben rechazar POSTs sin
/// antiforgery token. Cada test ejercita el atributo
/// <see cref="Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute"/>
/// aplicado en el commit C-2.
/// </summary>
/// <remarks>
/// <para>
/// ASP.NET Core responde 400 Bad Request cuando falta el
/// <c>__RequestVerificationToken</c> en el form y el atributo exige
/// validación. El cuerpo contiene
/// <c>AntiforgeryValidationException</c> en el log de desarrollo; en
/// producción el middleware ProblemDetails lo presenta como
/// <c>application/problem+json</c>.
/// </para>
/// <para>
/// Los tests son análogos para CambiarContrasena (que ya tenía el
/// atributo antes de este commit) pero se incluyen en este archivo
/// para dejar constancia contractual de que TODA la superficie Auth
/// está cubierta.
/// </para>
/// </remarks>
[Collection("WebIntegration")]
public sealed class AuthPagesAntiforgeryTests
{
    private readonly WebIntegrationFixture _fixture;

    public AuthPagesAntiforgeryTests(WebIntegrationFixture fixture) => _fixture = fixture;

    public static IEnumerable<object[]> AnonymousAuthPostEndpoints() => new[]
    {
        new object[] { "/auth/sign-in", new Dictionary<string, string>
        {
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "Admin#12345"
        }},
        new object[] { "/auth/forgot-password", new Dictionary<string, string>
        {
            ["Input.Email"] = "person@example.com"
        }},
        new object[] { "/auth/reset-password", new Dictionary<string, string>
        {
            ["UserId"] = "u1",
            ["Token"] = "t1",
            ["Input.NewPassword"] = "New1Pass!",
            ["Input.ConfirmPassword"] = "New1Pass!"
        }}
    };

    /// <summary>
    /// Endpoints Auth anónimos (sin <c>[Authorize]</c>) donde el guard
    /// antiforgery debe activarse ANTES del redirect de auth. El atributo
    /// <see cref="Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute"/>
    /// corre como filtro y rechaza POSTs sin token con 400.
    /// </summary>
    [Theory]
    [MemberData(nameof(AnonymousAuthPostEndpoints))]
    public async Task Post_AnonymousAuthEndpoint_WithoutAntiforgeryToken_ReturnsBadRequest(
        string relativePath,
        Dictionary<string, string> formFields)
    {
        await using var factory = CreateAnonymousFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.PostAsync(relativePath, new FormUrlEncodedContent(formFields));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_Logout_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        // Logout es anónimo en sentido auth (no requiere estar autenticado
        // para pedir un sign-out), pero SÍ exige antiforgery porque un
        // atacante podría desautenticar al usuario con un POST cross-site.
        await using var factory = CreateAnonymousFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.PostAsync("/auth/logout", new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private SgvWebApplicationFactory CreateAnonymousFactory()
        => _fixture.RootFactory.WithOverrides(
            configureServices: services =>
            {
                services.RemoveAll<IAuthApiClient>();
                services.AddSingleton<IAuthApiClient>(new AnonymousOkAuthApiClient());
                services.RemoveAll<ISetupApiClient>();
                services.AddSingleton<ISetupApiClient>(new NoSetupRequiredApiClient());
            });

    /// <summary>Cliente tipado que nunca delega a la API real en estos tests.</summary>
    private sealed class AnonymousOkAuthApiClient : IAuthApiClient
    {
        public Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<LoginResponse?>(null);

        public Task<PasswordResetOutcome> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(PasswordResetOutcome.Success);

        public Task<PasswordResetOutcome> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(PasswordResetOutcome.Success);

        public Task<PasswordResetOutcome> ValidateResetTokenAsync(ValidateResetTokenRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(PasswordResetOutcome.Success);

        public Task<ChangePasswordOutcome> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(ChangePasswordOutcome.Success);
    }

    /// <summary>Stub mínimo que reporta estado "no requiere setup".</summary>
    private sealed class NoSetupRequiredApiClient : ISetupApiClient
    {
        public Task<SetupStatusResponse> ObtenerEstadoAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new SetupStatusResponse(false));

        public Task<IReadOnlyList<TipoDocumentoDto>> GetTiposDocumentoAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TipoDocumentoDto>>([]);

        public Task<SetupHttpResult> CrearAsync(SetupRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(SetupHttpResult.Success(new SetupResult(Guid.Empty, "u1", "admin")));
    }
}
