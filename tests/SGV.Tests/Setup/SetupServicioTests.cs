using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Auditoria;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Aplicacion.Setup;
using SGV.Contracts.Comun;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Contracts.Setup;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Seguridad;
using SGV.Tests.Persistencia;
using SGV.Tests.Seguridad;
using Xunit;

namespace SGV.Tests.Setup;

/// <summary>
/// Unit + integration tests for <see cref="ISetupServicio"/> (issue #195).
///
/// Strategy:
/// - DB-empty happy path, DB-with-users guard, audit invocation, and
///   validation failure run against real MySQL via <see cref="JwtRealWebApplicationFactory"/>
///   (decorated as <see cref="MySqlFactAttribute"/>) and skip cleanly
///   when MySQL is unreachable.
/// - The PasswordTooShort and DuplicateUserName mappings are hard to
///   reproduce end-to-end (DuplicateUserName requires a race condition;
///   PasswordTooShort is normally caught by our <c>SetupRequestValidator</c>).
///   These run as <see cref="FactAttribute"/> with a fake
///   <see cref="IUsuarioIdentityGateway"/> injected via DI override.
/// - All Setup tests share a single xUnit collection to serialize them
///   against the shared <c>sgv_test</c> database.
/// </summary>
[Collection("SetupServicio")]
public sealed class SetupServicioTests
{
    private const string SigningKey = "E2E-API-TEST-MIN-32-BYTES-REQUIRED!!!";

    // ---- [Fact] tests — no DB required -----------------------------------

    [Fact]
    public async Task CrearAdminAsync_ValidacionFalla_DevuelveDatosInvalidosConFieldErrors()
    {
        var factory = await CreateFactoryAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var setupServicio = scope.ServiceProvider.GetRequiredService<ISetupServicio>();

        var request = NewValidRequest(nombres: string.Empty); // Nombres requerido => falla validación

        var result = await setupServicio.CrearAdminAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(SetupErrorCode.DatosInvalidos, result.Error!.Code);
        Assert.Equal(ErrorCategoria.Validation, result.Error.Categoria);
        Assert.NotNull(result.FieldErrors);
        Assert.NotEmpty(result.FieldErrors!);
        Assert.True(result.FieldErrors!.ContainsKey("nombres"),
            $"Se esperaba 'nombres' en fieldErrors; claves: {string.Join(',', result.FieldErrors.Keys)}");
    }

