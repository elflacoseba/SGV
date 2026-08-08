using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Seguridad;
using SGV.Tests.Integration;
using SGV.Tests.Persistencia;
using SGV.Tests.Seguridad;
using Xunit;

namespace SGV.Tests.Api;

/// <summary>
/// Extracts the MySQL-backed <c>ChangePassword</c> SecurityStamp
/// rotation test from <see cref="AuthControllerChangePasswordTests"/>.
/// That class lives in <c>[Collection("ApiIntegration")]</c> and
/// therefore cannot also be serialized into
/// <see cref="MySqlIntegrationCollection"/>; the test that needs a real
/// <see cref="UserManager{TUser}"/> against MySQL (issue #204 / #260)
/// lives here instead so it shares the
/// <see cref="JwtRealWebApplicationFactory"/> boot with the rest of the
/// MySqlFact suite without racing them.
/// </summary>
[Collection(MySqlIntegrationCollection.Name)]
public sealed class AuthControllerSecurityStampMySqlFactTests
{
    private const string SigningKey = "E2E-API-TEST-MIN-32-BYTES-REQUIRED!!!";
    private const string ChangePasswordRoute = "api/v1/auth/change-password";

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

        const string SeedPassword = "Admin#12345";
        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var userManager = setupScope.ServiceProvider
                .GetRequiredService<UserManager<SgvIdentityUser>>();
            var admin = await userManager.FindByNameAsync("admin");
            Assert.NotNull(admin);
            var token = await userManager.GeneratePasswordResetTokenAsync(admin!);
            var reset = await userManager.ResetPasswordAsync(admin!, token, SeedPassword);
            Assert.True(reset.Succeeded, string.Join(", ", reset.Errors.Select(e => e.Description)));
        }

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

        using (var loginClient = factory.CreateClient())
        {
            var loginResponse = await loginClient.PostAsJsonAsync(
                "api/v1/auth/login",
                new LoginRequest("admin", SeedPassword));
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
            var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(loginBody);

            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);

            var response = await client.PostAsJsonAsync(
                $"/{ChangePasswordRoute}",
                new ChangePasswordRequest(SeedPassword, "NewPassword1!", "NewPassword1!"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<SgvIdentityUser>>();
            var adminPost = await userManager.FindByNameAsync("admin");
            Assert.NotNull(adminPost);
            Assert.NotEqual(stampPrevio, adminPost!.SecurityStamp);
        }

        await using (var teardownScope = factory.Services.CreateAsyncScope())
        {
            var userManager = teardownScope.ServiceProvider
                .GetRequiredService<UserManager<SgvIdentityUser>>();
            var admin = await userManager.FindByNameAsync("admin");
            Assert.NotNull(admin);
            var token = await userManager.GeneratePasswordResetTokenAsync(admin!);
            var reset = await userManager.ResetPasswordAsync(admin!, token, SeedPassword);
            Assert.True(reset.Succeeded, string.Join(", ", reset.Errors.Select(e => e.Description)));
        }
    }
}