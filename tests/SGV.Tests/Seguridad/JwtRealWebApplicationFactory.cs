using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using MySqlConnector;
using SGV.Contracts.Seguridad;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Seguridad;
using SGV.Tests.Persistencia;
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
internal class JwtRealWebApplicationFactory(string signingKey)
    : WebApplicationFactory<SGV.Api.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = signingKey,
                ["ConnectionStrings:SgvDatabase"] = TestSgvDbContextFactory.LocalDevConnectionString,
            }));

        builder.ConfigureServices(services =>
        {
            services.AddDbContext<SgvDbContext>(options => options.UseMySql(
                TestSgvDbContextFactory.LocalDevConnectionString,
                new MySqlServerVersion(new Version(8, 0, 36))));
        });
    }

    /// <summary>
    /// Forces host build (so <c>ValidateOnStart</c> runs) and seeds the
    /// minimum role/persona/admin tuple so <c>/api/v1/auth/login</c> can
    /// authenticate. Idempotent AND race-safe: when multiple test classes
    /// run in parallel against the shared <c>sgv_test</c> database, two
    /// concurrent invocations can both pass the <c>FindByNameAsync</c>
    /// check before either calls <c>CreateAsync</c>. The second
    /// <c>CreateAsync</c> must therefore tolerate
    /// <c>IdentityErrorDescriber.DuplicateUserName</c> (code
    /// <c>"DuplicateUserName"</c>) and resolve the just-created user via
    /// <c>FindByNameAsync</c> instead of failing the test host.
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

        var databaseName = new MySqlConnectionStringBuilder(db.Database.GetConnectionString()!).Database;
        Assert.Equal("sgv_test", databaseName);

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

        // 2) Persona previa (idempotente) — PersonaId es FK obligatoria (OnDelete=Restrict) y
        //    Nombres/Apellidos son required; Id debe setearse explicitamente
        //    (ConfigurarId usa ValueGeneratedNever).
        var persona = await db.Personas
            .FirstOrDefaultAsync(p => p.Nombres == "Admin" && p.Apellidos == "Seed");
        if (persona is null)
        {
            persona = new PersonaEntity
            {
                Id = Guid.NewGuid(),
                Nombres = "Admin",
                Apellidos = "Seed",
                IsActive = true,
            };
            db.Personas.Add(persona);
            // Detect concurrent inserts of the same Admin/Seed persona
            // (no UNIQUE constraint on Nombres+Apellidos, so we can't rely
            // on a duplicate-key failure) and re-query instead of throwing.
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                persona = await db.Personas
                    .FirstOrDefaultAsync(p => p.Nombres == "Admin" && p.Apellidos == "Seed");
                Assert.NotNull(persona);
            }
        }

        // 3) Admin — UserManager.CreateAsync NO asigna PersonaId, es property
        //    publica de SgvIdentityUser y debe setearse antes.
        var admin = await userManager.FindByNameAsync("admin");
        if (admin is null)
        {
            var candidate = new SgvIdentityUser
            {
                UserName = "admin",
                Email = "admin@test.local",
                EmailConfirmed = true,
                PersonaId = persona!.Id,
            };
            IdentityResult createResult;
            try
            {
                createResult = await userManager.CreateAsync(candidate, "Admin#12345");
            }
            catch (DbUpdateException du) when (IsDuplicateUserName(du))
            {
                // Identity usually converts this into a Failure result, but
                // when multiple InitializeAsync invocations race on the same
                // schema the UserStore sometimes propagates the raw
                // DbUpdateException. Treat it as a benign race and re-query.
                createResult = IdentityResult.Failed(new IdentityErrorDescriber().DuplicateUserName(candidate.UserName));
            }

            if (!createResult.Succeeded)
            {
                // Race resolution: another InitializeAsync invocation
                // committed the admin between our FindByNameAsync and our
                // CreateAsync. Identity surfaces this as DuplicateUserName.
                // Re-query and continue instead of failing the whole host.
                var duplicate = createResult.Errors.Any(e =>
                    string.Equals(e.Code, "DuplicateUserName", StringComparison.Ordinal));
                Assert.True(duplicate,
                    "CreateAsync(admin) failed for a non-duplicate reason: " +
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));

                admin = await userManager.FindByNameAsync("admin");
                Assert.NotNull(admin);
            }
            else
            {
                admin = candidate;
            }

            if (!await userManager.IsInRoleAsync(admin!, RolesSgv.Administrador))
            {
                var roleAssign = await userManager.AddToRoleAsync(admin!, RolesSgv.Administrador);
                Assert.True(roleAssign.Succeeded,
                    string.Join(", ", roleAssign.Errors.Select(e => e.Description)));
            }
        }
    }

    /// <summary>
    /// Detects whether a <see cref="DbUpdateException"/> originated from a
    /// unique-key violation on <c>AspNetUsers.UserNameIndex</c>. Pomelo +
    /// MySQL surface this as <c>MySqlException</c> with number 1062.
    /// </summary>
    private static bool IsDuplicateUserName(DbUpdateException exception)
    {
        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is MySqlException { Number: 1062 } my)
            {
                return my.Message.Contains("UserNameIndex", StringComparison.OrdinalIgnoreCase)
                    || my.Message.Contains("NormalizedUserName", StringComparison.OrdinalIgnoreCase);
            }
        }
        return false;
    }
}