    [Fact]
    public async Task CrearAdminAsync_PasswordCorta_DevuelvePasswordDebil()
    {
        // Inyectamos un FakeIdentityGateway que simula PasswordTooShort.
        await using var factory = await CreateFactoryWithFakeGatewayAsync(
            UsuarioCommandResult.Failure(new UsuarioError(
                UsuarioErrorType.Validation,
                "PasswordTooShort",
                "La contraseña debe tener al menos 6 caracteres.",
                Categoria: ErrorCategoria.Validation)));
        await VaciarAspNetUsersAsync(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var setupServicio = scope.ServiceProvider.GetRequiredService<ISetupServicio>();

        var request = NewValidRequest();

        var result = await setupServicio.CrearAdminAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(SetupErrorCode.PasswordDebil, result.Error!.Code);
        Assert.Equal(ErrorCategoria.Validation, result.Error.Categoria);
        Assert.Equal(400, result.Error.StatusCode);
    }

    [Fact]
    public async Task CrearAdminAsync_UserNameDuplicado_DevuelveUserNameDuplicado()
    {
        await using var factory = await CreateFactoryWithFakeGatewayAsync(
            UsuarioCommandResult.Failure(new UsuarioError(
                UsuarioErrorType.Conflict,
                "UserNameDuplicado",
                "El nombre de usuario ya está en uso.",
                Categoria: ErrorCategoria.Conflict)));
        await VaciarAspNetUsersAsync(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var setupServicio = scope.ServiceProvider.GetRequiredService<ISetupServicio>();

        var request = NewValidRequest();

        var result = await setupServicio.CrearAdminAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(SetupErrorCode.UserNameDuplicado, result.Error!.Code);
        Assert.Equal(ErrorCategoria.Conflict, result.Error.Categoria);
        Assert.Equal(409, result.Error.StatusCode);
    }

    // ---- [MySqlFact] tests — DB required ---------------------------------

    [MySqlFact]
    public async Task CrearAdminAsync_DBVacia_DatosValidos_DevuelveSuccess()
    {
        await using var factory = await CreateFactoryAsync();
        await VaciarTablasAsync(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var setupServicio = scope.ServiceProvider.GetRequiredService<ISetupServicio>();

        var request = NewValidRequest();

        var result = await setupServicio.CrearAdminAsync(request);

        Assert.True(result.IsSuccess,
            $"Esperaba éxito. Error={result.Error?.Code} Msg={result.Error?.Message}");
        Assert.NotNull(result.Value);
        Assert.Equal(request.UserName, result.Value!.UserName);
        Assert.NotEqual(Guid.Empty, result.Value.PersonaId);
        Assert.False(string.IsNullOrEmpty(result.Value.UserId));

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<SgvDbContext>();
        var userManager = verifyScope.ServiceProvider
            .GetRequiredService<UserManager<SgvIdentityUser>>();

        var user = await userManager.FindByIdAsync(result.Value.UserId);
        Assert.NotNull(user);
        var roles = await userManager.GetRolesAsync(user!);
        Assert.Contains(RolesSgv.Administrador, roles);

        var persona = await db.Personas
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == result.Value.PersonaId);
        Assert.NotNull(persona);
        Assert.Equal(request.Nombres, persona!.Nombres);
        Assert.Equal(request.Apellidos, persona.Apellidos);
    }

    [MySqlFact]
    public async Task CrearAdminAsync_DBTieneUsuarios_DevuelveSetupYaCompletado()
    {
        await using var factory = await CreateFactoryAsync();
        await VaciarTablasAsync(factory);
        await SeedAdminAsync(factory, $"admin-existente-{Guid.NewGuid():N}"[..24]);

        await using var scope = factory.Services.CreateAsyncScope();
        var setupServicio = scope.ServiceProvider.GetRequiredService<ISetupServicio>();

        var request = NewValidRequest();

        var result = await setupServicio.CrearAdminAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(SetupErrorCode.SetupYaCompletado, result.Error!.Code);
        Assert.Equal(ErrorCategoria.Conflict, result.Error.Categoria);
        Assert.Equal(409, result.Error.StatusCode);
    }

    [MySqlFact]
    public async Task CrearAdminAsync_DBVacia_RegistraAuditoriaConUsuarioOperadorSystem()
    {
        await using var factory = await CreateFactoryAsyncWithRecordingAudit();
        await VaciarTablasAsync(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var setupServicio = scope.ServiceProvider.GetRequiredService<ISetupServicio>();
        var auditSpy = scope.ServiceProvider.GetRequiredService<RecordingAuditoriaServicio>();

        var request = NewValidRequest();

        var result = await setupServicio.CrearAdminAsync(request);

        Assert.True(result.IsSuccess,
            $"Esperaba éxito. Error={result.Error?.Code} Msg={result.Error?.Message}");

        var call = auditSpy.Calls.SingleOrDefault(c =>
            string.Equals(c.entidad, "SetupInicial", StringComparison.Ordinal) &&
            string.Equals(c.accion, "AltaPrimerAdministrador", StringComparison.Ordinal));
        Assert.NotNull(call);
        Assert.Equal("system", call!.usuarioOperadorId);
    }

    // ---- Helpers ---------------------------------------------------------

    private static SetupRequest NewValidRequest(
        string? nombres = null,
        string? apellidos = null,
        string? userName = null,
        string? email = null,
        string? password = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        // Importante: NO usar Nombres="Admin" / Apellidos="Seed" porque
        // colisionan con la persona "Admin Seed" que JwtRealWebApplicationFactory
        // siembra en InitializeAsync. Si coinciden, InitializeAsync reusa esa
        // persona y al intentar crear admin con su mismo PersonaId falla con
        // duplicate FK.
        return new SetupRequest(
            Nombres: nombres ?? "Operador",
            Apellidos: apellidos ?? "Inicial",
            Legajo: $"LEG-{suffix}",
            Email: email ?? $"operador-{suffix}@setup.test",
            UserName: userName ?? $"operador-{suffix}",
            Password: password ?? "Setup#12345",
            TipoDocumentoId: null,
            NumeroDocumento: null,
            Telefono: "+5491100000000");
    }

    private static async Task<JwtRealWebApplicationFactory> CreateFactoryAsync()
    {
        var factory = new JwtRealWebApplicationFactory(signingKey: SigningKey);
        await factory.InitializeAsync();
        return factory;
    }

    private static async Task<JwtRealWebApplicationFactory> CreateFactoryAsyncWithRecordingAudit()
    {
        var factory = new DelegatingJwtRealWebApplicationFactory(
            SigningKey,
            services =>
            {
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditoriaServicio));
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton<RecordingAuditoriaServicio>();
                services.AddSingleton<IAuditoriaServicio>(sp => sp.GetRequiredService<RecordingAuditoriaServicio>());
            });
        await factory.InitializeAsync();
        return factory;
    }

    private static async Task<JwtRealWebApplicationFactory> CreateFactoryWithFakeGatewayAsync(
        UsuarioCommandResult gatewayResponse)
    {
        var factory = new DelegatingJwtRealWebApplicationFactory(
            SigningKey,
            services =>
            {
                // Reemplaza el gateway real por un fake que devuelve la respuesta provista.
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IUsuarioIdentityGateway));
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton(new FakeUsuarioIdentityGateway(gatewayResponse));
                services.AddSingleton<IUsuarioIdentityGateway>(sp => sp.GetRequiredService<FakeUsuarioIdentityGateway>());
            });
        await factory.InitializeAsync();
        return factory;
    }

    private static async Task VaciarTablasAsync(JwtRealWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgvDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM `Auditorias`");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM `AspNetUserRoles`");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM `AspNetUsers`");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM `Personas`");
        await db.SaveChangesAsync();
    }

    private static async Task VaciarAspNetUsersAsync(JwtRealWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgvDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM `Auditorias`");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM `AspNetUserRoles`");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM `AspNetUsers`");
        await db.SaveChangesAsync();
    }

    private static async Task SeedAdminAsync(JwtRealWebApplicationFactory factory, string userName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<SgvIdentityUser>>();

        var persona = new SGV.Infraestructura.Persistencia.Entidades.PersonaEntity
        {
            Id = Guid.NewGuid(),
            Nombres = "Seed",
            Apellidos = "Admin",
            IsActive = true,
        };
        var db = scope.ServiceProvider.GetRequiredService<SgvDbContext>();
        db.Personas.Add(persona);
        await db.SaveChangesAsync();

        var user = new SgvIdentityUser
        {
            UserName = userName,
            Email = $"{userName}@seed.test",
            EmailConfirmed = true,
            PersonaId = persona.Id,
        };
        var create = await userManager.CreateAsync(user, "Seed#12345");
        Assert.True(create.Succeeded,
            $"Seed admin falló: {string.Join(',', create.Errors.Select(e => e.Description))}");
        await userManager.AddToRoleAsync(user, RolesSgv.Administrador);
    }
}

