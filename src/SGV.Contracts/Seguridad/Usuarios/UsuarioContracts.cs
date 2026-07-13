using SGV.Contracts.Comun;

namespace SGV.Contracts.Seguridad.Usuarios;

/// <summary>
/// Request to create a new SGV user linked to an existing persona.
/// </summary>
public sealed record CrearUsuarioRequest(
    Guid PersonaId,
    string UserName,
    string Email,
    string Password,
    IReadOnlyCollection<string> Roles);

/// <summary>
/// Request to replace the role set of an existing SGV user.
/// </summary>
public sealed record AsignarRolesRequest(IReadOnlyCollection<string> Roles);

/// <summary>
/// Credentials payload for the login endpoint. Accepts either a username
/// or an email address in <see cref="UserNameOrEmail"/>.
/// </summary>
public sealed record LoginRequest(string UserNameOrEmail, string Password);

/// <summary>
/// Successful login response with a bearer access token and its absolute
/// expiration timestamp.
/// </summary>
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);

/// <summary>
/// Projection of a SGV user exposed by the API. Carries the linked
/// persona id, the identity username/email and the assigned role names.
/// </summary>
public sealed record UsuarioDto(
    string Id,
    Guid PersonaId,
    string UserName,
    string Email,
    IReadOnlyCollection<string> Roles);

/// <summary>
/// Categorizes failures produced by user-management write operations.
/// </summary>
[Obsolete("Use SGV.Contracts.Comun.ErrorCategoria. Will be removed in the archive of change 2026-07-13.")]
public enum UsuarioErrorType
{
    NotFound,
    Conflict,
    Validation,
    Unauthorized
}

/// <summary>
/// Typed error returned by user write operations. <see cref="Code"/> is a
/// stable machine identifier; <see cref="Message"/> is a human-readable
/// explanation suitable for client surfacing.
/// </summary>
public sealed record UsuarioError(
    UsuarioErrorType Type,
    string Code,
    string Message,
    int? StatusCode = null,
    ErrorCategoria Categoria = ErrorCategoria.Unexpected);

/// <summary>
/// Discriminated result of a user-management write operation. Carries
/// either the persisted <see cref="UsuarioDto"/> on success or a typed
/// <see cref="UsuarioError"/> on failure.
/// </summary>
public sealed record UsuarioCommandResult(bool IsSuccess, UsuarioDto? Value, UsuarioError? Error)
{
    public static UsuarioCommandResult Success(UsuarioDto value) => new(true, value, null);

    public static UsuarioCommandResult Failure(UsuarioError error) => new(false, null, error);
}