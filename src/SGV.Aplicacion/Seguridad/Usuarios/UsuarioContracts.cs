using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Aplicacion.Seguridad.Usuarios;

/// <summary>
/// Port through which the application layer reaches the Identity store
/// to create users and assign roles. Implemented by
/// <c>SGV.Infraestructura.Seguridad.UsuarioIdentityGateway</c>.
/// </summary>
/// <remarks>
/// Cambio <c>2026-07-15-quita-soft-delete-usuario</c>: <c>Desactivar</c> /
/// <c>Reactivar</c> se retiran junto con la columna <c>IsDeleted</c> y
/// las columnas generadas <c>ActiveUserNameUnique</c> /
/// <c>ActivePersonaIdUnique</c>. La separación activa/bloqueada vive
/// ahora en <see cref="UserManager{TUser}.IsLockedOutAsync"/> /
/// <see cref="UserManager{TUser}.SetLockoutEndDateAsync"/> nativos de
/// ASP.NET Core Identity; <c>Eliminar</c> ejecuta
/// <see cref="UserManager{TUser}.DeleteAsync"/> (hard-delete con
/// cascade técnico a AspNetUserRoles/Claims/Logins/Tokens).
/// </remarks>
public interface IUsuarioIdentityGateway
{
    Task<UsuarioCommandResult> CrearAsync(CrearUsuarioRequest request, CancellationToken cancellationToken = default);

    Task<UsuarioCommandResult> AsignarRolesAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);

    Task<UsuarioDto?> ObtenerAsync(string userId, CancellationToken cancellationToken = default);

    Task<UsuarioCommandResult> ActualizarAsync(string userId, ActualizarUsuarioRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bloquea una cuenta fijando <c>LockoutEnabled=true</c> y
    /// <c>LockoutEnd</c> a un instante futuro. Idempotente: repetir el
    /// bloqueo sobre una cuenta ya bloqueada preserva <c>LockoutEnd</c>
    /// sin duplicar efectos colaterales.
    /// </summary>
    Task<UsuarioCommandResult> BloquearAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Desbloquea una cuenta limpiando <c>LockoutEnd</c> (a <c>null</c>)
    /// manteniendo <c>LockoutEnabled=true</c> (contrato Identity).
    /// </summary>
    Task<UsuarioCommandResult> DesbloquearAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Eliminación física de la cuenta Identity. <c>UserManager.DeleteAsync</c>
    /// purga la fila de <c>AspNetUsers</c> y las cascadas técnicas a
    /// <c>AspNetUserRoles/Claims/Logins/Tokens</c>. La <c>Persona</c>
    /// vinculada sobrevive (FK RESTRICT sobre <c>AspNetUsers.PersonaId</c>)
    /// y las <c>Auditorias</c> referenciando el UserId permanecen
    /// consultables (la columna <c>UserId</c> es string sin FK).
    /// </summary>
    Task<UsuarioCommandResult> EliminarAsync(string userId, CancellationToken cancellationToken = default);
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

    /// <summary>
    /// Bloqueo administrativo de un usuario. Idempotente.
    /// </summary>
    Task<UsuarioCommandResult> BloquearAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Desbloqueo de un usuario. Idempotente.
    /// </summary>
    Task<UsuarioCommandResult> DesbloquearAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Eliminación física de un usuario. Idempotente: el segundo intento
    /// devuelve <c>UsuarioNoEncontrado</c>.
    /// </summary>
    Task<UsuarioCommandResult> EliminarAsync(string userId, CancellationToken cancellationToken = default);
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
/// <remarks>
/// Cambio <c>2026-07-15-quita-soft-delete-usuario</c>: el chequeo de
/// bloqueo se delega a <see cref="UserManager{TUser}.IsLockedOutAsync"/>
/// antes de validar la contraseña, replicando el comportamiento de
/// <see cref="SignInManager{TUser}.PasswordSignInAsync"/>.
/// </remarks>
public interface IAuthServicio
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}