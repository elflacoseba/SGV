using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Aplicacion.Seguridad.Usuarios;

/// <summary>
/// Port through which the application layer reaches the Identity store
/// to create users and assign roles. Implemented by
/// <c>SGV.Infraestructura.Seguridad.UsuarioIdentityGateway</c>.
/// </summary>
public interface IUsuarioIdentityGateway
{
    Task<UsuarioCommandResult> CrearAsync(CrearUsuarioRequest request, CancellationToken cancellationToken = default);

    Task<UsuarioCommandResult> AsignarRolesAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);

    Task<UsuarioDto?> ObtenerAsync(string userId, CancellationToken cancellationToken = default);

    Task<UsuarioCommandResult> ActualizarAsync(string userId, ActualizarUsuarioRequest request, CancellationToken cancellationToken = default);

    Task<UsuarioCommandResult> DesactivarAsync(string userId, CancellationToken cancellationToken = default);

    Task<UsuarioCommandResult> ReactivarAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Application service that orchestrates persona lookup and role catalog
/// validation before delegating the actual Identity write to
/// <see cref="IUsuarioIdentityGateway"/>.
/// </summary>
public interface IUsuarioServicioComandos
{
    Task<UsuarioCommandResult> CrearAsync(CrearUsuarioRequest request, CancellationToken cancellationToken = default);

    Task<UsuarioCommandResult> AsignarRolesAsync(string userId, AsignarRolesRequest request, CancellationToken cancellationToken = default);

    Task<UsuarioCommandResult> ActualizarAsync(string userId, ActualizarUsuarioRequest request, CancellationToken cancellationToken = default);

    Task<UsuarioCommandResult> DesactivarAsync(string userId, CancellationToken cancellationToken = default);

    Task<UsuarioCommandResult> ReactivarAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Application query service that exposes the SGV user catalog.
/// </summary>
public interface IUsuarioServicioConsulta
{
    Task<IReadOnlyList<UsuarioDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<UsuarioDto?> GetByIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<PagedResult<UsuarioDto>> QueryAsync(UsuarioListQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Application query service that exposes the fixed SGV role catalog
/// (see <see cref="RolesSgv.Todos"/>).
/// </summary>
public interface IRolServicioConsulta
{
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Application service that authenticates credentials and issues a JWT
/// bearer token. Returns <c>null</c> when credentials are invalid so
/// the API layer can map it to a 401 response.
/// </summary>
public interface IAuthServicio
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}