using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SGV.Api.Seguridad;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Seguridad;
using SGV.Tests.Persistencia;
using Xunit;

namespace SGV.Tests.Seguridad;

/// <summary>
/// MySqlFact end-to-end test for the JWT corte inmediato (design Q1
/// closure). Verifies that after a user is blocked, the JWT that was
/// valid BEFORE the block immediately returns 401 on the next
/// authenticated request — without waiting for <c>exp</c>.
///
/// RED phase: test written and compiled before execution. GREEN means
/// the corte inmediato works against real MySQL via the full production
/// pipeline (RevalidatorCredenciales + OnTokenValidated hook).
///
/// This test closes the observable Q1 from the change design: "verificar
/// en CI real que OnTokenValidated corre tras la firma del token pero
/// antes de la autorización, con UserManager resuelto desde scope."
/// </summary>
public sealed class JwtCorteInmediatoMySqlFactTests
{
    private const string SigningKey = "CORTE-INMEDIATO-TEST-MIN-32-BYTES!!";
    private const string LoginRoute = "api/v1/auth/login";
    private const string ConsultaRoute = "api/v1/usuarios/consulta?page=1&pageSize=5&status=activas";

    [MySqlFact]
    public async Task BloquearUsuario_InvalidaJwtInmediatamente()
    {
        await using var factory = await CreateFactoryAsync();
        var (adminToken, _) = await LoginAsAdminAsync(factory);
        var (userToken, userId, targetUserName) = await CreateAndLoginTargetUserAsync(factory);

        // Verificar que el JWT funciona ANTES de bloquear
        using var preClient = factory.CreateClient();
        preClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", userToken);
        Assert.Equal(HttpStatusCode.OK,
            (await preClient.GetAsync(ConsultaRoute)).StatusCode);

        // Bloquear al usuario mediante UserManager.
        // LockoutEnabled=true es necesario para que IsLockedOutAsync respete
        // LockoutEnd (sin esto Identity ignora silenciosamente la fecha).
        // SetLockoutEndDateAsync ya invoca UpdateAsync por dentro.
        await using var scope = factory.Services.CreateAsyncScope();
        {
            var um = scope.ServiceProvider
                .GetRequiredService<UserManager<SgvIdentityUser>>();
            var u = await um.FindByIdAsync(userId);
            Assert.NotNull(u);

            u.LockoutEnabled = true;
            var lr = await um.SetLockoutEndDateAsync(
                u, new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.Zero));
            Assert.True(lr.Succeeded,
                $"SetLockoutEndDateAsync(block) falló: {string.Join(", ", lr.Errors.Select(e => e.Description))}");
        }

        // Verificación: revalidator rechaza al usuario bloqueado
        await using var rvScope = factory.Services.CreateAsyncScope();
        {
            var rv = rvScope.ServiceProvider
                .GetRequiredService<IRevalidatorCredenciales>();
            Assert.False(await rv.SigueVigenteAsync(userId));
        }

        // ASSERT PRINCIPAL: JWT previo → 401 tras el bloqueo
        using var postClient = factory.CreateClient();
        postClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", userToken);
        var postResp = await postClient.GetAsync(ConsultaRoute);
        var postBody = await postResp.Content.ReadAsStringAsync();
        Assert.True(
            postResp.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 401 but got {(int)postResp.StatusCode} {postResp.ReasonPhrase}. Body: {postBody}");

        // Admin JWT sigue funcionando (no es el bloqueado)
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);
        Assert.Equal(HttpStatusCode.OK,
            (await adminClient.GetAsync(ConsultaRoute)).StatusCode);

        // Desbloquear → nuevo login funciona
        await using var unlockScope = factory.Services.CreateAsyncScope();
        {
            var um = unlockScope.ServiceProvider
                .GetRequiredService<UserManager<SgvIdentityUser>>();
            var u = await um.FindByIdAsync(userId);
            Assert.NotNull(u);
            var ur = await um.SetLockoutEndDateAsync(u, null);
            Assert.True(ur.Succeeded,
                $"SetLockoutEndDateAsync(unlock) falló: {string.Join(", ", ur.Errors.Select(e => e.Description))}");
        }

        var newLogin = await factory.CreateClient().PostAsJsonAsync(
            LoginRoute,
            new LoginRequest(targetUserName, "Corte#12345"));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        var newToken = (await newLogin.Content.ReadFromJsonAsync<LoginResponse>())!;

        using var newClient = factory.CreateClient();
        newClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", newToken.AccessToken);
        Assert.Equal(HttpStatusCode.OK,
            (await newClient.GetAsync(ConsultaRoute)).StatusCode);

        // Documentación de comportamiento actual: el JWT original emitido
        // antes del bloqueo VUELVE a ser válido tras el desbloqueo porque
        // RevalidatorCredenciales sólo verifica IsLockedOutAsync y existencia
        // del usuario. No hay revocación por SecurityStamp. Cuando se agregue,
        // este test debe actualizarse para esperar 401 aquí en vez de 200.
        using var oldClient = factory.CreateClient();
        oldClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", userToken);
        Assert.Equal(HttpStatusCode.OK,
            (await oldClient.GetAsync(ConsultaRoute)).StatusCode);
    }

    private async Task<(string Token, string UserId, string UserName)> CreateAndLoginTargetUserAsync(
        JwtRealWebApplicationFactory factory)
    {
        var marker = $"corte-{Guid.NewGuid():N}"[..12];
        var userName = $"corte-user-{marker}";
        var password = "Corte#12345";

        await using var ps = factory.Services.CreateAsyncScope();
        var db = ps.ServiceProvider.GetRequiredService<SgvDbContext>();
        var persona = new PersonaEntity
        {
            Id = Guid.NewGuid(),
            Nombres = $"Corte{marker}",
            Apellidos = "User",
            IsActive = true,
        };
        db.Personas.Add(persona);
        await db.SaveChangesAsync();

        await using var us = factory.Services.CreateAsyncScope();
        var um = us.ServiceProvider.GetRequiredService<UserManager<SgvIdentityUser>>();
        var user = new SgvIdentityUser
        {
            UserName = userName,
            Email = $"{marker}@corte.test",
            EmailConfirmed = true,
            PersonaId = persona.Id,
        };
        var cr = await um.CreateAsync(user, password);
        Assert.True(cr.Succeeded, string.Join(", ", cr.Errors.Select(e => e.Description)));

        using var client = factory.CreateClient();
        var loginResp = await client.PostAsJsonAsync(LoginRoute, new LoginRequest(userName, password));
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var body = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        return (body!.AccessToken, user.Id, userName);
    }

    private static async Task<(string Token, string UserId)> LoginAsAdminAsync(
        JwtRealWebApplicationFactory factory)
    {
        using var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync(LoginRoute, new LoginRequest("admin", "Admin#12345"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);

        await using var scope = factory.Services.CreateAsyncScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<SgvIdentityUser>>();
        var admin = await um.FindByNameAsync("admin");
        Assert.NotNull(admin);
        return (body!.AccessToken, admin!.Id);
    }

    private static async Task<JwtRealWebApplicationFactory> CreateFactoryAsync()
    {
        var f = new JwtRealWebApplicationFactory(signingKey: SigningKey);
        await f.InitializeAsync();
        return f;
    }
}
