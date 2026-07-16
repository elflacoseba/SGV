using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using SGV.Aplicacion.Seguridad;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Seguridad;
using Xunit;

namespace SGV.Tests.Persistencia;

public sealed class UsuarioIdentityGatewayTests
{
    [MySqlFact]
    public async Task QueryAsync_ReturnsRequestedSegmentWithPersonaNamesAndRoles()
    {
        await using var fixture = await GatewayFixture.CreateAsync();
        var marker = fixture.Marker;
        await fixture.AddUserAsync($"{marker}-active", "Ana", marker, isDeleted: false, [RolesSgv.Administrador]);
        await fixture.AddUserAsync($"{marker}-deleted", "Beto", marker, isDeleted: true, [RolesSgv.Consultor]);

        var active = await fixture.Gateway.QueryAsync(
            new UsuarioListQuery(1, 20, marker, "username_asc", UsuarioSegmentoListado.Activas));
        var deleted = await fixture.Gateway.QueryAsync(
            new UsuarioListQuery(1, 20, marker, "username_asc", UsuarioSegmentoListado.Eliminadas));

        var activeUser = Assert.Single(active.Items);
        Assert.Equal("Ana", activeUser.Nombres);
        Assert.Equal(marker, activeUser.Apellidos);
        Assert.Equal([RolesSgv.Administrador], activeUser.Roles);
        var deletedUser = Assert.Single(deleted.Items);
        Assert.Equal("Beto", deletedUser.Nombres);
        Assert.Equal([RolesSgv.Consultor], deletedUser.Roles);
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
            isDeleted: false,
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
    public async Task DesactivarAndReactivarAsync_MovesUserBetweenSegments()
    {
        await using var fixture = await GatewayFixture.CreateAsync();
        var user = await fixture.AddUserAsync(
            $"{fixture.Marker}-toggle",
            "Toggle",
            fixture.Marker,
            false,
            [RolesSgv.Consultor]);

        var deactivate = await fixture.Gateway.DesactivarAsync(user.Id);
        var deleted = await fixture.Gateway.QueryAsync(new UsuarioListQuery(
            1,
            20,
            fixture.Marker,
            null,
            UsuarioSegmentoListado.Eliminadas));
        var reactivate = await fixture.Gateway.ReactivarAsync(user.Id);
        var active = await fixture.Gateway.QueryAsync(new UsuarioListQuery(
            1,
            20,
            fixture.Marker,
            null,
            UsuarioSegmentoListado.Activas));

        Assert.True(deactivate.IsSuccess);
        Assert.Contains(deleted.Items, item => item.Id == user.Id);
        Assert.True(reactivate.IsSuccess);
        Assert.Contains(active.Items, item => item.Id == user.Id);
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

    [MySqlFact]
    public async Task Migration_AppliesSuccessfullyToCleanDatabase()
    {
        var databaseName = $"sgv_users_{Guid.NewGuid():N}"[..24];
        var databaseConnection = new MySqlConnectionStringBuilder(
            TestSgvDbContextFactory.ResolveConnectionString())
        {
            Database = databaseName
        };
        var serverConnection = new MySqlConnectionStringBuilder(databaseConnection.ConnectionString)
        {
            Database = string.Empty
        };

        await using var adminConnection = new MySqlConnection(serverConnection.ConnectionString);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = $"CREATE DATABASE `{databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<SgvDbContext>()
                .UseMySql(
                    databaseConnection.ConnectionString,
                    new MySqlServerVersion(new Version(8, 0, 36)))
                .Options;
            await using var context = new SgvDbContext(options);

            await context.Database.MigrateAsync();

            var columns = await context.Database
                .SqlQueryRaw<string>(
                    "SELECT COLUMN_NAME AS Value FROM INFORMATION_SCHEMA.COLUMNS " +
                    "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'AspNetUsers' " +
                    "AND COLUMN_NAME IN ('IsDeleted', 'ActiveUserNameUnique')")
                .ToListAsync();
            Assert.Equal(["ActiveUserNameUnique", "IsDeleted"], columns.Order().ToArray());
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS `{databaseName}`;";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [MySqlFact]
    public async Task Migration_CreatesGeneratedActiveUserNameColumnAndUniqueIndex()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXTRA, GENERATION_EXPRESSION
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'AspNetUsers'
              AND COLUMN_NAME = 'ActiveUserNameUnique';
            """;
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Contains("STORED GENERATED", reader.GetString(0), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lower", reader.GetString(1), StringComparison.OrdinalIgnoreCase);
        await reader.CloseAsync();

        command.CommandText = """
            SELECT NON_UNIQUE
            FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'AspNetUsers'
              AND INDEX_NAME = 'IX_AspNetUsers_ActiveUserNameUnique';
            """;
        var nonUnique = Convert.ToInt32(await command.ExecuteScalarAsync());
        Assert.Equal(0, nonUnique);
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

        public string Marker { get; }
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
            bool isDeleted,
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
                CreatedAt = DateTime.UtcNow
            };
            var user = new SgvIdentityUser
            {
                Id = Guid.NewGuid().ToString("N"),
                PersonaId = persona.Id,
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                Email = $"{Guid.NewGuid():N}@user.test",
                NormalizedEmail = $"{Guid.NewGuid():N}@USER.TEST",
                IsDeleted = isDeleted,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };

            Context.Personas.Add(persona);
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

        public async ValueTask DisposeAsync()
        {
            Context.UserRoles.RemoveRange(Context.UserRoles.Where(role => _userIds.Contains(role.UserId)));
            Context.Users.RemoveRange(Context.Users.Where(user => _userIds.Contains(user.Id)));
            Context.Personas.RemoveRange(Context.Personas.Where(persona => _personaIds.Contains(persona.Id)));
            await Context.SaveChangesAsync();
            await _provider.DisposeAsync();
        }
    }
}
