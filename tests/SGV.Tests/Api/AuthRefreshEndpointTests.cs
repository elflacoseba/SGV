using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Seguridad.Servicios;
using SGV.Contracts.Auth;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Infraestructura.Persistencia;
using SGV.Tests.Integration;
using SGV.Tests.Persistencia;
using SGV.Tests.Seguridad;
using Xunit;

namespace SGV.Tests.Api;

/// <summary>
/// Integration tests for <c>POST /api/v1/auth/refresh</c> and
/// <c>POST /api/v1/auth/logout</c> against real MySQL (PR2a of change
/// <c>implementa-refresh-tokens</c>). Uses
/// <see cref="JwtRealWebApplicationFactory"/> so login mints a real JWT and
/// a real refresh token row.
/// </summary>
[Collection(MySqlIntegrationCollection.Name)]
public sealed class AuthRefreshEndpointTests
{
    private const string SigningKey = "E2E-REFRESH-TEST-MIN-32-BYTES-REQUIRED!!!";
    private const string SeedUser = "admin";
    private const string SeedPassword = "Admin#12345";

    [MySqlFact]
    public async Task Refresh_WithValidToken_Returns200AndRotatesWithinSameFamily()
    {
        var factory = new JwtRealWebApplicationFactory(SigningKey);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();

        var login = await LoginAsync(client);
        Assert.False(string.IsNullOrWhiteSpace(login.RefreshToken));
        Assert.NotNull(login.RefreshTokenExpiresAt);

        var response = await client.PostAsJsonAsync(
            AuthApiRoutes.Refresh,
            new RefreshRequest(login.RefreshToken!));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RefreshResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.NotEqual(login.RefreshToken, body.RefreshToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgvDbContext>();
        var consumido = await FindAsync(db, login.RefreshToken!);
        var rotado = await FindAsync(db, body.RefreshToken);
        Assert.NotNull(consumido!.RevokedAt);
        Assert.Null(rotado!.RevokedAt);
        Assert.Equal(consumido.FamilyId, rotado.FamilyId);
        Assert.Equal(rotado.Id, consumido.ReplacedById);
    }

    [MySqlFact]
    public async Task Refresh_WithUnknownToken_Returns401()
    {
        var factory = new JwtRealWebApplicationFactory(SigningKey);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            AuthApiRoutes.Refresh,
            new RefreshRequest($"never-issued-{Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [MySqlFact]
    public async Task Refresh_WithExpiredToken_Returns401AndLeavesRowUntouched()
    {
        var factory = new JwtRealWebApplicationFactory(SigningKey);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();

        var login = await LoginAsync(client);
        var hash = RefreshTokenHashing.ComputeSha256Hex(login.RefreshToken!);

        await using (var setup = factory.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<SgvDbContext>();
            await db.RefreshTokens
                .Where(r => r.TokenHash == hash)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ExpiresAt, DateTime.UtcNow.AddDays(-1)));
        }

        var response = await client.PostAsJsonAsync(
            AuthApiRoutes.Refresh,
            new RefreshRequest(login.RefreshToken!));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var verify = scope.ServiceProvider.GetRequiredService<SgvDbContext>();
        var row = await FindAsync(verify, login.RefreshToken!);
        // REQ-AUTH-REFRESH-2: expiry alone must not mutate the row.
        Assert.Null(row!.RevokedAt);
        Assert.Null(row.ReplacedById);
    }

    [MySqlFact]
    public async Task Refresh_ReplayingConsumedToken_Returns401AndRevokesWholeFamily()
    {
        var factory = new JwtRealWebApplicationFactory(SigningKey);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();

        var login = await LoginAsync(client);
        var first = await client.PostAsJsonAsync(
            AuthApiRoutes.Refresh,
            new RefreshRequest(login.RefreshToken!));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var rotated = await first.Content.ReadFromJsonAsync<RefreshResponse>();

        var replay = await client.PostAsJsonAsync(
            AuthApiRoutes.Refresh,
            new RefreshRequest(login.RefreshToken!));

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgvDbContext>();
        var rotatedRow = await FindAsync(db, rotated!.RefreshToken);
        Assert.NotNull(rotatedRow!.RevokedAt);

        var familia = rotatedRow.FamilyId;
        Assert.Empty(await db.RefreshTokens
            .AsNoTracking()
            .Where(r => r.FamilyId == familia && r.RevokedAt == null)
            .ToListAsync());

        // REQ-AUTH-REFRESH-3: ExecuteUpdateAsync bypasses the audit
        // interceptor, so the service must write the entry explicitly.
        var auditoria = await db.Auditorias
            .AsNoTracking()
            .Where(a => a.EntityName == "RefreshToken"
                        && a.Operation == "RevocarFamilia"
                        && a.EntityId == familia.ToString())
            .ToListAsync();
        Assert.NotEmpty(auditoria);
        Assert.All(auditoria, entry =>
            Assert.DoesNotContain("TokenHash", entry.NewValuesJson ?? string.Empty, StringComparison.Ordinal));

        // The already-rotated token is dead too: the family is gone.
        var afterRevocation = await client.PostAsJsonAsync(
            AuthApiRoutes.Refresh,
            new RefreshRequest(rotated.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevocation.StatusCode);
    }

    [MySqlFact]
    public async Task Login_Refresh_Refresh_Logout_Chain_KeepsFamilyAndThenRevokesIt()
    {
        var factory = new JwtRealWebApplicationFactory(SigningKey);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();

        var login = await LoginAsync(client);
        var firstRotation = await RefreshAsync(client, login.RefreshToken!);
        var secondRotation = await RefreshAsync(client, firstRotation.RefreshToken);

        Guid familia;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SgvDbContext>();
            var original = await FindAsync(db, login.RefreshToken!);
            var current = await FindAsync(db, secondRotation.RefreshToken);
            familia = original!.FamilyId;
            Assert.Equal(familia, current!.FamilyId);
            Assert.Null(current.RevokedAt);
        }

        using var authenticated = factory.CreateClient();
        authenticated.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", secondRotation.AccessToken);

        var logout = await authenticated.PostAsJsonAsync(
            AuthApiRoutes.Logout,
            new LogoutRequest(secondRotation.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        var logoutBody = await logout.Content.ReadFromJsonAsync<LogoutResponse>();
        Assert.True(logoutBody!.Success);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SgvDbContext>();
            Assert.Empty(await db.RefreshTokens
                .AsNoTracking()
                .Where(r => r.FamilyId == familia && r.RevokedAt == null)
                .ToListAsync());
        }

        var afterLogout = await client.PostAsJsonAsync(
            AuthApiRoutes.Refresh,
            new RefreshRequest(secondRotation.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [MySqlFact]
    public async Task Logout_WithoutAuthentication_Returns401()
    {
        var factory = new JwtRealWebApplicationFactory(SigningKey);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(AuthApiRoutes.Logout, new LogoutRequest(null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [MySqlFact]
    public async Task Logout_WithoutRefreshTokenInBody_Returns200AndRevokesEveryFamily()
    {
        var factory = new JwtRealWebApplicationFactory(SigningKey);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();

        // Two logins ⇒ two families for the same user; a bodyless logout must
        // clear both (REQ-AUTH-LOGOUT-1, legacy-session scenario).
        var first = await LoginAsync(client);
        var second = await LoginAsync(client);

        using var authenticated = factory.CreateClient();
        authenticated.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", second.AccessToken);

        var response = await authenticated.PostAsJsonAsync(AuthApiRoutes.Logout, new LogoutRequest(null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgvDbContext>();
        var firstRow = await FindAsync(db, first.RefreshToken!);
        var secondRow = await FindAsync(db, second.RefreshToken!);
        Assert.NotNull(firstRow!.RevokedAt);
        Assert.NotNull(secondRow!.RevokedAt);
        Assert.NotEqual(firstRow.FamilyId, secondRow.FamilyId);
    }

    private static async Task<LoginResponse> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            AuthApiRoutes.Login,
            new LoginRequest(SeedUser, SeedPassword));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        return body!;
    }

    private static async Task<RefreshResponse> RefreshAsync(HttpClient client, string refreshToken)
    {
        var response = await client.PostAsJsonAsync(AuthApiRoutes.Refresh, new RefreshRequest(refreshToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RefreshResponse>();
        Assert.NotNull(body);
        return body!;
    }

    private static Task<SGV.Infraestructura.Persistencia.Entidades.RefreshTokenEntity?> FindAsync(
        SgvDbContext db,
        string plainToken)
    {
        var hash = RefreshTokenHashing.ComputeSha256Hex(plainToken);
        return db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(r => r.TokenHash == hash);
    }
}
