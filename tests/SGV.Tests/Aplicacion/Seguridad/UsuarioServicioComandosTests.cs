using SGV.Aplicacion.Auditoria;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Aplicacion.Seguridad;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Contracts.Comun;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Dominio.Personas;
using Xunit;

namespace SGV.Tests.Aplicacion.Seguridad;

public sealed class UsuarioServicioComandosTests
{
    private const string CurrentUserId = "admin-current";
    private const string TargetUserId = "user-target";
    private static readonly Guid PersonaId = Guid.Parse("e1000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task CrearAsync_WithExistingPersonaAndFixedRoles_CreatesLinkedUserAndAuditsCriticalFields()
    {
        var context = CreateContext();
        var request = new CrearUsuarioRequest(PersonaId, "admin", "admin@test.com", "Password1!", [RolesSgv.Administrador]);

        var result = await context.Service.CrearAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(PersonaId, result.Value!.PersonaId);
        Assert.Equal([RolesSgv.Administrador], result.Value.Roles);
        Assert.Equal(PersonaId, context.Gateway.CreatedRequest!.PersonaId);
        var audit = Assert.Single(context.Auditoria.Entries);
        Assert.Equal("Alta", audit.Accion);
        Assert.Equal(CurrentUserId, audit.UsuarioOperadorId);
        Assert.Equal("admin", audit.Nuevos["UserName"]);
        Assert.Equal("admin@test.com", audit.Nuevos["Email"]);
        Assert.Equal(RolesSgv.Administrador, audit.Nuevos["Roles"]);
    }

    [Fact]
    public async Task CrearAsync_WithoutExistingPersona_RejectsWithoutCreatingIdentityUserOrAudit()
    {
        var context = CreateContext(personaExists: false);
        var request = new CrearUsuarioRequest(PersonaId, "admin", "admin@test.com", "Password1!", [RolesSgv.Administrador]);

        var result = await context.Service.CrearAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(UsuarioErrorType.NotFound, result.Error!.Type);
        Assert.Equal(ErrorCategoria.NotFound, result.Error.Categoria);
        Assert.Null(context.Gateway.CreatedRequest);
        Assert.Empty(context.Auditoria.Entries);
    }

    [Theory]
    [InlineData("", "admin@test.com", "Password1!", "DatosInvalidos")]
    [InlineData("admin", "", "Password1!", "DatosInvalidos")]
    [InlineData("admin", "admin@test.com", "", "DatosInvalidos")]
    public async Task CrearAsync_WithMissingRequiredField_ReturnsValidation(
        string userName,
        string email,
        string password,
        string expectedCode)
    {
        var context = CreateContext();
        var request = new CrearUsuarioRequest(PersonaId, userName, email, password, [RolesSgv.Administrador]);

        var result = await context.Service.CrearAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.Equal(ErrorCategoria.Validation, result.Error.Categoria);
        Assert.Null(context.Gateway.CreatedRequest);
    }

    [Fact]
    public async Task CrearAsync_WithUnsupportedRole_RejectsWithoutCreatingIdentityUser()
    {
        var context = CreateContext();
        var request = new CrearUsuarioRequest(PersonaId, "admin", "admin@test.com", "Password1!", ["Lector"]);

        var result = await context.Service.CrearAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("RolNoSoportado", result.Error!.Code);
        Assert.Equal(ErrorCategoria.Validation, result.Error.Categoria);
        Assert.Null(context.Gateway.CreatedRequest);
    }

    [Fact]
    public async Task AsignarRolesAsync_WithMissingUser_RejectsWithoutRoleAssignmentOrAudit()
    {
        var context = CreateContext();
        context.Gateway.Remove(TargetUserId);

        var result = await context.Service.AsignarRolesAsync(
            TargetUserId,
            new AsignarRolesRequest([RolesSgv.GestorVacantes]));

        Assert.False(result.IsSuccess);
        Assert.Equal(UsuarioErrorType.NotFound, result.Error!.Type);
        Assert.Null(context.Gateway.AssignedRoles);
        Assert.Empty(context.Auditoria.Entries);
    }

    [Fact]
    public async Task AsignarRolesAsync_WithValidRoles_AssignsRolesAndAuditsBeforeAfter()
    {
        var context = CreateContext();

        var result = await context.Service.AsignarRolesAsync(
            TargetUserId,
            new AsignarRolesRequest([RolesSgv.GestorVacantes]));

        Assert.True(result.IsSuccess);
        Assert.Equal([RolesSgv.GestorVacantes], result.Value!.Roles);
        var audit = Assert.Single(context.Auditoria.Entries);
        Assert.Equal(RolesSgv.Consultor, audit.Anteriores["Roles"]);
        Assert.Equal(RolesSgv.GestorVacantes, audit.Nuevos["Roles"]);
    }

    [Fact]
    public async Task AsignarRolesAsync_WithUnsupportedRole_RejectsWithoutAssignmentOrAudit()
    {
        var context = CreateContext();

        var result = await context.Service.AsignarRolesAsync(
            TargetUserId,
            new AsignarRolesRequest(["Lector"]));

        Assert.False(result.IsSuccess);
        Assert.Equal("RolNoSoportado", result.Error!.Code);
        Assert.Equal(ErrorCategoria.Validation, result.Error.Categoria);
        Assert.Null(context.Gateway.AssignedRoles);
        Assert.Empty(context.Auditoria.Entries);
    }

    [Fact]
    public async Task ActualizarAsync_WithInvalidEmail_RejectsWithoutCallingGateway()
    {
        var context = CreateContext();
        var request = new ActualizarUsuarioRequest("renamed", "not-an-email", [RolesSgv.Consultor]);

        var result = await context.Service.ActualizarAsync(TargetUserId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal("EmailInvalido", result.Error!.Code);
        Assert.Equal(ErrorCategoria.Validation, result.Error.Categoria);
        Assert.Null(context.Gateway.UpdatedRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ActualizarAsync_WithMissingUserName_RejectsValidation(string userName)
    {
        var context = CreateContext();
        var request = new ActualizarUsuarioRequest(userName, "new@test.com", [RolesSgv.Consultor]);

        var result = await context.Service.ActualizarAsync(TargetUserId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal("DatosInvalidos", result.Error!.Code);
        Assert.Equal(ErrorCategoria.Validation, result.Error.Categoria);
        Assert.Null(context.Gateway.UpdatedRequest);
    }

    [Fact]
    public async Task ActualizarAsync_ValidRequest_UpdatesAllFieldsAtomicallyAndAuditsDiff()
    {
        var context = CreateContext();
        var request = new ActualizarUsuarioRequest(
            "renamed",
            "new@test.com",
            [RolesSgv.Administrador, RolesSgv.Consultor]);

        var result = await context.Service.ActualizarAsync(TargetUserId, request);

        Assert.True(result.IsSuccess);
        Assert.Equal("renamed", result.Value!.UserName);
        Assert.Equal("new@test.com", result.Value.Email);
        Assert.Equal(request.Roles, result.Value.Roles);
        Assert.Equal(request, context.Gateway.UpdatedRequest);
        var audit = Assert.Single(context.Auditoria.Entries);
        Assert.Equal("Modificacion", audit.Accion);
        Assert.Equal("old-name", audit.Anteriores["UserName"]);
        Assert.Equal("renamed", audit.Nuevos["UserName"]);
        Assert.Equal("old@test.com", audit.Anteriores["Email"]);
        Assert.Equal("new@test.com", audit.Nuevos["Email"]);
        Assert.Equal(RolesSgv.Consultor, audit.Anteriores["Roles"]);
        Assert.Equal("Administrador,Consultor", audit.Nuevos["Roles"]);
    }

    [Fact]
    public async Task ActualizarAsync_TwoSequentialAdministrators_LastWriteWinsAndReturnsPersistedDto()
    {
        var context = CreateContext();

        var first = await context.Service.ActualizarAsync(
            TargetUserId,
            new ActualizarUsuarioRequest("first", "first@test.com", [RolesSgv.Consultor]));
        var second = await context.Service.ActualizarAsync(
            TargetUserId,
            new ActualizarUsuarioRequest("second", "second@test.com", [RolesSgv.GestorVacantes]));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("second", second.Value!.UserName);
        Assert.Equal("second@test.com", second.Value.Email);
        Assert.Equal([RolesSgv.GestorVacantes], second.Value.Roles);
        Assert.Equal("second", (await context.Gateway.ObtenerAsync(TargetUserId))!.UserName);
    }

    [Fact]
    public async Task DesactivarAsync_CurrentUser_ReturnsForbiddenAutoBajaWithoutCallingGateway()
    {
        var context = CreateContext(currentUserId: TargetUserId);

        var result = await context.Service.DesactivarAsync(TargetUserId);

        Assert.False(result.IsSuccess);
        Assert.Equal("AutoBaja", result.Error!.Code);
        Assert.Equal(ErrorCategoria.Forbidden, result.Error.Categoria);
        Assert.False(context.Gateway.DesactivarCalled);
        Assert.Empty(context.Auditoria.Entries);
    }

    [Fact]
    public async Task DesactivarAsync_OtherUser_SoftDeletesAndAuditsCriticalFields()
    {
        var context = CreateContext();

        var result = await context.Service.DesactivarAsync(TargetUserId);

        Assert.True(result.IsSuccess);
        Assert.True(context.Gateway.DesactivarCalled);
        var audit = Assert.Single(context.Auditoria.Entries);
        Assert.Equal("BajaLogica", audit.Accion);
        Assert.Equal("old-name", audit.Anteriores["UserName"]);
        Assert.Equal("old@test.com", audit.Anteriores["Email"]);
        Assert.Equal(RolesSgv.Consultor, audit.Anteriores["Roles"]);
    }

    [Fact]
    public async Task ReactivarAsync_WithInactivePersona_ReturnsConflictPersonaInactivaWithoutCallingGateway()
    {
        var inactivePersona = CreatePersona();
        inactivePersona.Desactivar();
        var context = CreateContext(inactivePersona);

        var result = await context.Service.ReactivarAsync(TargetUserId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PersonaInactiva", result.Error!.Code);
        Assert.Equal(ErrorCategoria.Conflict, result.Error.Categoria);
        Assert.False(context.Gateway.ReactivarCalled);
        Assert.Empty(context.Auditoria.Entries);
    }

    [Fact]
    public async Task ReactivarAsync_WithActivePersona_ReactivatesAndAuditsCriticalFields()
    {
        var context = CreateContext();
        await context.Gateway.DesactivarAsync(TargetUserId);

        var result = await context.Service.ReactivarAsync(TargetUserId);

        Assert.True(result.IsSuccess);
        Assert.True(context.Gateway.ReactivarCalled);
        var audit = Assert.Single(context.Auditoria.Entries);
        Assert.Equal("Reactivacion", audit.Accion);
        Assert.Equal("old-name", audit.Nuevos["UserName"]);
        Assert.Equal(RolesSgv.Consultor, audit.Nuevos["Roles"]);
    }

    private static TestContext CreateContext(
        Persona? persona = null,
        string currentUserId = CurrentUserId,
        bool personaExists = true)
    {
        if (personaExists)
        {
            persona ??= CreatePersona();
        }

        var gateway = new FakeUsuarioIdentityGateway();
        gateway.Seed(new UsuarioDto(
            TargetUserId,
            PersonaId,
            "old-name",
            "old@test.com",
            [RolesSgv.Consultor],
            persona?.Nombres,
            persona?.Apellidos));
        var auditoria = new FakeAuditoriaServicio();
        var service = new UsuarioServicioComandos(
            new FakePersonaRepository(persona),
            gateway,
            new FakeUsuarioActual(currentUserId),
            auditoria);
        return new TestContext(service, gateway, auditoria);
    }

    private static Persona CreatePersona()
    {
        return new Persona("Juan", "Perez", "LEG-001", "juan@test.com") { Id = PersonaId };
    }

    private sealed record TestContext(
        UsuarioServicioComandos Service,
        FakeUsuarioIdentityGateway Gateway,
        FakeAuditoriaServicio Auditoria);

    private sealed class FakePersonaRepository(Persona? persona) : IPersonaRepository
    {
        public Task<Persona?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(persona?.Id == id && persona.IsActive ? persona : null);

        public Task<Persona?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(persona?.Id == id ? persona : null);

        public Task<IReadOnlyList<Persona>> ListAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Persona>>(persona is null ? [] : [persona]);

        public Task AddAsync(Persona value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Persona?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);
        public Task UpdateAsync(Persona value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ExistsActiveLegajoAsync(string legajo, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ExistsActiveEmailAsync(string email, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ExistsActiveDocumentoAsync(string tipoDocumento, string numeroDocumento, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<(IReadOnlyList<Persona> Items, int TotalCount)> QueryAsync(
            string? search,
            int page,
            int pageSize,
            string? sort = null,
            PersonaSegmentoListado segmento = PersonaSegmentoListado.Activas,
            CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<Persona>, int)>(([], 0));
    }

    private sealed class FakeUsuarioIdentityGateway : IUsuarioIdentityGateway
    {
        private readonly Dictionary<string, UsuarioDto> _users = new(StringComparer.Ordinal);

        public CrearUsuarioRequest? CreatedRequest { get; private set; }
        public IReadOnlyCollection<string>? AssignedRoles { get; private set; }
        public ActualizarUsuarioRequest? UpdatedRequest { get; private set; }
        public bool DesactivarCalled { get; private set; }
        public bool ReactivarCalled { get; private set; }

        public void Seed(UsuarioDto user) => _users[user.Id] = user;
        public void Remove(string id) => _users.Remove(id);

        public Task<UsuarioDto?> ObtenerAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(_users.GetValueOrDefault(userId));

        public Task<UsuarioCommandResult> CrearAsync(CrearUsuarioRequest request, CancellationToken cancellationToken = default)
        {
            CreatedRequest = request;
            var user = new UsuarioDto(
                "user-created",
                request.PersonaId,
                request.UserName,
                request.Email,
                request.Roles,
                "Juan",
                "Perez");
            _users[user.Id] = user;
            return Task.FromResult(UsuarioCommandResult.Success(user));
        }

        public Task<UsuarioCommandResult> AsignarRolesAsync(
            string userId,
            IReadOnlyCollection<string> roles,
            CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(userId, out var current))
            {
                return Task.FromResult(NotFound());
            }

            AssignedRoles = roles;
            var updated = current with { Roles = roles };
            _users[userId] = updated;
            return Task.FromResult(UsuarioCommandResult.Success(updated));
        }

        public Task<UsuarioCommandResult> ActualizarAsync(
            string userId,
            ActualizarUsuarioRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(userId, out var current))
            {
                return Task.FromResult(NotFound());
            }

            UpdatedRequest = request;
            var updated = current with
            {
                UserName = request.UserName,
                Email = request.Email,
                Roles = request.Roles
            };
            _users[userId] = updated;
            return Task.FromResult(UsuarioCommandResult.Success(updated));
        }

        public Task<UsuarioCommandResult> DesactivarAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(userId, out var current))
            {
                return Task.FromResult(NotFound());
            }

            DesactivarCalled = true;
            return Task.FromResult(UsuarioCommandResult.Success(current));
        }

        public Task<UsuarioCommandResult> ReactivarAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(userId, out var current))
            {
                return Task.FromResult(NotFound());
            }

            ReactivarCalled = true;
            return Task.FromResult(UsuarioCommandResult.Success(current));
        }

        private static UsuarioCommandResult NotFound()
            => UsuarioCommandResult.Failure(new UsuarioError(
                UsuarioErrorType.NotFound,
                "UsuarioNoEncontrado",
                "El usuario no existe.",
                Categoria: ErrorCategoria.NotFound));
    }

    private sealed class FakeUsuarioActual(string userId) : IUsuarioActual
    {
        public string? UserId => userId;
        public Guid? PersonaId => UsuarioServicioComandosTests.PersonaId;
        public IReadOnlyCollection<string> Roles => [RolesSgv.Administrador];
        public Guid? CorrelationId => Guid.Parse("a1000000-0000-0000-0000-000000000001");
    }

    private sealed class FakeAuditoriaServicio : IAuditoriaServicio
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task RegistrarAsync(
            string entidad,
            string entityId,
            string accion,
            string? usuarioOperadorId,
            IReadOnlyDictionary<string, object?> valoresAnteriores,
            IReadOnlyDictionary<string, object?> valoresNuevos,
            CancellationToken cancellationToken = default)
        {
            Entries.Add(new AuditEntry(
                entidad,
                entityId,
                accion,
                usuarioOperadorId,
                valoresAnteriores,
                valoresNuevos));
            return Task.CompletedTask;
        }
    }

    private sealed record AuditEntry(
        string Entidad,
        string EntityId,
        string Accion,
        string? UsuarioOperadorId,
        IReadOnlyDictionary<string, object?> Anteriores,
        IReadOnlyDictionary<string, object?> Nuevos);
}