/// <summary>
/// Recording spy for <see cref="IAuditoriaServicio"/>. Replaces the real
/// auditor inside the test host so tests can assert the entity /
/// operation / usuarioOperadorId actually invoked.
/// </summary>
internal sealed class RecordingAuditoriaServicio : IAuditoriaServicio
{
    public List<(string entidad, string entityId, string accion, string? usuarioOperadorId)> Calls { get; } = new();

    public Task RegistrarAsync(
        string entidad,
        string entityId,
        string accion,
        string? usuarioOperadorId,
        IReadOnlyDictionary<string, object?> valoresAnteriores,
        IReadOnlyDictionary<string, object?> valoresNuevos,
        CancellationToken cancellationToken = default)
    {
        Calls.Add((entidad, entityId, accion, usuarioOperadorId));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Fake <see cref="IUsuarioIdentityGateway"/> que devuelve una
/// respuesta pre-configurada. Útil para tests unitarios del mapping
/// de errores en <see cref="SGV.Infraestructura.Setup.SetupServicio"/>
/// sin necesidad de levantar el pipeline de Identity.
/// </summary>
internal sealed class FakeUsuarioIdentityGateway : IUsuarioIdentityGateway
{
    private readonly UsuarioCommandResult _crearResponse;

    public FakeUsuarioIdentityGateway(UsuarioCommandResult crearResponse)
    {
        _crearResponse = crearResponse;
    }

    public Task<UsuarioCommandResult> CrearAsync(CrearUsuarioRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(_crearResponse);

    public Task<UsuarioCommandResult> AsignarRolesAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("No usado en tests de Setup.");

    public Task<UsuarioDto?> ObtenerAsync(string userId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("No usado en tests de Setup.");

    public Task<UsuarioCommandResult> ActualizarAsync(string userId, ActualizarUsuarioRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("No usado en tests de Setup.");

    public Task<UsuarioCommandResult> BloquearAsync(string userId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("No usado en tests de Setup.");

    public Task<UsuarioCommandResult> DesbloquearAsync(string userId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("No usado en tests de Setup.");

    public Task<UsuarioCommandResult> EliminarAsync(string userId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("No usado en tests de Setup.");
}

/// <summary>
/// Variante de <see cref="JwtRealWebApplicationFactory"/> que añade un
/// callback extra de <c>ConfigureServices</c>. Sirve para que los tests
/// sobrescriban dependencias internas (e.g.
/// <see cref="IAuditoriaServicio"/>,
/// <see cref="IUsuarioIdentityGateway"/>) sin reescribir el host wiring.
/// </summary>
internal sealed class DelegatingJwtRealWebApplicationFactory(
    string signingKey,
    Action<IServiceCollection> extraConfigure)
    : JwtRealWebApplicationFactory(signingKey)
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(extraConfigure);
    }
}
