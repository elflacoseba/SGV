using SGV.Aplicacion.Personas.Consultas;
using SGV.Aplicacion.Seguridad;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Dominio.Personas;
using Xunit;

namespace SGV.Tests.Aplicacion.Seguridad;

public sealed class UsuarioServicioComandosTests
{
    private static readonly Guid PersonaId = Guid.Parse("e1000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task CrearAsync_WithExistingPersonaAndFixedRoles_CreatesLinkedUser()
    {
        var gateway = new FakeUsuarioIdentityGateway();
        var service = new UsuarioServicioComandos(new FakePersonaRepository(CreatePersona()), gateway);
        var request = new CrearUsuarioRequest(PersonaId, "admin", "admin@test.com", "Password1!", [RolesSgv.Administrador]);

        var result = await service.CrearAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(PersonaId, result.Value!.PersonaId);
        Assert.Equal([RolesSgv.Administrador], result.Value.Roles);
        Assert.Equal(PersonaId, gateway.CreatedRequest!.PersonaId);
    }

    [Fact]
    public async Task CrearAsync_WithoutExistingPersona_RejectsWithoutCreatingIdentityUser()
    {
        var gateway = new FakeUsuarioIdentityGateway();
        var service = new UsuarioServicioComandos(new FakePersonaRepository(null), gateway);
        var request = new CrearUsuarioRequest(PersonaId, "admin", "admin@test.com", "Password1!", [RolesSgv.Administrador]);

        var result = await service.CrearAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(UsuarioErrorType.NotFound, result.Error!.Type);
        Assert.Null(gateway.CreatedRequest);
    }

    [Fact]
    public async Task CrearAsync_WithUnsupportedRole_RejectsWithoutCreatingIdentityUser()
    {
        var gateway = new FakeUsuarioIdentityGateway();
        var service = new UsuarioServicioComandos(new FakePersonaRepository(CreatePersona()), gateway);
        var request = new CrearUsuarioRequest(PersonaId, "admin", "admin@test.com", "Password1!", ["Lector"]);

        var result = await service.CrearAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(UsuarioErrorType.Validation, result.Error!.Type);
        Assert.Null(gateway.CreatedRequest);
    }

    [Fact]
    public async Task AsignarRolesAsync_WithMissingUser_RejectsWithoutRoleAssignment()
    {
        var gateway = new FakeUsuarioIdentityGateway { MissingUserOnAssignment = true };
        var service = new UsuarioServicioComandos(new FakePersonaRepository(CreatePersona()), gateway);

        var result = await service.AsignarRolesAsync("missing-user", new AsignarRolesRequest([RolesSgv.GestorVacantes]));

        Assert.False(result.IsSuccess);
        Assert.Equal(UsuarioErrorType.NotFound, result.Error!.Type);
        Assert.Null(gateway.AssignedRoles);
    }

    [Fact]
    public async Task AsignarRolesAsync_WithValidRoles_AssignsRoles()
    {
        var gateway = new FakeUsuarioIdentityGateway();
        var service = new UsuarioServicioComandos(new FakePersonaRepository(CreatePersona()), gateway);

        var result = await service.AsignarRolesAsync("user-1", new AsignarRolesRequest([RolesSgv.GestorVacantes]));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Contains(RolesSgv.GestorVacantes, result.Value!.Roles);
    }

    [Fact]
    public async Task AsignarRolesAsync_WithUnsupportedRole_RejectsWithoutAssignment()
    {
        var gateway = new FakeUsuarioIdentityGateway();
        var service = new UsuarioServicioComandos(new FakePersonaRepository(CreatePersona()), gateway);

        var result = await service.AsignarRolesAsync("user-1", new AsignarRolesRequest(["Lector"]));

        Assert.False(result.IsSuccess);
        Assert.Equal(UsuarioErrorType.Validation, result.Error!.Type);
        Assert.Null(gateway.AssignedRoles);
    }

    private static Persona CreatePersona()
    {
        return new Persona("Juan", "Perez", "LEG-001", "juan@test.com") { Id = PersonaId };
    }

    private sealed class FakePersonaRepository(Persona? persona) : IPersonaRepository
    {
        public Task<Persona?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(persona?.Id == id ? persona : null);

        public Task<IReadOnlyList<Persona>> ListAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Persona>>(persona is null ? [] : [persona]);

        public Task AddAsync(Persona persona, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Persona?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);
        public Task<Persona?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);
        public Task UpdateAsync(Persona persona, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ExistsActiveLegajoAsync(string legajo, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ExistsActiveEmailAsync(string email, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ExistsActiveDocumentoAsync(string tipoDocumento, string numeroDocumento, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class FakeUsuarioIdentityGateway : IUsuarioIdentityGateway
    {
        public CrearUsuarioRequest? CreatedRequest { get; private set; }
        public IReadOnlyCollection<string>? AssignedRoles { get; private set; }
        public bool MissingUserOnAssignment { get; init; }

        public Task<UsuarioCommandResult> CrearAsync(CrearUsuarioRequest request, CancellationToken cancellationToken = default)
        {
            CreatedRequest = request;
            return Task.FromResult(UsuarioCommandResult.Success(new UsuarioDto("user-1", request.PersonaId, request.UserName, request.Email, request.Roles)));
        }

        public Task<UsuarioCommandResult> AsignarRolesAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
        {
            if (MissingUserOnAssignment)
            {
                return Task.FromResult(UsuarioCommandResult.Failure(new UsuarioError(UsuarioErrorType.NotFound, "UsuarioNoEncontrado", "El usuario no existe.")));
            }

            AssignedRoles = roles;
            return Task.FromResult(UsuarioCommandResult.Success(new UsuarioDto(userId, PersonaId, "admin", "admin@test.com", roles)));
        }
    }
}
