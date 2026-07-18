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

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@no-local-part.com")]
    [InlineData("missing-at-sign.com")]
    public async Task CrearAsync_WithInvalidEmail_ReturnsEmailInvalidoValidation(string invalidEmail)
    {
        // PR #148 review: ActualizarAsync ya validaba formato de email
        // con MailAddress.TryCreate, pero CrearAsync no lo hacía. El
        // helper compartido IsValidEmail garantiza el mismo
        // comportamiento en ambos puntos de entrada para no aceptar
        // emails que el backend de Identity rechazaría downstream.
        var context = CreateContext();
        var request = new CrearUsuarioRequest(PersonaId, "admin", invalidEmail, "Password1!", [RolesSgv.Administrador]);

        var result = await context.Service.CrearAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("EmailInvalido", result.Error!.Code);
        Assert.Equal(ErrorCategoria.Validation, result.Error.Categoria);
        Assert.Null(context.Gateway.CreatedRequest);
    }

    [Theory]
    [InlineData("admin@test.com")]
    [InlineData("first.last+tag@sub.domain.example.com")]
    public async Task CrearAsync_WithValidEmail_ProceedsToGateway(string validEmail)
    {
        var context = CreateContext();
        var request = new CrearUsuarioRequest(PersonaId, "admin", validEmail, "Password1!", [RolesSgv.Administrador]);

        var result = await context.Service.CrearAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(validEmail, context.Gateway.CreatedRequest!.Email);
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
    public async Task BloquearAsync_CalledTwice_AuditsOnce()
    {
        var context = CreateContext();

        var first = await context.Service.BloquearAsync(TargetUserId);
        var second = await context.Service.BloquearAsync(TargetUserId);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.True(second.Value!.Bloqueado);
        Assert.Single(context.Auditoria.Entries);
    }

    [Fact]
    public async Task BloquearAsync_OtherUser_WithExistingLockout_SucceedsWithoutDoubleAudit()
    {
        var context = CreateContext(seedBloqueado: true);

        var result = await context.Service.BloquearAsync(TargetUserId);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Bloqueado);
        Assert.Empty(context.Auditoria.Entries);
    }

    [Fact]
    public async Task EliminarAsync_CalledTwice_SecondReturns404NotFound()
    {
        var context = CreateContext();

        var first = await context.Service.EliminarAsync(TargetUserId);
        var second = await context.Service.EliminarAsync(TargetUserId);

        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Equal(UsuarioErrorType.NotFound, second.Error!.Type);
        Assert.Equal("UsuarioNoEncontrado", second.Error.Code);
        Assert.Single(context.Auditoria.Entries);
    }

    [Fact]
    public async Task EliminarAsync_WhenUserDoesNotExist_AuditsNothingAndReturnsNotFound()
    {
        var context = CreateContext();
        context.Gateway.Remove(TargetUserId);

        var result = await context.Service.EliminarAsync(TargetUserId);

        Assert.False(result.IsSuccess);
        Assert.Equal(UsuarioErrorType.NotFound, result.Error!.Type);
        Assert.Equal("UsuarioNoEncontrado", result.Error.Code);
        Assert.Empty(context.Auditoria.Entries);
    }

    [Fact]
    public async Task BloquearAsync_CurrentUser_ReturnsForbiddenAutoBloqueoWithoutCallingGateway()
    {
        var context = CreateContext(currentUserId: TargetUserId);

        var result = await context.Service.BloquearAsync(TargetUserId);

        Assert.False(result.IsSuccess);
        Assert.Equal("AutoBloqueo", result.Error!.Code);
        Assert.Equal(ErrorCategoria.Forbidden, result.Error.Categoria);
        Assert.False(context.Gateway.BloquearCalled);
        Assert.Empty(context.Auditoria.Entries);
    }

    [Fact]
    public async Task ActualizarAsync_CurrentUser_ReturnsForbiddenAutoCambioRolWithoutCallingGateway()
    {
        // Defensa simétrica a AutoBloqueo/AutoEliminacion: el admin no
        // puede cambiarse su propio rol a través de ActualizarAsync. El
        // gateway NO debe invocarse: la validación corre antes de tocar
        // identityGateway.ObtenerAsync / ActualizarAsync.
        var context = CreateContext(currentUserId: TargetUserId);
        var request = new ActualizarUsuarioRequest(
            "renamed",
            "new@test.com",
            [RolesSgv.Administrador]);

        var result = await context.Service.ActualizarAsync(TargetUserId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal("AutoCambioRol", result.Error!.Code);
        Assert.Equal(UsuarioErrorType.Unauthorized, result.Error.Type);
        Assert.Equal(ErrorCategoria.Forbidden, result.Error.Categoria);
        Assert.Null(context.Gateway.UpdatedRequest);
        Assert.Empty(context.Auditoria.Entries);
    }

    [Fact]
    public async Task BloquearAsync_OtherUser_LocksAndAuditsCriticalFields()
    {
        var context = CreateContext();

        var result = await context.Service.BloquearAsync(TargetUserId);

        Assert.True(result.IsSuccess);
        Assert.True(context.Gateway.BloquearCalled);
        var audit = Assert.Single(context.Auditoria.Entries);
        Assert.Equal("BloqueoUsuario", audit.Accion);
        Assert.Equal("old-name", audit.Nuevos["UserName"]);
        Assert.Equal(RolesSgv.Consultor, audit.Nuevos["Roles"]);
    }

    [Fact]
    public async Task DesbloquearAsync_OtherUser_UnlocksAndAuditsCriticalFields()
    {
        var context = CreateContext(seedBloqueado: true);

        var result = await context.Service.DesbloquearAsync(TargetUserId);

        Assert.True(result.IsSuccess);
        Assert.True(context.Gateway.DesbloquearCalled);
        var audit = Assert.Single(context.Auditoria.Entries);
        Assert.Equal("DesbloqueoUsuario", audit.Accion);
    }

    [Fact]
    public async Task DesbloquearAsync_WhenAlreadyUnlocked_SucceedsWithoutAudit()
    {
        var context = CreateContext();

        var result = await context.Service.DesbloquearAsync(TargetUserId);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Bloqueado);
        Assert.Empty(context.Auditoria.Entries);
    }

    [Fact]
    public async Task EliminarAsync_OtherUser_DeletesAndAuditsEliminacionFisica()
    {
        var context = CreateContext();

        var result = await context.Service.EliminarAsync(TargetUserId);

        Assert.True(result.IsSuccess);
        Assert.True(context.Gateway.EliminarCalled);
        var audit = Assert.Single(context.Auditoria.Entries);
        Assert.Equal("EliminacionFisica", audit.Accion);
        Assert.Equal("old-name", audit.Anteriores["UserName"]);
        Assert.Equal(RolesSgv.Consultor, audit.Anteriores["Roles"]);
    }

    [Fact]
    public async Task EliminarAsync_WhenGatewayDeleteFails_StillPersistsAudit()
    {
        // RES-002 (4R review): con delete físico fallando, la auditoría
        // de EliminacionFisica ya debe estar persistida.
        var context = CreateContext();
        context.Gateway.EliminarShouldFail = true;

        var result = await context.Service.EliminarAsync(TargetUserId);

        Assert.False(result.IsSuccess);
        Assert.True(context.Gateway.EliminarCalled);
        var audit = Assert.Single(context.Auditoria.Entries);
        Assert.Equal("EliminacionFisica", audit.Accion);
        Assert.Equal("old-name", audit.Anteriores["UserName"]);
    }

    [Fact]
    public async Task EliminarAsync_AuditsPreviousBloqueadoFlag()
    {
        // RIS-004 (4R review): la auditoría de EliminacionFisica debe
        // contener el Bloqueado previo del snapshot.
        var context = CreateContext(seedBloqueado: true);

        var result = await context.Service.EliminarAsync(TargetUserId);

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(context.Auditoria.Entries);
        Assert.Equal("EliminacionFisica", audit.Accion);
        Assert.Equal(true, audit.Anteriores["Bloqueado"]);
    }

    private static TestContext CreateContext(
        Persona? persona = null,
        string currentUserId = CurrentUserId,
        bool personaExists = true,
        bool seedBloqueado = false)
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
            persona?.Apellidos,
            Bloqueado: seedBloqueado));
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
            CancellationToken cancellationToken = default,
            bool? soloSinUsuario = null)
            => Task.FromResult<(IReadOnlyList<Persona>, int)>(([], 0));
    }

    private sealed class FakeUsuarioIdentityGateway : IUsuarioIdentityGateway
    {
        private readonly Dictionary<string, UsuarioDto> _users = new(StringComparer.Ordinal);

        public CrearUsuarioRequest? CreatedRequest { get; private set; }
        public IReadOnlyCollection<string>? AssignedRoles { get; private set; }
        public ActualizarUsuarioRequest? UpdatedRequest { get; private set; }
        public bool BloquearCalled { get; private set; }
        public bool DesbloquearCalled { get; private set; }
        public bool EliminarCalled { get; private set; }

        /// <summary>
        /// RES-002 (4R review): cuando es <c>true</c>, la próxima
        /// invocación de <see cref="EliminarAsync"/> retorna un failure
        /// sin tocar el store. Usado por
        /// <c>EliminarAsync_WhenGatewayDeleteFails_StillPersistsAudit</c>
        /// para verificar que la auditoría persiste aún si el delete
        /// físico falla.
        /// </summary>
        public bool EliminarShouldFail { get; set; }

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

        public Task<UsuarioCommandResult> BloquearAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(userId, out var current))
            {
                return Task.FromResult(NotFound());
            }

            BloquearCalled = true;
            var blocked = current with { Bloqueado = true };
            _users[userId] = blocked;
            return Task.FromResult(UsuarioCommandResult.Success(blocked));
        }

        public Task<UsuarioCommandResult> DesbloquearAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(userId, out var current))
            {
                return Task.FromResult(NotFound());
            }

            DesbloquearCalled = true;
            var unblocked = current with { Bloqueado = false };
            _users[userId] = unblocked;
            return Task.FromResult(UsuarioCommandResult.Success(unblocked));
        }

        public Task<UsuarioCommandResult> EliminarAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(userId, out var current))
            {
                return Task.FromResult(NotFound());
            }

            EliminarCalled = true;

            // RES-002: si el flag está activo, simulamos un fallo del
            // gateway (ej. FK constraint o timeout) sin tocar el store, de
            // manera que la prueba verifique que la auditoría ya fue
            // persistida antes de este punto.
            if (EliminarShouldFail)
            {
                return Task.FromResult(Failure(
                    UsuarioErrorType.Conflict,
                    "EliminacionFallida",
                    "No se pudo completar la eliminación física.",
                    ErrorCategoria.Conflict));
            }

            _users.Remove(userId);
            return Task.FromResult(UsuarioCommandResult.Success(current));
        }

        private static UsuarioCommandResult Failure(
            UsuarioErrorType type, string code, string message, ErrorCategoria categoria)
            => UsuarioCommandResult.Failure(new UsuarioError(
                type, code, message, Categoria: categoria));

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
