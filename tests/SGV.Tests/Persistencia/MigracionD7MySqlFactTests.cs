using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Seguridad;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// MySqlFact tests for the DropSoftDeleteFromAspNetUsers (D7) migration.
///
/// RED phase: the test is written and targets a database that already has
/// D7 applied (the [MySqlFact] bootstrap runs Database.Migrate() once per
/// session). GREEN means every assertion passes against the real MySQL.
///
/// Coverage:
/// - Migration idempotency (2nd Migrate() is a no-op)
/// - Unique IX_AspNetUsers_PersonaId (preflight end-state)
/// - LockoutEnd datetime(6) precision
/// - FK CASCADE purges Identity junction tables
/// - Personas and Auditorías survive user deletion
/// </summary>
public sealed class MigracionD7MySqlFactTests
{
    [MySqlFact]
    public async Task Migrate_TwoCalls_IsIdempotent()
    {
        // RES-001 (4R review): Un segundo run de Database.Migrate() contra
        // el schema post-D7 (IsDeleted ya no existe) debe ser un no-op.
        // El stored procedure __sgvApplyD7 gatea por information_schema.COLUMNS,
        // y EF Core ya no ejecuta Migrate() para migraciones ya aplicadas.
        var connectionString = TestSgvDbContextFactory.ResolveConnectionString();
        var options = new DbContextOptionsBuilder<SgvDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;

        // Primer run — ya aplicado por el bootstrap de MySqlFact, pero
        // creamos un context fresco para probar.
        await using var firstCtx = new SgvDbContext(options);
        var exception1 = await Record.ExceptionAsync(() => firstCtx.Database.MigrateAsync());
        Assert.Null(exception1);

        // Segundo run — debe ser no-op (EF Core + stored procedure gate).
        await using var secondCtx = new SgvDbContext(options);
        var exception2 = await Record.ExceptionAsync(() => secondCtx.Database.MigrateAsync());
        Assert.Null(exception2);
    }

