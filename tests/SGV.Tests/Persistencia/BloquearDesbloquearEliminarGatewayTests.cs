using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Seguridad;
using SGV.Tests.Integration;
using SGV.Tests.Persistencia;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Tests for the lockout-based user lifecycle (Bloquear / Desbloquear /
/// Eliminar) introduced by change <c>2026-07-15-quita-soft-delete-usuario</c>.
/// Replaces the previous Desactivar/Reactivar tests with assertions
/// aligned to Identity's <see cref="UserManager{TSelf}.SetLockoutEndDateAsync"/>
/// and <see cref="UserManager{TSelf}.DeleteAsync"/> APIs.
/// </summary>
[Collection(MySqlIntegrationCollection.Name)]
public sealed class BloquearDesbloquearEliminarGatewayTests
{
    [MySqlFact]
    public async Task BloquearAsync_ActiveUser_SetsLockoutEndIntoFuture()
    {
        await using var fixture = await BloquearGatewayFixture.CreateAsync();
        var user = await fixture.AddActiveUserAsync(
            $"{fixture.Marker}-bloq",
            [RolesSgv.Consultor]);

        var result = await fixture.Gateway.BloquearAsync(user.Id);

        Assert.True(result.IsSuccess);
        var tracked = await fixture.Context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.True(tracked.LockoutEnd.HasValue);
        Assert.True(tracked.LockoutEnd.Value > DateTimeOffset.UtcNow);
    }

    [MySqlFact]
    public async Task BloquearAsync_AlreadyBlockedUser_IsIdempotent()
    {
        // Triangulate: a second Bloquear on the same user does not throw
        // and preserves LockoutEnd future. Idempotencia de auditoría es
        // responsabilidad del command service (Phase 2); aquí sólo
        // verificamos el comportamiento del gateway.
        await using var fixture = await BloquearGatewayFixture.CreateAsync();
        var user = await fixture.AddActiveUserAsync(
            $"{fixture.Marker}-bloq2",
            [RolesSgv.Consultor]);

        var first = await fixture.Gateway.BloquearAsync(user.Id);
        var second = await fixture.Gateway.BloquearAsync(user.Id);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        var tracked = await fixture.Context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.True(tracked.LockoutEnd.HasValue);
        Assert.True(tracked.LockoutEnd.Value > DateTimeOffset.UtcNow);
    }

    [MySqlFact]
    public async Task BloquearAsync_UnknownUser_ReturnsUsuarioNoEncontrado()
    {
        await using var fixture = await BloquearGatewayFixture.CreateAsync();

        var result = await fixture.Gateway.BloquearAsync("nonexistent-id");

        Assert.False(result.IsSuccess);
        Assert.Equal("UsuarioNoEncontrado", result.Error!.Code);
    }

    [MySqlFact]
    public async Task DesbloquearAsync_LockedOutUser_ClearsLockoutEnd()
    {
        await using var fixture = await BloquearGatewayFixture.CreateAsync();
        var user = await fixture.AddActiveUserAsync(
            $"{fixture.Marker}-desbloq",
            [RolesSgv.Consultor]);
        await fixture.Gateway.BloquearAsync(user.Id);

        var result = await fixture.Gateway.DesbloquearAsync(user.Id);

        Assert.True(result.IsSuccess);
        var tracked = await fixture.Context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Null(tracked.LockoutEnd);
        Assert.True(tracked.LockoutEnabled,
            "LockoutEnabled must remain true even after unlock — Identity contract.");
    }

    [MySqlFact]
    public async Task EliminarAsync_ActiveUser_DeletesIdentityRowButPreservesPersona()
    {
        await using var fixture = await BloquearGatewayFixture.CreateAsync();
        var user = await fixture.AddActiveUserAsync(
            $"{fixture.Marker}-delete",
            [RolesSgv.Consultor]);

        var result = await fixture.Gateway.EliminarAsync(user.Id);

        Assert.True(result.IsSuccess);
        var stillThere = await fixture.Context.Users.SingleOrDefaultAsync(u => u.Id == user.Id);
        Assert.Null(stillThere);
        // Persona sobrevive porque la FK de AspNetUsers.PersonaId es RESTRICT
        // y la eliminación sólo borra AspNetUsers + Identity cascade.
        var personaStill = await fixture.Context.Personas.SingleOrDefaultAsync(
            p => p.Id == user.PersonaId);
        Assert.NotNull(personaStill);
        // REL-007 (4R review): FK CASCADE debe purgar las tablas Identity
        // vinculadas. El fixture siembra un UserRole; los otros 3 sets
        // deben permanecer vacíos.
        Assert.Empty(await fixture.Context.UserRoles.Where(ur => ur.UserId == user.Id).ToListAsync());
        Assert.Empty(await fixture.Context.UserClaims.Where(claim => claim.UserId == user.Id).ToListAsync());
        Assert.Empty(await fixture.Context.UserLogins.Where(login => login.UserId == user.Id).ToListAsync());
        Assert.Empty(await fixture.Context.UserTokens.Where(token => token.UserId == user.Id).ToListAsync());
    }

    [MySqlFact]
    public async Task ObtenerAsync_LockoutEndInFuture_ReturnsBloqueadoTrue()
    {
        // RIS-006 / REA-009 (4R review): UsuarioDto.Bloqueado refleja
        // LockoutEnd > UtcNow vía MapAsync.
        await using var fixture = await BloquearGatewayFixture.CreateAsync();
        var user = await fixture.AddActiveUserAsync(
            $"{fixture.Marker}-bloq-fut",
            [RolesSgv.Consultor]);
        await fixture.Gateway.BloquearAsync(user.Id);

        var dto = await fixture.Gateway.ObtenerAsync(user.Id);

        Assert.True(dto!.Bloqueado);
    }

    [MySqlFact]
    public async Task ObtenerAsync_LockoutEndInPast_ReturnsBloqueadoFalse()
    {
        // RIS-006 / REA-009 (4R review): Bloqueado=false cuando LockoutEnd venció.
        await using var fixture = await BloquearGatewayFixture.CreateAsync();
        var user = await fixture.AddActiveUserAsync(
            $"{fixture.Marker}-past-lock",
            [RolesSgv.Consultor]);
        var tracked = await fixture.Context.Users.SingleAsync(u => u.Id == user.Id);
        tracked.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-5);
        tracked.LockoutEnabled = true;
        await fixture.Context.SaveChangesAsync();

        var dto = await fixture.Gateway.ObtenerAsync(user.Id);

        Assert.False(dto!.Bloqueado);
    }

    [MySqlFact]
    public async Task EliminarAsync_LockedOutUser_DeletesWithoutPriorUnlock()
    {
        // Triangulate: el gateway permite borrar una cuenta bloqueada sin
        // requerir desbloqueo previo. La eliminación es física e
        // independiente del estado de lockout.
        await using var fixture = await BloquearGatewayFixture.CreateAsync();
        var user = await fixture.AddActiveUserAsync(
            $"{fixture.Marker}-bloqdelete",
            [RolesSgv.Consultor]);
        await fixture.Gateway.BloquearAsync(user.Id);

        var result = await fixture.Gateway.EliminarAsync(user.Id);

        Assert.True(result.IsSuccess);
        var stillThere = await fixture.Context.Users.SingleOrDefaultAsync(u => u.Id == user.Id);
        Assert.Null(stillThere);
    }

    [MySqlFact]
    public async Task EliminarAsync_UnknownUser_ReturnsUsuarioNoEncontrado()
    {
        await using var fixture = await BloquearGatewayFixture.CreateAsync();

        var result = await fixture.Gateway.EliminarAsync("nonexistent-id");

        Assert.False(result.IsSuccess);
        Assert.Equal("UsuarioNoEncontrado", result.Error!.Code);
    }

    [MySqlFact]
    public async Task QueryAsync_ByBloqueadas_ReturnsUsersWithFutureLockoutEnd()
    {
        // Triangulate: el segmento Bloqueadas filtra usuarios con
        // LockoutEnd > UtcNow. No hay columna IsDeleted.
        await using var fixture = await BloquearGatewayFixture.CreateAsync();
        var activeUser = await fixture.AddActiveUserAsync(
            $"{fixture.Marker}-active2",
            [RolesSgv.Consultor]);
        var blockedUser = await fixture.AddActiveUserAsync(
            $"{fixture.Marker}-blocked",
            [RolesSgv.Consultor]);
        await fixture.Gateway.BloquearAsync(blockedUser.Id);

        var bloqueadas = await fixture.Gateway.QueryAsync(new UsuarioListQuery(
            1, 20, fixture.Marker, null, UsuarioSegmentoListado.Bloqueadas));

        Assert.Equal(1, bloqueadas.TotalCount);
        Assert.Equal(blockedUser.Id, Assert.Single(bloqueadas.Items).Id);
    }

    [MySqlFact]
    public async Task QueryAsync_ByActivas_ExcludesBlockedUsers()
    {
        await using var fixture = await BloquearGatewayFixture.CreateAsync();
        var activeUser = await fixture.AddActiveUserAsync(
            $"{fixture.Marker}-active3",
            [RolesSgv.Consultor]);
        var blockedUser = await fixture.AddActiveUserAsync(
            $"{fixture.Marker}-blocked3",
            [RolesSgv.Consultor]);
        await fixture.Gateway.BloquearAsync(blockedUser.Id);

        var activas = await fixture.Gateway.QueryAsync(new UsuarioListQuery(
            1, 20, fixture.Marker, null, UsuarioSegmentoListado.Activas));

        Assert.Single(activas.Items);
        Assert.Equal(activeUser.Id, activas.Items[0].Id);
    }

    /// <summary>
    /// Fixture específico para tests del flujo Bloquear/Desbloquear/Eliminar.
    /// Reemplaza el GatewayFixture previo porque ya no existe la columna
    /// IsDeleted en <see cref="SgvIdentityUser"/>: todos los usuarios
    /// que crea son ACTIVOS (LockoutEnd null).
    /// </summary>
    private sealed class BloquearGatewayFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly List<string> _userIds = [];
        private readonly List<Guid> _personaIds = [];

        private BloquearGatewayFixture(
            ServiceProvider provider,
            SgvDbContext context,
            UsuarioIdentityGateway gateway)
        {
            _provider = provider;
            Context = context;
            Gateway = gateway;
            Marker = $"bde{Guid.NewGuid():N}"[..14];
        }

        public string Marker { get; }
        public SgvDbContext Context { get; }
        public UsuarioIdentityGateway Gateway { get; }

        public static Task<BloquearGatewayFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<SgvDbContext>()
                .UseMySql(
                    TestSgvDbContextFactory.ResolveConnectionString(),
                    new MySqlServerVersion(new Version(8, 0, 36)),
                    mysql => mysql.SchemaBehavior(Pomelo.EntityFrameworkCore.MySql.Infrastructure.MySqlSchemaBehavior.Ignore))
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
            return Task.FromResult(new BloquearGatewayFixture(provider, context, gateway));
        }

        public async Task<SgvIdentityUser> AddActiveUserAsync(
            string userName,
            IReadOnlyCollection<string> roles)
        {
            var persona = new PersonaEntity
            {
                Id = Guid.NewGuid(),
                Legajo = $"LEG-{Guid.NewGuid():N}"[..18],
                Nombres = "Active",
                Apellidos = Marker,
                Email = $"{Guid.NewGuid():N}@persona.test",
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
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
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
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
            Context.ChangeTracker.Clear();
            await _provider.DisposeAsync();
        }
    }
}