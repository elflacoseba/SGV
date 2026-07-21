using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Consultas.Dtos;

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
/// Request to atomically update the editable identity fields and role set.
/// </summary>
public sealed record ActualizarUsuarioRequest(
    string UserName,
    string Email,
    IReadOnlyCollection<string> Roles);

/// <summary>
/// Selects users by lockout state for paginated queries. Replaces the
/// previous <c>Eliminadas</c> segment that mapped to the now-removed
/// <c>IsDeleted</c> column; <c>Bloqueadas</c> covers both
/// administrative lockouts and Identity's <c>MaxFailedAccessAttempts</c>
/// temporary lockouts — see design decision D5.
/// </summary>
public enum UsuarioSegmentoListado
{
    Activas = 0,
    Bloqueadas = 1,

    /// <summary>
    /// DEPRECATED alias for <see cref="Bloqueadas"/>. Mantenido
    /// temporalmente en Phase 1 para que el código Web (Pages Razor,
    /// tests de FakeUsuarioApiClient) siga compilando; el rediseño de
    /// la UI (Pages) ocurre en Phase 3 y retira este alias.
    /// </summary>
    [Obsolete("Use Bloqueadas. Phase 3 retira este alias.")]
    Eliminadas = Bloqueadas
}

/// <summary>
/// Parameters for server-side user filtering, sorting, and pagination.
/// </summary>
public sealed record UsuarioListQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Sort,
    UsuarioSegmentoListado Segmento = UsuarioSegmentoListado.Activas);

/// <summary>
/// Compatibility wrapper around the generic paginated user result.
/// </summary>
public sealed record UsuarioListadoDto(PagedResult<UsuarioDto> Result);

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
/// Request to initiate the password recovery flow. The supplied
/// identifier is matched against both <c>UserName</c> and
/// <c>Email</c> (case-insensitive) by
/// <c>IPasswordResetService.ForgotPasswordAsync</c>; an unknown
/// identifier yields the same response shape to prevent user
/// enumeration.
/// </summary>
public sealed record ForgotPasswordRequest(string UserNameOrEmail);

/// <summary>
/// Request to finalize the password recovery flow. The token is the
/// URL-encoded string delivered in the recovery email; the Web shell
/// MUST <c>Uri.UnescapeDataString</c> it before sending the request.
/// <see cref="NewPassword"/> is validated against the same policy that
/// <c>IdentityOptions.Password</c> enforces at signup
/// (<c>RequiredLength=6</c>, requires lowercase, uppercase, digit and
/// non-alphanumeric).
/// </summary>
public sealed record ResetPasswordRequest(string UserId, string Token, string NewPassword);

/// <summary>
/// Lightweight token validation request for the password recovery flow.
/// Used by the Web shell to verify a reset link is still valid before
/// showing the password form to the user. The token MUST be
/// URL-decoded before sending.
/// </summary>
public sealed record ValidateResetTokenRequest(string UserId, string Token);

/// <summary>
/// Projection of a SGV user exposed by the API. Carries the linked
/// persona id, the identity username/email, the assigned role names
/// and the lockout flag (<c>Bloqueado</c>) derived from
/// <c>LockoutEnd &gt; UtcNow</c>. Migration
/// <c>2026-07-15-quita-soft-delete-usuario</c> (REA-009 / RIS-006 /
/// REL-001): el flag se rellena tanto en
/// <c>UsuarioIdentityGateway.MapAsync</c> como en la proyección de
/// <c>QueryAsync</c>; el default <c>false</c> mantiene source-compat
/// con los 23 <c>new UsuarioDto(...)</c> posicionales vigentes.
/// </summary>
public sealed record UsuarioDto(
    string Id,
    Guid PersonaId,
    string UserName,
    string Email,
    IReadOnlyCollection<string> Roles,
    string? Nombres = null,
    string? Apellidos = null,
    bool Bloqueado = false);

/// <summary>
/// Categorizes failures produced by user-management write operations.
/// </summary>
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
/// <see cref="UsuarioError"/> on failure. <see cref="FieldErrors"/>
/// carries per-field validation errors when the backend returned a
/// <c>ValidationProblemDetails</c> with a non-null <c>errors</c>
/// payload; otherwise it is <c>null</c>.
/// </summary>
/// <remarks>
/// PR2-HALL-1 (mini-PR correctivo): se extendió el record del PR1 con
/// la propiedad <see cref="FieldErrors"/> y la sobrecarga
/// <see cref="Failure(UsuarioError, IReadOnlyDictionary{string, string[]})"/>
/// para cerrar el gap que impedía propagar errores de validación por
/// campo a la Razor Page de Create/Edit (PR 4). El shape espeja al
/// canónico de <c>CargoCommandResult</c>, <c>PuestoCommandResult</c>,
/// <c>UnidadOrganizativaCommandResult</c>, <c>HabilidadCommandResult</c>
/// y <c>PersonaCommandResult</c>: diccionario con valores
/// <c>string[]</c> (no <c>string</c> único) y default <c>null</c>
/// para mantener source-compat con los call sites del PR2 que
/// invocan <see cref="Failure(UsuarioError)"/>.
/// </remarks>
public sealed record UsuarioCommandResult(
    bool IsSuccess,
    UsuarioDto? Value,
    UsuarioError? Error,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public static UsuarioCommandResult Success(UsuarioDto value) => new(true, value, null);

    public static UsuarioCommandResult Failure(UsuarioError error) => new(false, null, error);

    public static UsuarioCommandResult Failure(
        UsuarioError error,
        IReadOnlyDictionary<string, string[]> fieldErrors)
        => new(false, null, error, fieldErrors);
}