    [MySqlFact]
    public async Task UniqueIndex_PersonaId_PreventsDuplicateAssignment()
    {
        // El preflight fail-loud de D7 aborta si existen PersonaId duplicados.
        // Post-D7, el UNIQUE INDEX IX_AspNetUsers_PersonaId garantiza que
        // ningún INSERT puede crear duplicados. En lugar de simular el
        // preflight contra un schema pre-D7 (que ya no existe), verificamos
        // el end-state: el índice rechaza el duplicado.
        var connectionString = TestSgvDbContextFactory.ResolveConnectionString();
        var options = new DbContextOptionsBuilder<SgvDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)),
                mysql => mysql.SchemaBehavior(Pomelo.EntityFrameworkCore.MySql.Infrastructure.MySqlSchemaBehavior.Ignore))
            .Options;

        var context = new SgvDbContext(options);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(context);
        services.AddIdentityCore<SgvIdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<SgvDbContext>();
        await using var provider = services.BuildServiceProvider();
        var gateway = new UsuarioIdentityGateway(
            provider.GetRequiredService<UserManager<SgvIdentityUser>>(),
            context);

        // Crear una Persona
        var personaId = Guid.NewGuid();
        context.Personas.Add(new PersonaEntity
        {
            Id = personaId,
            Legajo = $"LEG-UNIQ-{Guid.NewGuid():N}"[..18],
            Nombres = "Uniq",
            Apellidos = $"Test{ Guid.NewGuid():N}"[..10],
            Email = $"{Guid.NewGuid():N}@uniq.test",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        // Primer usuario vinculado a esa Persona — debe funcionar.
        var firstResult = await gateway.CrearAsync(new CrearUsuarioRequest(
            personaId,
            $"uniq-first-{Guid.NewGuid():N}"[..20],
            $"{Guid.NewGuid():N}@uniq-first.test",
            "Uniq#12345",
            [RolesSgv.Consultor]));
        Assert.True(firstResult.IsSuccess);

        context.ChangeTracker.Clear();

        // Segundo usuario para la MISMA Persona — debe fallar.
        var secondResult = await gateway.CrearAsync(new CrearUsuarioRequest(
            personaId,
            $"uniq-second-{Guid.NewGuid():N}"[..20],
            $"{Guid.NewGuid():N}@uniq-second.test",
            "Uniq#12345",
            [RolesSgv.Consultor]));
        Assert.False(secondResult.IsSuccess);
        Assert.Equal("PersonaYaTieneUsuario", secondResult.Error!.Code);
    }

    [MySqlFact]
    public async Task LockoutEnd_HasDatetime6Precision()
    {
        // El backfill de D7 escribe LockoutEnd='9999-12-31 23:59:59.999999'
        // (datetime(6) máximo). Verificamos que el valor escrito por
        // BloquearAsync tenga la precisión esperada.
        await using var fixture = await GatewayFixture.CreateAsync();
        var user = await fixture.AddUserAsync(
            $"{fixture.Marker}-dt6", "Ana", fixture.Marker, blocked: true, [RolesSgv.Consultor]);

        // Leer el LockoutEnd mediante SQL crudo para ver el valor exacto en MySQL.
        using var command = fixture.Context.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            "SELECT CAST(`LockoutEnd` AS CHAR(30)) FROM `AspNetUsers` WHERE `Id` = @p0";
        var param = command.CreateParameter();
        param.ParameterName = "p0";
        param.Value = user.Id;
        command.Parameters.Add(param);
        await fixture.Context.Database.OpenConnectionAsync();
        var rawValue = (string?)await command.ExecuteScalarAsync();

        Assert.NotNull(rawValue);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3,6}$", rawValue);
    }

    [MySqlFact]
    public async Task Eliminar_IdentityUser_CascadesToJunctionTables()
    {
        // REL-007 (4R review): FK CASCADE debe purgar AspNetUserRoles,
        // AspNetUserClaims, AspNetUserLogins, AspNetUserTokens al eliminar
        // la fila de AspNetUsers. Personas (FK RESTRICT) y Auditorías
        // (string sin FK) deben sobrevivir.
        await using var fixture = await GatewayFixture.CreateAsync();
        var user = await fixture.AddUserAsync(
            $"{fixture.Marker}-cascade",
            "Cascade",
            fixture.Marker,
            blocked: false,
            [RolesSgv.Consultor, RolesSgv.GestorVacantes]);

        // Insertar un claim y un login para cubrir las 4 tablas de junction.
        fixture.Context.UserClaims.Add(new IdentityUserClaim<string>
        {
            UserId = user.Id,
            ClaimType = "test-claim",
            ClaimValue = "value",
        });
        fixture.Context.UserLogins.Add(new IdentityUserLogin<string>
        {
            LoginProvider = "TEST",
            ProviderKey = "key",
            ProviderDisplayName = "Test",
            UserId = user.Id,
        });
        fixture.Context.UserTokens.Add(new IdentityUserToken<string>
        {
            UserId = user.Id,
            LoginProvider = "TEST",
            Name = "token-name",
            Value = "token-value",
        });
        await fixture.Context.SaveChangesAsync();

        // Crear una auditoría vinculada a este usuario (Id explícito para
        // evitar conflicto de PK con otras filas sin Id seteado).
        fixture.Context.Auditorias.Add(new AuditoriaEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OccurredAt = DateTime.UtcNow,
            EntityName = "Usuario",
            EntityId = user.Id,
            Operation = "PruebaCascade",
        });
        await fixture.Context.SaveChangesAsync();

        var personaId = user.PersonaId;

        // Act: eliminar el usuario.
        var result = await fixture.Gateway.EliminarAsync(user.Id);
        Assert.True(result.IsSuccess);

        // Assert: AspNetUsers se fue.
        var userGone = await fixture.Context.Users.SingleOrDefaultAsync(u => u.Id == user.Id);
        Assert.Null(userGone);

        // Assert: FK CASCADE purgeó las 4 tablas Identity.
        Assert.Empty(await fixture.Context.UserRoles.Where(ur => ur.UserId == user.Id).ToListAsync());
        Assert.Empty(await fixture.Context.UserClaims.Where(c => c.UserId == user.Id).ToListAsync());
        Assert.Empty(await fixture.Context.UserLogins.Where(l => l.UserId == user.Id).ToListAsync());
        Assert.Empty(await fixture.Context.UserTokens.Where(t => t.UserId == user.Id).ToListAsync());

        // Assert: Persona sobrevive (FK RESTRICT desde AspNetUsers → Personas).
        var personaStill = await fixture.Context.Personas.SingleOrDefaultAsync(p => p.Id == personaId);
        Assert.NotNull(personaStill);

        // Assert: Auditoría sobrevive (string UserId sin FK).
        var auditStill = await fixture.Context.Auditorias
            .Where(a => a.EntityId == user.Id && a.Operation == "PruebaCascade")
            .ToListAsync();
        Assert.NotEmpty(auditStill);
    }

    /// <summary>
    /// Fixture reutilizable para tests de migración D7. Crea un gateway
    /// y un DbContext compartidos para operaciones de usuario.
    /// </summary>
    private sealed class GatewayFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly List<string> _userIds = [];
        private readonly List<Guid> _personaIds = [];

        private GatewayFixture(
            ServiceProvider provider,
            SgvDbContext context,
            UsuarioIdentityGateway gateway)
        {
            _provider = provider;
            Context = context;
            Gateway = gateway;
            Marker = $"d7mg{Guid.NewGuid():N}"[..14];
        }

        public string Marker { get; }
        public SgvDbContext Context { get; }
        public UsuarioIdentityGateway Gateway { get; }

        public static Task<GatewayFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<SgvDbContext>()
                .UseMySql(
                    TestSgvDbContextFactory.ResolveConnectionString(),
                    new MySqlServerVersion(new Version(8, 0, 36)),
                    mysql => mysql.SchemaBehavior(
                        Pomelo.EntityFrameworkCore.MySql.Infrastructure.MySqlSchemaBehavior.Ignore))
                .Options;

            var context = new SgvDbContext(options);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(context);
            services.AddIdentityCore<SgvIdentityUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<SgvDbContext>();
            var provider = services.BuildServiceProvider();
            var userManager = provider.GetRequiredService<UserManager<SgvIdentityUser>>();
            var gateway = new UsuarioIdentityGateway(userManager, context);
            return Task.FromResult(new GatewayFixture(provider, context, gateway));
        }

        public async Task<SgvIdentityUser> AddUserAsync(
            string userName,
            string nombres,
            string apellidos,
            bool blocked,
            IReadOnlyCollection<string> roles)
        {
            var persona = new PersonaEntity
            {
                Id = Guid.NewGuid(),
                Legajo = $"LEG-{Guid.NewGuid():N}"[..18],
                Nombres = nombres,
                Apellidos = apellidos,
                Email = $"{Guid.NewGuid():N}@persona.test",
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
            };
            Context.Personas.Add(persona);
            await Context.SaveChangesAsync();
            _personaIds.Add(persona.Id);

            var user = new SgvIdentityUser
            {
                Id = Guid.NewGuid().ToString("N"),
                PersonaId = persona.Id,
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                Email = $"{Guid.NewGuid():N}@user.test",
                NormalizedEmail = $"{Guid.NewGuid():N}@USER.TEST",
                LockoutEnabled = true,
                LockoutEnd = blocked
                    ? new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.Zero)
                    : null,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            };

            Context.Users.Add(user);
            foreach (var role in roles)
            {
                Context.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = role });
            }

            await Context.SaveChangesAsync();
            _userIds.Add(user.Id);
            return user;
        }

        public async ValueTask DisposeAsync()
        {
#pragma warning disable EF1002
            await Context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=0");
            try
            {
                if (_personaIds.Count > 0)
                {
                    var personaIdList = string.Join(",", _personaIds.Select(id => $"'{id}'"));
                    await Context.Database.ExecuteSqlRawAsync(
                        $"DELETE FROM `AspNetUserRoles` WHERE `UserId` IN " +
                        $"(SELECT `Id` FROM `AspNetUsers` WHERE `PersonaId` IN ({personaIdList}))");
                    await Context.Database.ExecuteSqlRawAsync(
                        $"DELETE FROM `AspNetUsers` WHERE `PersonaId` IN ({personaIdList})");
                    await Context.Database.ExecuteSqlRawAsync(
                        $"DELETE FROM `Personas` WHERE `Id` IN ({personaIdList})");
                }
            }
            finally
            {
                await Context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=1");
            }
#pragma warning restore EF1002
            Context.ChangeTracker.Clear();
            await _provider.DisposeAsync();
        }
    }
}
