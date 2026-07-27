using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Seguridad.PasswordChange;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Seguridad;
using SGV.Tests.Api.Collections;
using SGV.Tests.Persistencia;
using SGV.Tests.Seguridad;
using Xunit;

namespace SGV.Tests.Api;

/// <summary>
/// Integration tests for the authenticated password-change endpoint
/// exposed by <c>AuthController.ChangePassword</c> (issue #204 / PR2).
/// Uses <see cref="FakeChangePasswordService"/> for the controller
/// mapping (Success / InvalidCurrentPassword / ValidationError /
/// RateLimited) and the real <see cref="UserManager{TUser}"/> via
/// <see cref="JwtRealWebApplicationFactory"/> for the
/// <c>SecurityStamp</c> rotation test against MySQL.
/// </summary>
[Collection("ApiIntegration")]
public sealed class AuthControllerChangePasswordTests
{
    private const string SigningKey = "E2E-API-TEST-MIN-32-BYTES-REQUIRED!!!";
    private const string ChangePasswordRoute = "api/v1/auth/change-password";

    private readonly ApiIntegrationFixture _fixture;

    public AuthControllerChangePasswordTests(ApiIntegrationFixture fixture) => _fixture = fixture;

    private async Task<ApiWebApplicationFactory> BuildFactoryAsync(
        Func<ChangePasswordRequest, ChangePasswordOutcome>? overrideOutcome = null)
    {
        var fake = new FakeChangePasswordService();
        if (overrideOutcome is not null)
        {
            fake.Override = overrideOutcome;
        }

        var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IChangePasswordService>();
            services.AddSingleton<IChangePasswordService>(fake);
        });
        await Task.CompletedTask;
        return factory;
    }

    [Fact]
    public async Task ChangePassword_NoAuthHeader_Returns401()
    {
        await using var factory = await BuildFactoryAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/{ChangePasswordRoute}",
            new ChangePasswordRequest("valid", "NewPassword1!", "NewPassword1!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_InvalidCurrentPassword_Returns400WithSpanishMessage()
    {
        await using var factory = await BuildFactoryAsync();
        var client = factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync(
            $"/{ChangePasswordRoute}",
            new ChangePasswordRequest("wrong", "NewPassword1!", "NewPassword1!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("contraseña actual", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChangePassword_WeakNewPassword_Returns400()
    {
        await using var factory = await BuildFactoryAsync();
        var client = factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync(
            $"/{ChangePasswordRoute}",
            new ChangePasswordRequest("valid", "short", "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_SixthRequestWithinWindow_Returns429WithRetryAfter()
    {
        // The "ChangePassword" named policy allows 5 requests per 15
        // minutes. The fake auth scheme maps all admin clients to the
        // same subject so the bucket is shared per test.
        await using var factory = await BuildFactoryAsync();
        var client = factory.CreateAdminClient();

        for (var i = 0; i < 5; i++)
        {
            var ok = await client.PostAsJsonAsync(
                $"/{ChangePasswordRoute}",
                new ChangePasswordRequest("valid", "NewPassword1!", "NewPassword1!"));
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        var blocked = await client.PostAsJsonAsync(
            $"/{ChangePasswordRoute}",
            new ChangePasswordRequest("valid", "NewPassword1!", "NewPassword1!"));

        Assert.Equal(
            (HttpStatusCode)StatusCodes.Status429TooManyRequests,
            blocked.StatusCode);
        Assert.True(blocked.Headers.Contains("Retry-After"),
            "Expected Retry-After header on rejected request.");
    }

    [Fact]
    public async Task ChangePassword_Success_Returns200WithSpanishMessage()
    {
        // Mirror of the 200+stamp test below but WITHOUT the DB
        // round-trip — useful as a fast signal when MySQL is not
        // available. Builds on the fake service's Success branch.
        await using var factory = await BuildFactoryAsync();
        var client = factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync(
            $"/{ChangePasswordRoute}",
            new ChangePasswordRequest("valid", "NewPassword1!", "NewPassword1!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("contraseña", body, StringComparison.OrdinalIgnoreCase);
    }

    [MySqlFact]
    public async Task ChangePassword_Success_RotatesSecurityStampAgainstMySql()
    {
        // Issue #204 / spec scenario: tras un POST exitoso el
        // SecurityStamp en AspNetUsers MUST ser distinto del previo.
        // Usa el factory real (no fake) para que el endpoint ejecute
        // la cadena UserManager.ChangePasswordAsync +
        // UpdateSecurityStampAsync contra MySQL.
        //
        // El test muta la contraseña del admin sembrado; lo restaura
        // al final para no romper otras suites MySqlFact que dependen
        // del seed "Admin#12345".
        var factory = new JwtRealWebApplicationFactory(signingKey: SigningKey);
        await factory.InitializeAsync();

        // Garantizar que la contraseña vigente al iniciar el test
        // sea "Admin#12345" (idempotente: cubre re-runs).
        const string SeedPassword = "Admin#12345";
        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var userManager = setupScope.ServiceProvider
                .GetRequiredService<UserManager<SgvIdentityUser>>();
            var admin = await userManager.FindByNameAsync("admin");
            Assert.NotNull(admin);
            var token = await userManager.GeneratePasswordResetTokenAsync(admin!);
            var reset = await userManager.ResetPasswordAsync(admin, token, SeedPassword);
            Assert.True(reset.Succeeded, string.Join(", ", reset.Errors.Select(e => e.Description)));
        }

        // Snapshot del SecurityStamp en scope fresca.
        string stampPrevio;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<SgvIdentityUser>>();
            var admin = await userManager.FindByNameAsync("admin");
            Assert.NotNull(admin);
            stampPrevio = admin!.SecurityStamp;
            Assert.False(string.IsNullOrWhiteSpace(stampPrevio));
        }

        // Loguear para emitir un JWT real firmado con SigningKey.
        using (var loginClient = factory.CreateClient())
        {
            var loginResponse = await loginClient.PostAsJsonAsync(
                "api/v1/auth/login",
                new LoginRequest("admin", SeedPassword));
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
            var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(loginBody);

            // POST /change-password con JWT real.
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);

            var response = await client.PostAsJsonAsync(
                $"/{ChangePasswordRoute}",
                new ChangePasswordRequest(SeedPassword, "NewPassword1!", "NewPassword1!"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // Releer el SecurityStamp en OTRA scope fresca para evitar
        // el identity map de EF Core (que devolvería la entity
        // cacheada con el stamp previo).
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<SgvIdentityUser>>();
            var adminPost = await userManager.FindByNameAsync("admin");
            Assert.NotNull(adminPost);
            Assert.NotEqual(stampPrevio, adminPost!.SecurityStamp);
        }

        // Restaurar la contraseña original para no contaminar
        // suites MySqlFact que dependan del seed canónico.
        await using (var teardownScope = factory.Services.CreateAsyncScope())
        {
            var userManager = teardownScope.ServiceProvider
                .GetRequiredService<UserManager<SgvIdentityUser>>();
            var admin = await userManager.FindByNameAsync("admin");
            Assert.NotNull(admin);
            var token = await userManager.GeneratePasswordResetTokenAsync(admin!);
            var reset = await userManager.ResetPasswordAsync(admin, token, SeedPassword);
            Assert.True(reset.Succeeded, string.Join(", ", reset.Errors.Select(e => e.Description)));
        }
    }
}
