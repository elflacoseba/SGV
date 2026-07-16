using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using SGV.Aplicacion.Seguridad;
using SGV.Contracts.Comun;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Seguridad;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Tests del gateway de usuarios (queries, actualización y unicidad de
/// PersonaId) adaptados al modelo post-soft-delete. Los tests de
/// Bloquear/Desbloquear/Eliminar viven en
/// <see cref="BloquearDesbloquearEliminarGatewayTests"/>.
/// </summary>
/// <remarks>
/// Se removieron los tests que dependían de <c>IsDeleted</c> y de
/// <c>Desactivar/Reactivar</c>: ambos ya no existen en el modelo de
/// la entidad. La separación activa/bloqueada se filtra por
/// <c>LockoutEnd &gt; UtcNow</c>.
/// </remarks>
public sealed class UsuarioIdentityGatewayTests
{
    [MySqlFact]
    public async Task QueryAsync_ReturnsRequestedSegmentWithPersonaNamesAndRoles()
    {
        await using var fixture = await GatewayFixture.CreateAsync();
        var marker = fixture.Marker;
        var activeUser = await fixture.AddUserAsync($"{marker}-active", "Ana", marker, blocked: false, [RolesSgv.Administrador]);
        var blockedUser = await fixture.AddUserAsync($"{marker}-blocked", "Beto", marker, blocked: true, [RolesSgv.Consultor]);

        var active = await fixture.Gateway.QueryAsync(
            new UsuarioListQuery(1, 20, marker, "username_asc", UsuarioSegmentoListado.Activas));
        var bloqueadas = await fixture.Gateway.QueryAsync(
            new UsuarioListQuery(1, 20, marker, "username_asc", UsuarioSegmentoListado.Bloqueadas));

        var activeUserResult = Assert.Single(active.Items);
        Assert.Equal("Ana", activeUserResult.Nombres);
        Assert.Equal(marker, activeUserResult.Apellidos);
        Assert.Equal([RolesSgv.Administrador], activeUserResult.Roles);
        var blockedUserResult = Assert.Single(bloqueadas.Items);
        Assert.Equal("Beto", blockedUserResult.Nombres);
        Assert.Equal([RolesSgv.Consultor], blockedUserResult.Roles);
    }

    [MySqlFact]
    public async Task QueryAsync_SearchesPersonaNamesAndSurnames()
    {
        await using var fixture = await GatewayFixture.CreateAsync();
        var uniqueName = $"Nombre{fixture.Marker}";
        var uniqueSurname = $"Apellido{fixture.Marker}";
        await fixture.AddUserAsync(
            $"{fixture.Marker}-search",
            uniqueName,
            uniqueSurname,
            blocked: false,
            [RolesSgv.Consultor]);

        var byName = await fixture.Gateway.QueryAsync(new UsuarioListQuery(
            1,
            20,
            uniqueName,
            null));
        var bySurname = await fixture.Gateway.QueryAsync(new UsuarioListQuery(
            1,
            20,
            uniqueSurname,
            null));

        Assert.Equal(uniqueName, Assert.Single(byName.Items).Nombres);
        Assert.Equal(uniqueSurname, Assert.Single(bySurname.Items).Apellidos);
    }

    [MySqlFact]
    public async Task QueryAsync_SortsBeforePagination()
    {
        await using var fixture = await GatewayFixture.CreateAsync();
        await fixture.AddUserAsync($"{fixture.Marker}-z", "Zeta", fixture.Marker, false, [RolesSgv.Consultor]);
        await fixture.AddUserAsync($"{fixture.Marker}-a", "Alpha", fixture.Marker, false, [RolesSgv.Consultor]);

        var ascending = await fixture.Gateway.QueryAsync(
            new UsuarioListQuery(1, 20, fixture.Marker, "username_asc"));
        var descending = await fixture.Gateway.QueryAsync(
            new UsuarioListQuery(1, 20, fixture.Marker, "username_desc"));

        Assert.Equal(2, ascending.TotalCount);
        Assert.Equal($"{fixture.Marker}-a", ascending.Items[0].UserName);
        Assert.Equal($"{fixture.Marker}-z", ascending.Items[1].UserName);
        Assert.Equal($"{fixture.Marker}-z", descending.Items[0].UserName);
        Assert.Equal($"{fixture.Marker}-a", descending.Items[1].UserName);
    }

    [MySqlFact]
    public async Task QueryAsync_WithMultipleUsersAndRoles_UsesConstantQueryCount()
    {
        var interceptor = new CommandCounterInterceptor();
        await using var fixture = await GatewayFixture.CreateAsync(interceptor);
        await fixture.AddUserAsync($"{fixture.Marker}-one", "One", fixture.Marker, false, [RolesSgv.Administrador]);
        await fixture.AddUserAsync($"{fixture.Marker}-two", "Two", fixture.Marker, false, [RolesSgv.Consultor]);
        await fixture.AddUserAsync(
            $"{fixture.Marker}-three",
            "Three",
            fixture.Marker,
            false,
            [RolesSgv.Consultor, RolesSgv.GestorVacantes]);
        interceptor.Reset();

        var result = await fixture.Gateway.QueryAsync(
            new UsuarioListQuery(1, 20, fixture.Marker, "username_asc"));

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(3, result.TotalCount);
        Assert.All(result.Items, item => Assert.NotEmpty(item.Roles));
        Assert.Equal(2, interceptor.ReaderCommandCount);
    }

    [MySqlFact]
    public async Task ListAsync_CapsPageSizeToReasonableMaximum()
    {
        await using var fixture = await GatewayFixture.CreateAsync();
        var interceptor = new ListPageSizeInterceptor();
        var resultWithInterceptor = await ListAsyncWithInterceptorAsync(fixture, interceptor);

        Assert.NotNull(resultWithInterceptor);
        Assert.True(interceptor.ObservedPageSize <= 500,
            $"PageSize debe estar limitado a 500; se observó {interceptor.ObservedPageSize}.");
    }

    [MySqlFact]
    public async Task ListAsync_ReturnsActiveUsersUsingEligibleSegment()
    {
        // Triangulación: el atajo debe seguir devolviendo únicamente
        // usuarios activos (mismo segmento que las páginas
        // administrativas consumen vía QueryAsync).
        await using var fixture = await GatewayFixture.CreateAsync();
        var marker = $"listasync-{Guid.NewGuid():N}"[..14];
        await fixture.AddUserAsync($"{marker}-active", "Ana", marker, blocked: false, [RolesSgv.Consultor]);
        await fixture.AddUserAsync($"{marker}-blocked", "Beto", marker, blocked: true, [RolesSgv.Consultor]);

        var result = await fixture.Gateway.ListAsync();

        Assert.Contains(result, user => user.UserName == $"{marker}-active");
        Assert.DoesNotContain(result, user => user.UserName == $"{marker}-blocked");
    }

    private static async Task<IReadOnlyList<UsuarioDto>> ListAsyncWithInterceptorAsync(
        GatewayFixture fixture,
        ListPageSizeInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<SgvDbContext>()
            .UseMySql(
                TestSgvDbContextFactory.ResolveConnectionString(),
                new MySqlServerVersion(new Version(8, 0, 36)))
            .AddInterceptors(interceptor)
            .Options;
        await using var context = new SgvDbContext(options);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(context);
        services.AddIdentityCore<SgvIdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<SgvDbContext>();
        await using var provider = services.BuildServiceProvider();
        var userManager = provider.GetRequiredService<UserManager<SgvIdentityUser>>();
        var gateway = new UsuarioIdentityGateway(userManager, context);
        return await gateway.ListAsync();
    }

    private sealed class ListPageSizeInterceptor : DbCommandInterceptor
    {
        public int? ObservedPageSize { get; private set; }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            CapturePageSize(command);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            CapturePageSize(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void CapturePageSize(DbCommand command)
        {
            if (ObservedPageSize.HasValue)
            {
                return;
            }

            foreach (DbParameter parameter in command.Parameters)
            {
                if (parameter.Value is int intValue
                    && intValue > 1
                    && intValue <= 5000)
                {
                    ObservedPageSize = intValue;
                    return;
                }
            }
        }
    }

    [MySqlFact]
    public async Task ActualizarAsync_ValidRequest_PersistsCredentialsAndRolesAtomically()
    {
        await using var fixture = await GatewayFixture.CreateAsync();
        var user = await fixture.AddUserAsync(
            $"{fixture.Marker}-before",
            "Before",
            fixture.Marker,
            false,
            [RolesSgv.Consultor]);
        var request = new ActualizarUsuarioRequest(
            $"{fixture.Marker}-after",
            $"{fixture.Marker}@after.test",
            [RolesSgv.Administrador, RolesSgv.GestorVacantes]);

        var result = await fixture.Gateway.ActualizarAsync(user.Id, request);

        Assert.True(result.IsSuccess);
        Assert.Equal(request.UserName, result.Value!.UserName);
        Assert.Equal(request.Email, result.Value.Email);
        Assert.Equal(request.Roles.Order(), result.Value.Roles.Order());
        var persisted = await fixture.Context.Users.SingleAsync(item => item.Id == user.Id);
        Assert.Equal(request.UserName, persisted.UserName);
        Assert.Equal(request.Email, persisted.Email);
    }

    [MySqlFact]
    public async Task ActualizarAsync_DuplicateUserName_ReturnsConflictWithoutChangingRoles()
    {
        await using var fixture = await GatewayFixture.CreateAsync();
        var existing = await fixture.AddUserAsync(
            $"{fixture.Marker}-existing",
            "Existing",
            fixture.Marker,
            false,
            [RolesSgv.Administrador]);
        var target = await fixture.AddUserAsync(
            $"{fixture.Marker}-target",
            "Target",
            fixture.Marker,
            false,
            [RolesSgv.Consultor]);

        var result = await fixture.Gateway.ActualizarAsync(
            target.Id,
            new ActualizarUsuarioRequest(
                existing.UserName!,
                $"{fixture.Marker}@target.test",
                [RolesSgv.GestorVacantes]));

        Assert.False(result.IsSuccess);
        Assert.Equal("UserNameDuplicado", result.Error!.Code);
        var roles = await fixture.UserManager.GetRolesAsync(target);
        Assert.Equal([RolesSgv.Consultor], roles);
    }

    [MySqlFact]
    public async Task CrearAsync_WithActiveUserForSamePersona_ReturnsConflictPersonaYaTieneUsuario()
    {
        // La unicidad en PersonaId se mantiene plana (UNIQUE en
        // IX_AspNetUsers_PersonaId) sin la columna generada soft-delete
        // -aware. Cualquier intento de crear un segundo usuario activo
        // para la misma Persona debe disparar el Conflict
        // "PersonaYaTieneUsuario".
        await using var fixture = await GatewayFixture.CreateAsync();
        var persona = await fixture.AddPersonaAsync($"{fixture.Marker}", $"{fixture.Marker}");

        await fixture.AddUserForPersonaAsync(
            persona,
            $"{fixture.Marker}-first",
            blocked: false,
            [RolesSgv.Consultor]);

        fixture.Context.ChangeTracker.Clear();

        var request = new CrearUsuarioRequest(
            persona.Id,
            $"{fixture.Marker}-second",
            $"{fixture.Marker}-second@test.com",
            "Password1!",
            [RolesSgv.Consultor]);
        var result = await fixture.Gateway.CrearAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("PersonaYaTieneUsuario", result.Error!.Code);
        Assert.Equal(ErrorCategoria.Conflict, result.Error.Categoria);
    }

    [MySqlFact]
    public async Task AuditoriaServicio_RegistrarAsync_PersistsCriticalDiffForIdentityMutation()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var currentUser = new FakeUsuarioActual("admin-auditor");
        var service = new AuditoriaServicio(context, currentUser);
        var entityId = Guid.NewGuid().ToString("N");

        await service.RegistrarAsync(
            "Usuario",
            entityId,
            "Modificacion",
            currentUser.UserId,
            new Dictionary<string, object?>
            {
                ["UserName"] = "before",
                ["Email"] = "before@test.com",
                ["Roles"] = "Consultor"
            },
            new Dictionary<string, object?>
            {
                ["UserName"] = "after",
                ["Email"] = "after@test.com",
                ["Roles"] = "Administrador"
            });

        var audit = await context.Auditorias.SingleAsync(item => item.EntityId == entityId);
        Assert.Equal("admin-auditor", audit.UserId);
        Assert.Equal("Usuario", audit.EntityName);
        Assert.Equal("Modificacion", audit.Operation);
        Assert.Contains("before@test.com", audit.OldValuesJson, StringComparison.Ordinal);
        Assert.Contains("after@test.com", audit.NewValuesJson, StringComparison.Ordinal);
        Assert.Contains("UserName", audit.ChangedPropertiesJson, StringComparison.Ordinal);

        context.Auditorias.Remove(audit);
        await context.SaveChangesAsync();
    }

    private sealed class FakeUsuarioActual(string userId) : IUsuarioActual
    {
        public string? UserId => userId;
        public Guid? PersonaId => null;
        public IReadOnlyCollection<string> Roles => [RolesSgv.Administrador];
        public Guid? CorrelationId => Guid.Parse("a2000000-0000-0000-0000-000000000001");
    }

    private sealed class CommandCounterInterceptor : DbCommandInterceptor
    {
        private int _readerCommandCount;
        public int ReaderCommandCount => Volatile.Read(ref _readerCommandCount);
        public void Reset() => Volatile.Write(ref _readerCommandCount, 0);

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref _readerCommandCount);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _readerCommandCount);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class GatewayFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly List<string> _userIds = [];
        private readonly List<Guid> _personaIds = [];

        private GatewayFixture(
            ServiceProvider provider,
            SgvDbContext context,
            UserManager<SgvIdentityUser> userManager,
            UsuarioIdentityGateway gateway)
        {
            _provider = provider;
            Context = context;
            UserManager = userManager;
            Gateway = gateway;
            Marker = $"usr{Guid.NewGuid():N}"[..14];
        }

        public string Marker { get; private set; }
        public SgvDbContext Context { get; }
        public UserManager<SgvIdentityUser> UserManager { get; }
        public UsuarioIdentityGateway Gateway { get; }

        public static Task<GatewayFixture> CreateAsync(DbCommandInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<SgvDbContext>()
                .UseMySql(
                    TestSgvDbContextFactory.ResolveConnectionString(),
                    new MySqlServerVersion(new Version(8, 0, 36)),
                    mysql => mysql.SchemaBehavior(MySqlSchemaBehavior.Ignore));
            if (interceptor is not null)
            {
                options.AddInterceptors(interceptor);
            }

            var context = new SgvDbContext(options.Options);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(context);
            services.AddIdentityCore<SgvIdentityUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<SgvDbContext>();
            var provider = services.BuildServiceProvider();
            var userManager = provider.GetRequiredService<UserManager<SgvIdentityUser>>();
            var gateway = new UsuarioIdentityGateway(userManager, context);
            return Task.FromResult(new GatewayFixture(provider, context, userManager, gateway));
        }

        public async Task<SgvIdentityUser> AddUserAsync(
            string userName,
            string nombres,
            string apellidos,
            bool blocked,
            IReadOnlyCollection<string> roles)
        {
            var persona = await AddPersonaAsync(nombres, apellidos);
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
                    ? BloquearFechaFuturo()
                    : null,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };

            Context.Users.Add(user);
            foreach (var role in roles)
            {
                Context.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = role });
            }
            await Context.SaveChangesAsync();
            _personaIds.Add(persona.Id);
            _userIds.Add(user.Id);
            return user;
        }

        public async Task<PersonaEntity> AddPersonaAsync(
            string nombres,
            string apellidos)
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
                CreatedAt = DateTime.UtcNow
            };
            Context.Personas.Add(persona);
            await Context.SaveChangesAsync();
            _personaIds.Add(persona.Id);
            return persona;
        }

        public async Task<SgvIdentityUser> AddUserForPersonaAsync(
            PersonaEntity persona,
            string userName,
            bool blocked,
            IReadOnlyCollection<string> roles)
        {
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
                    ? BloquearFechaFuturo()
                    : null,
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

        private static DateTimeOffset BloquearFechaFuturo()
            => new(9999, 12, 31, 23, 59, 59, TimeSpan.Zero);
    }
}