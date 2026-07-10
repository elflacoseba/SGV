using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Seguridad;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Seguridad;
using Xunit;

namespace SGV.Tests.Seguridad;

/// <summary>
/// WebApplicationFactory for tests that need real JWT signing and validation,
/// with no fake auth scheme registered. Overrides <c>Jwt:SigningKey</c> via
/// in-memory configuration so each test can pin its own key without
/// touching <c>appsettings.Development.json</c>.
/// </summary>
/// <remarks>
/// Does NOT implement <see cref="IAsyncLifetime"/>: xUnit v2.9.2 only
/// invokes the interface on test classes or registered fixtures, not on
/// instances created with <c>new</c> inside a test body. Callers must
/// invoke <see cref="InitializeAsync"/> explicitly.
/// </remarks>
internal sealed class JwtRealWebApplicationFactory(string signingKey)
    : WebApplicationFactory<SGV.Api.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = signingKey,
            }));

    /// <summary>
    /// Forces host build (so <c>ValidateOnStart</c> runs) and seeds the
    /// minimum role/persona/admin tuple so <c>/api/v1/auth/login</c> can
    /// authenticate. Idempotent: running it twice does not throw.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Triggers WebHost.Build() which runs all IStartupValidator checks,
        // including the JwtOptions validator wired in Program.
        _ = Server;

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgvDbContext>();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<SgvIdentityUser>>();
        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        // 1) Rol (idempotente). DatosSemilla siembra via HasData, pero el check
        //    local evita depender del orden de migracion.
        if (!await roleManager.RoleExistsAsync(RolesSgv.Administrador))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole
            {
                Id = RolesSgv.Administrador,
                Name = RolesSgv.Administrador,
                NormalizedName = RolesSgv.Administrador.ToUpperInvariant(),
            });
            Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
        }

        // 2) Persona previa — PersonaId es FK obligatoria (OnDelete=Restrict) y
        //    Nombres/Apellidos son required; Id debe setearse explicitamente
        //    (ConfigurarId usa ValueGeneratedNever).
        var persona = new PersonaEntity
        {
            Id = Guid.NewGuid(),
            Nombres = "Admin",
            Apellidos = "Seed",
            IsActive = true,
        };
        db.Personas.Add(persona);
        await db.SaveChangesAsync();

        // 3) Admin — UserManager.CreateAsync NO asigna PersonaId, es property
        //    publica de SgvIdentityUser y debe setearse antes.
        if (await userManager.FindByNameAsync("admin") is null)
        {
            var admin = new SgvIdentityUser
            {
                UserName = "admin",
                Email = "admin@test.local",
                EmailConfirmed = true,
                PersonaId = persona.Id,
            };
            var createResult = await userManager.CreateAsync(admin, "Admin#12345");
            Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(e => e.Description)));
            var roleAssign = await userManager.AddToRoleAsync(admin, RolesSgv.Administrador);
            Assert.True(roleAssign.Succeeded, string.Join(", ", roleAssign.Errors.Select(e => e.Description)));
        }
    }
}
