using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Seguridad;
using SGV.Tests.Persistencia;
using Xunit;

namespace SGV.Tests.Seguridad;

/// <summary>
/// Regression guard for the lockout-based login rejection introduced by
/// change <c>2026-07-15-quita-soft-delete-usuario</c>. The soft-delete
/// column <c>IsDeleted</c> was retired in favor of ASP.NET Core Identity
/// <c>LockoutEnd</c>; <see cref="AuthServicio.LoginAsync"/> now invokes
/// <see cref="UserManager{TSelf}.IsLockedOutAsync"/> before checking
/// credentials, mirroring what <see cref="SignInManager{TSelf}.PasswordSignInAsync"/>
/// does internally.
/// </summary>
/// <remarks>
/// Uses <see cref="JwtRealWebApplicationFactory"/> (real JWT signing,
/// real MySQL persistence) so the regression also covers the full
/// HTTP request path through <c>AuthController</c>.
/// </remarks>
public sealed class SoftDeletedUserLoginTests
{
    private const string SigningKey = "SOFTDELETE-LOGIN-TEST-MIN-32-BYTES!!";
    private const string LoginRelative = "api/v1/auth/login";

    [MySqlFact]
    public async Task Login_WithLockedOutUser_Returns401AndDoesNotIssueToken()
    {
        await using var factory = new JwtRealWebApplicationFactory(signingKey: SigningKey);
        await factory.InitializeAsync();

        var marker = $"ghost-{Guid.NewGuid():N}"[..16];
        var userName = $"ghost-{marker}";
        var personaId = await SeedPersonaAsync(factory, marker);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<SgvIdentityUser>>();
            var ghost = new SgvIdentityUser
            {
                UserName = userName,
                Email = $"{userName}@test.local",
                EmailConfirmed = true,
                PersonaId = personaId,
            };
            var createResult = await userManager.CreateAsync(ghost, "Ghost#12345");
            Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(e => e.Description)));

            // Lock the user out indefinitely, mirroring what
            // UsuarioIdentityGateway.BloquearAsync does in production
            // (LockoutEnd = sentinel "very far in the future").
            var lockoutResult = await userManager.SetLockoutEndDateAsync(
                ghost, BloquearFechaFuturo());
            Assert.True(lockoutResult.Succeeded);
            ghost.LockoutEnabled = true;
            await userManager.UpdateAsync(ghost);
        }

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            LoginRelative,
            new LoginRequest(userName, "Ghost#12345"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [MySqlFact]
    public async Task Login_WithLockedOutUserByEmail_Returns401AndDoesNotIssueToken()
    {
        await using var factory = new JwtRealWebApplicationFactory(signingKey: SigningKey);
        await factory.InitializeAsync();

        var marker = $"zombie-{Guid.NewGuid():N}"[..16];
        var userName = $"zombie-{marker}";
        var email = $"{userName}@test.local";
        var personaId = await SeedPersonaAsync(factory, marker);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<SgvIdentityUser>>();
            var zombie = new SgvIdentityUser
            {
                UserName = userName,
                Email = email,
                EmailConfirmed = true,
                PersonaId = personaId,
            };
            var createResult = await userManager.CreateAsync(zombie, "Zombie#12345");
            Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(e => e.Description)));

            var lockoutResult = await userManager.SetLockoutEndDateAsync(
                zombie, BloquearFechaFuturo());
            Assert.True(lockoutResult.Succeeded);
            zombie.LockoutEnabled = true;
            await userManager.UpdateAsync(zombie);
        }

        using var client = factory.CreateClient();
        // Use email instead of user name to exercise the
        // FindByEmailAsync branch of LoginAsync.
        var response = await client.PostAsJsonAsync(
            LoginRelative,
            new LoginRequest(email, "Zombie#12345"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [MySqlFact]
    public async Task Login_WithUnlockedUser_AfterPreviousLockout_Returns200AndIssuesToken()
    {
        // Triangulate: same user, locked out then unlocked, must be able
        // to log back in. The fix should only block locked-out users, not
        // permanently revoke access.
        await using var factory = new JwtRealWebApplicationFactory(signingKey: SigningKey);
        await factory.InitializeAsync();

        var marker = $"revive-{Guid.NewGuid():N}"[..16];
        var userName = $"revive-{marker}";
        var personaId = await SeedPersonaAsync(factory, marker);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<SgvIdentityUser>>();
            var user = new SgvIdentityUser
            {
                UserName = userName,
                Email = $"{userName}@test.local",
                EmailConfirmed = true,
                PersonaId = personaId,
            };
            var createResult = await userManager.CreateAsync(user, "Revive#12345");
            Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(e => e.Description)));

            // Lockout then unlock (DesbloquearAsync in production).
            await userManager.SetLockoutEndDateAsync(user, BloquearFechaFuturo());
            user.LockoutEnabled = true;
            await userManager.UpdateAsync(user);

            var refreshed = await userManager.FindByIdAsync(user.Id);
            Assert.NotNull(refreshed);
            var unlockResult = await userManager.SetLockoutEndDateAsync(refreshed!, null);
            Assert.True(unlockResult.Succeeded);
            await userManager.UpdateAsync(refreshed!);
        }

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            LoginRelative,
            new LoginRequest(userName, "Revive#12345"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.True(body.ExpiresAt > DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Sentinel lockout date matching the production BloquearAsync
    /// behavior: datetime(6) maximum minus 1 second. Avoids the
    /// DateTimeOffset.MaxValue 7th-fraction overflow observed during
    /// the design review (see Engram #1134 / #1135).
    /// </summary>
    private static DateTimeOffset BloquearFechaFuturo()
        => new(9999, 12, 31, 23, 59, 59, TimeSpan.Zero);

    [MySqlFact]
    public async Task Login_AfterFiveFailedAttempts_EvenCorrectPasswordReturns401()
    {
        // RIS-001 (4R review): LockoutOptions (5 intentos) +
        // AccessFailedAsync en AuthServicio.LoginAsync. Tras 5 intentos
        // fallidos IsLockedOutAsync=true debe bloquear también la password
        // correcta.
        await using var factory = new JwtRealWebApplicationFactory(signingKey: SigningKey);
        await factory.InitializeAsync();

        var marker = $"lockout-{Guid.NewGuid():N}"[..16];
        var userName = $"alice-{marker}";
        var validPassword = $"Pass#1{Guid.NewGuid():N}"[..20];
        var personaId = await SeedPersonaAsync(factory, marker);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<SgvIdentityUser>>();
            var createResult = await userManager.CreateAsync(new SgvIdentityUser
            {
                UserName = userName,
                Email = $"{userName}@test.local",
                EmailConfirmed = true,
                PersonaId = personaId,
            }, validPassword);
            Assert.True(createResult.Succeeded,
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }

        using var client = factory.CreateClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await client.PostAsJsonAsync(LoginRelative,
                    new LoginRequest(userName, "wrong-attempt-X9"))).StatusCode);
        }
        // 6° intento: password correcta pero cuenta lockeada → 401.
        var correctAttempt = await client.PostAsJsonAsync(LoginRelative,
            new LoginRequest(userName, validPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, correctAttempt.StatusCode);

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyManager = verifyScope.ServiceProvider
            .GetRequiredService<UserManager<SgvIdentityUser>>();
        var tracked = await verifyManager.FindByNameAsync(userName);
        Assert.NotNull(tracked);
        Assert.True(await verifyManager.IsLockedOutAsync(tracked!),
            "Cuenta debe estar lockeada tras 5 intentos fallidos.");
    }

    private static async Task<Guid> SeedPersonaAsync(JwtRealWebApplicationFactory factory, string marker)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgvDbContext>();
        var persona = new PersonaEntity
        {
            Id = Guid.NewGuid(),
            Nombres = $"Ghost{marker}",
            Apellidos = "Seed",
            IsActive = true,
        };
        db.Personas.Add(persona);
        await db.SaveChangesAsync();
        return persona.Id;
    }
}