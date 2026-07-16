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
/// Regression guard for review finding on PR #148: a user with
/// <c>IsDeleted = true</c> MUST NOT authenticate. The login flow must
/// return 401 instead of issuing a JWT, regardless of whether the
/// credentials are valid.
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
    public async Task Login_WithSoftDeletedUser_Returns401AndDoesNotIssueToken()
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

            // Soft-delete the user directly via DbContext to mirror what
            // UsuarioIdentityGateway.DesactivarAsync does in production.
            var db = scope.ServiceProvider.GetRequiredService<SgvDbContext>();
            var tracked = await db.Users.SingleAsync(u => u.Id == ghost.Id);
            tracked.IsDeleted = true;
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            LoginRelative,
            new LoginRequest(userName, "Ghost#12345"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [MySqlFact]
    public async Task Login_WithSoftDeletedUserByEmail_Returns401AndDoesNotIssueToken()
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

            var db = scope.ServiceProvider.GetRequiredService<SgvDbContext>();
            var tracked = await db.Users.SingleAsync(u => u.Id == zombie.Id);
            tracked.IsDeleted = true;
            await db.SaveChangesAsync();
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
    public async Task Login_WithReactivatedUser_AfterSoftDelete_Returns200AndIssuesToken()
    {
        // Triangulate: same user, deleted then reactivated, must be able
        // to log back in. The fix should only block deleted users, not
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

            var db = scope.ServiceProvider.GetRequiredService<SgvDbContext>();
            var tracked = await db.Users.SingleAsync(u => u.Id == user.Id);
            tracked.IsDeleted = true;
            await db.SaveChangesAsync();

            var refreshed = await db.Users.SingleAsync(u => u.Id == user.Id);
            refreshed.IsDeleted = false;
            await db.SaveChangesAsync();
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