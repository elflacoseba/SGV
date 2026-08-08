using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Seguridad;
using SGV.Tests.Integration;
using SGV.Tests.Persistencia;
using SGV.Tests.Seguridad;
using Xunit;

namespace SGV.Tests.Api;

/// <summary>
/// MySqlFact end-to-end tests for the UsuariosController API endpoints
/// introduced by change <c>2026-07-15-quita-soft-delete-usuario</c>.
/// Runs against a real MySQL database via <see cref="JwtRealWebApplicationFactory"/>.
///
/// RED phase: tests are written before execution. GREEN means every
/// assertion passes against real MySQL.
///
/// Coverage:
/// - DELETE /api/v1/usuarios/{id} → 204 (admin deleting another user)
/// - POST /api/v1/usuarios/{id}/bloquear → 200
/// - Auto-fence: admin cannot delete/block themselves → 403
/// - Double DELETE → 404
/// </summary>
[Collection(MySqlIntegrationCollection.Name)]
public sealed class UsuariosEndToEndMySqlFactTests
{
    private const string SigningKey = "E2E-API-TEST-MIN-32-BYTES-REQUIRED!!!";
    private const string LoginRoute = "api/v1/auth/login";

    [MySqlFact]
    public async Task Delete_AnotherUser_Returns204()
    {
        await using var factory = await CreateFactoryAsync();
        var (adminToken, _) = await LoginAsAdminAsync(factory);
        var targetUserId = await CreateTargetUserAsync(factory, "del-204");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.DeleteAsync($"/api/v1/usuarios/{targetUserId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [MySqlFact]
    public async Task Bloquear_AnotherUser_Returns200WithBloqueadoTrue()
    {
        await using var factory = await CreateFactoryAsync();
        var (adminToken, _) = await LoginAsAdminAsync(factory);
        var targetUserId = await CreateTargetUserAsync(factory, "bloq-200");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.PostAsync(
            $"/api/v1/usuarios/{targetUserId}/bloquear", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<UsuarioDto>();
        Assert.NotNull(dto);
        Assert.True(dto!.Bloqueado);
    }

    [MySqlFact]
    public async Task Delete_OwnUser_Returns403AutoEliminacion()
    {
        await using var factory = await CreateFactoryAsync();
        var (adminToken, adminUserId) = await LoginAsAdminAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.DeleteAsync($"/api/v1/usuarios/{adminUserId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.Equal("AutoEliminacion", problem!.Title);
    }

    [MySqlFact]
    public async Task Bloquear_OwnUser_Returns403AutoBloqueo()
    {
        await using var factory = await CreateFactoryAsync();
        var (adminToken, adminUserId) = await LoginAsAdminAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.PostAsync(
            $"/api/v1/usuarios/{adminUserId}/bloquear", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.Equal("AutoBloqueo", problem!.Title);
    }

    [MySqlFact]
    public async Task Delete_AlreadyDeletedUser_Returns404()
    {
        await using var factory = await CreateFactoryAsync();
        var (adminToken, _) = await LoginAsAdminAsync(factory);
        var targetUserId = await CreateTargetUserAsync(factory, "404-dbl");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // First delete → 204
        var first = await client.DeleteAsync($"/api/v1/usuarios/{targetUserId}");
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        // Second delete → 404
        var second = await client.DeleteAsync($"/api/v1/usuarios/{targetUserId}");
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    /// <summary>
    /// Crea el factory, lo inicializa (seeds admin + rol) y loguea al admin
    /// para obtener su JWT y su ID.
    /// </summary>
    private static async Task<(string Token, string UserId)> LoginAsAdminAsync(
        JwtRealWebApplicationFactory factory)
    {
        using var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync(
            LoginRoute,
            new LoginRequest("admin", "Admin#12345"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var body = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);

        // Resolver el ID del admin desde UserManager.
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<SgvIdentityUser>>();
        var admin = await userManager.FindByNameAsync("admin");
        Assert.NotNull(admin);

        return (body!.AccessToken, admin!.Id);
    }

    /// <summary>
    /// Crea una Persona + usuario no-admin para usar como target en las
    /// pruebas de eliminación/bloqueo. Retorna el ID del IdentityUser.
    /// </summary>
    private static async Task<string> CreateTargetUserAsync(
        JwtRealWebApplicationFactory factory,
        string prefix)
    {
        var marker = $"{prefix}-{Guid.NewGuid():N}"[..12];

        await using var personaScope = factory.Services.CreateAsyncScope();
        var db = personaScope.ServiceProvider.GetRequiredService<SgvDbContext>();
        var persona = new PersonaEntity
        {
            Id = Guid.NewGuid(),
            Nombres = $"Target{marker}",
            Apellidos = "User",
            IsActive = true,
        };
        db.Personas.Add(persona);
        await db.SaveChangesAsync();

        await using var userScope = factory.Services.CreateAsyncScope();
        var userManager = userScope.ServiceProvider
            .GetRequiredService<UserManager<SgvIdentityUser>>();
        var user = new SgvIdentityUser
        {
            UserName = $"target-{marker}",
            Email = $"{marker}@target.test",
            EmailConfirmed = true,
            PersonaId = persona.Id,
        };
        var createResult = await userManager.CreateAsync(user, "Target#12345");
        Assert.True(createResult.Succeeded,
            string.Join(", ", createResult.Errors.Select(e => e.Description)));

        return user.Id;
    }

    /// <summary>
    /// Crea un factory con clave JWT fija y lo inicializa (Migrate + seed).
    /// </summary>
    private static async Task<JwtRealWebApplicationFactory> CreateFactoryAsync()
    {
        var factory = new JwtRealWebApplicationFactory(signingKey: SigningKey);
        await factory.InitializeAsync();
        return factory;
    }
}
